# 06 – Database (MySQL)

## 1. Quy ước

| Hạng mục | Quy ước |
|---|---|
| Server | MySQL ở `localhost:3306` (Docker `mysql-dev`, chỉ giữ DB), database `homestay`, `utf8mb4_unicode_ci` |
| Tên | Bảng / cột `snake_case` (EFCore.NamingConventions), index `ix_…`, unique `ux_…` |
| Khoá chính | `users.id` = `char(36)` (Guid – không đoán được); các bảng khác `bigint` tự tăng |
| Thời điểm | `datetime(6)` lưu **UTC** |
| Ngày nghiệp vụ | `date` (ngày nhận / trả phòng, đêm); giờ nhận / trả phòng `time` |
| Tiền | `decimal(15,2)` (VND dùng số nguyên) – không bao giờ `float/double` |
| Enum | `varchar` snake_case chữ thường (`pending`, `checked_in`, `bank_transfer`…) + CHECK `IN (…)`; JSON trả cùng cách viết |
| Xoá | Giữa bảng nghiệp vụ dùng `RESTRICT` (service kiểm tra trước và trả `409`); bảng phụ thuộc thuần (`booking_nights`, `booking_events`, `homestay_amenities`, `photos`, `rate_overrides`, `refresh_tokens`) dùng `CASCADE`. Riêng `room_nights` dùng `RESTRICT` (MySQL cấm CHECK trên cột có hành động FK) – service xoá dòng khoá trong cùng transaction |

## 2. Khởi tạo database (làm 1 lần)

Chạy bằng root trong container (mật khẩu lấy từ biến môi trường của container, không gõ ra màn hình):

```bash
docker exec -i mysql-dev sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD"' <<'SQL'
CREATE DATABASE IF NOT EXISTS homestay CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'homestay'@'%' IDENTIFIED BY '<mật khẩu trong backend/.env>';
GRANT ALL PRIVILEGES ON homestay.* TO 'homestay'@'%';
-- DB cho backend.Tests (test tự xoá + tạo lại, không đụng DB homestay)
GRANT ALL PRIVILEGES ON `homestay_test`.* TO 'homestay'@'%';
FLUSH PRIVILEGES;
SQL
```

User `homestay` **chỉ** có quyền trên `homestay.*` và `homestay_test.*`. Tạo bảng: `cd backend && dotnet tool restore && dotnet ef database update`
(Development tự migrate khi `dotnet run`).

## 3. ERD

```
users 1──* refresh_tokens
users 1──* bookings (khách – có thể null: đặt hộ)        users 1──* payments (người ghi)
homestays 1──* rooms 1──* rate_overrides (room, date)
homestays 1──* photos           homestays 1──* homestay_amenities
rooms 1──* bookings 1──* booking_nights (đêm + giá chụp lại)
rooms 1──* room_blocks
rooms 1──* room_nights *──1 bookings | room_blocks        ← khoá đêm, UNIQUE (room_id, night)
bookings 1──* payments   bookings 1──* booking_events   bookings 1──0..1 reviews
email_outbox (độc lập)
```

## 4. Bảng

### 4.1 `users`, `refresh_tokens`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | `char(36)` PK | |
| `email` | `varchar(254)` **ux** | so sánh không phân biệt hoa thường (collation) |
| `password_hash` | `varchar(100)` | BCrypt work factor 11 |
| `full_name` | `varchar(100)` | |
| `phone` | `varchar(15)` null | chuẩn hoá: di động `0xxxxxxxxx` (10 số), cố định `02xxxxxxxxx` (11 số); `+84` / `84` → `0` |
| `role` | `varchar(10)` | `guest` · `staff` · `admin` – `ix (role, is_active)` |
| `is_active` | `bool` | khoá tài khoản nhân viên |
| `last_login_at`, `created_at`, `updated_at` | `datetime(6)` | |

`refresh_tokens`: `id`, `user_id` FK CASCADE, `token_hash` `char(64)` **ux**, `expires_at`, `revoked_at`, `replaced_by_hash`, `user_agent`, `created_at` – giống Sổ Chi Tiêu.

### 4.2 `homestays`, `homestay_amenities`, `photos`

| Cột (`homestays`) | Kiểu | Ghi chú |
|---|---|---|
| `id` | `bigint` PK | |
| `slug` | `varchar(140)` **ux** | |
| `name` | `varchar(120)` | |
| `province`, `address` | `varchar(60)`, `varchar(255)` | `ix (province, status)` |
| `description` | `text` | |
| `check_in_time`, `check_out_time` | `time` | |
| `deposit_percent`, `hold_hours`, `free_cancel_days`, `partial_refund_days`, `partial_refund_percent` | `tinyint unsigned` | §2 nghiệp vụ |
| `status` | `varchar(10)` | `active` · `hidden` |
| `created_at`, `updated_at` | `datetime(6)` | |

`homestay_amenities`: PK (`homestay_id`, `code`), FK CASCADE.
`photos`: `id`, `homestay_id` FK, `file_name` `varchar(80)` (guid + đuôi), `width`, `height`, `sort_order` (0 = ảnh bìa), `created_at`.

### 4.3 `rooms`, `rate_overrides`

| Cột (`rooms`) | Kiểu | Ghi chú |
|---|---|---|
| `id` | `bigint` PK | |
| `homestay_id` | FK RESTRICT | **ux** (`homestay_id`, `name`) |
| `name` | `varchar(80)` | |
| `description`, `bed_info` | `text`, `varchar(120)` | |
| `area_m2` | `smallint` null | |
| `base_guests`, `max_guests`, `min_nights` | `tinyint` | CHECK `1 ≤ base_guests ≤ max_guests ≤ 20` |
| `base_price`, `weekend_price` (null), `extra_guest_fee`, `cleaning_fee` | `decimal(15,2)` | CHECK `base_price > 0` |
| `status`, `sort_order` | `varchar(10)`, `int` | `active` · `inactive` |
| `created_at`, `updated_at` | `datetime(6)` | |

`rate_overrides`: PK (`room_id`, `date`), `price` `decimal(15,2)` CHECK `> 0`, `note` `varchar(100)`, `created_at`.

### 4.4 `bookings`, `booking_nights`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | `bigint` PK | |
| `code` | `char(12)` **ux** | `HS-XXXX-XXXX` (Crockford base32 ngẫu nhiên, bỏ chữ dễ nhầm) |
| `homestay_id`, `room_id` | FK RESTRICT | |
| `user_id` | FK null | khách có tài khoản; null = đặt hộ |
| `guest_name`, `guest_phone`, `guest_email` | `varchar(100)`, `varchar(15)`, `varchar(254)` null | `ix (guest_phone)` |
| `guests`, `nights` | `tinyint` | |
| `check_in`, `check_out` | `date` | CHECK `check_out > check_in` |
| `room_total`, `extra_guest_total`, `cleaning_fee`, `total`, `deposit_amount`, `refund_amount` | `decimal(15,2)` | ảnh chụp lúc đặt |
| `status` | `varchar(12)` | `pending · confirmed · checked_in · checked_out · cancelled · expired · no_show` |
| `source` | `varchar(10)` | `web · phone · walk_in · ota` |
| `guest_note`, `internal_note`, `cancel_reason` | `varchar(500)` | |
| `hold_until` | `datetime(6)` null | `ix (status, hold_until)` cho dịch vụ hết hạn |
| `confirmed_at`, `checked_in_at`, `checked_out_at`, `cancelled_at` | `datetime(6)` null | |
| `created_by` | FK `users` null | nhân viên tạo hộ |
| `created_at`, `updated_at` | `datetime(6)` | `ix (room_id, check_in)`, `ix (homestay_id, check_in)`, `ix (user_id, check_in)` |

`booking_nights`: PK (`booking_id`, `night`), `price` – giữ lại cả khi huỷ (báo cáo, lịch sử).

### 4.5 `room_nights` – khoá chống đặt trùng

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `room_id` | FK | **PK (`room_id`, `night`)** ⇒ 1 phòng 1 đêm chỉ 1 dòng |
| `night` | `date` | |
| `booking_id` | FK null, RESTRICT | |
| `block_id` | FK null, RESTRICT | CHECK đúng 1 trong 2 cột khác null |

Chỉ chứa đêm của đặt phòng **đang hiệu lực** (`pending`, `confirmed`, `checked_in`) và khoá bảo trì. Huỷ / hết hạn → xoá dòng.

```sql
-- Phòng trống trong [check_in, check_out)
SELECT r.* FROM rooms r
WHERE r.status = 'active' AND r.max_guests >= @guests
  AND NOT EXISTS (SELECT 1 FROM room_nights n
                  WHERE n.room_id = r.id AND n.night >= @check_in AND n.night < @check_out);
```

### 4.6 `room_blocks`, `payments`, `booking_events`, `reviews`, `email_outbox`

| Bảng | Cột chính |
|---|---|
| `room_blocks` | `id`, `room_id`, `from_date`, `to_date` (gồm 2 đầu), `reason`, `created_by`, `created_at` |
| `payments` | `id`, `booking_id` FK, `kind` (`deposit · balance · extra · refund`), `method` (`cash · bank_transfer · card · ewallet`), `amount` CHECK `> 0`, `paid_at`, `note`, `recorded_by` FK users, `created_at` |
| `booking_events` | `id`, `booking_id` FK, `type`, `from_status`, `to_status`, `message`, `actor_id` (null = hệ thống), `created_at` – `ix (booking_id, id)` |
| `reviews` | `id`, `booking_id` **ux**, `homestay_id`, `user_id`, `rating` CHECK 1–5, `comment`, `is_hidden`, `created_at` – `ix (homestay_id, is_hidden)` |
| `email_outbox` | `id`, `to_email`, `subject`, `html_body`, `status` (`pending · sent · failed`), `attempts`, `last_error`, `created_at`, `sent_at` |

## 5. Biến môi trường `.env`

`backend/.env` (không commit – mẫu ở `backend/.env.example`):

```dotenv
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://0.0.0.0:5100
ConnectionStrings__Default=Server=localhost;Port=3306;Database=homestay;User=homestay;Password=<bí mật>;CharSet=utf8mb4
Jwt__Secret=<chuỗi ngẫu nhiên ≥ 32 ký tự>
Seed__AdminEmail=admin@homestay.local
Seed__AdminPassword=<mật khẩu admin đầu tiên>
App__TimeZone=Asia/Ho_Chi_Minh
```

## 6. Sao lưu

```bash
docker exec mysql-dev sh -c 'mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" --single-transaction --routines homestay' > homestay-$(date +%F).sql
```

Kèm thư mục ảnh `backend/storage/uploads/` (bản publish: `<thư mục publish>\storage\uploads\`) – ảnh không nằm trong DB.
