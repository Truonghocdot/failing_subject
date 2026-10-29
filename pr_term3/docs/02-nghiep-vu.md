# 02 – Quy tắc nghiệp vụ

Mọi quy tắc dưới đây được kiểm tra ở **backend** (service); FE chỉ kiểm tra sớm để báo lỗi nhanh. Thông báo lỗi bằng tiếng Việt.
"Hôm nay" luôn tính theo múi giờ `App__TimeZone` (mặc định `Asia/Ho_Chi_Minh`), không theo UTC.

## 1. Vai trò & quyền

| Việc | Khách (`guest`) | Lễ tân (`staff`) | Quản trị (`admin`) |
|---|:-:|:-:|:-:|
| Tìm, xem homestay, báo giá | ✅ (cả khi chưa đăng nhập) | ✅ | ✅ |
| Đặt phòng online, xem / huỷ đặt phòng **của mình**, đánh giá | ✅ | – | – |
| Xem lịch phòng, mọi đặt phòng, tạo đặt phòng hộ khách | – | ✅ | ✅ |
| Xác nhận, check-in, check-out, no-show, huỷ, đổi ngày / phòng | – | ✅ | ✅ |
| Ghi thu tiền, hoàn tiền, khoá phòng (bảo trì) | – | ✅ | ✅ |
| Quản lý homestay, phòng, ảnh, giá, chính sách | – | – | ✅ |
| Quản lý tài khoản nhân viên, ẩn đánh giá, xem báo cáo doanh thu | – | – | ✅ |

- Khách truy cập đặt phòng của người khác → `404` (không lộ là có tồn tại).
- Khách không có tài khoản (đặt qua điện thoại) tra cứu bằng **mã đặt phòng + số điện thoại** (giới hạn 10 lần/phút/IP).
- Admin không tự hạ quyền / khoá chính mình; hệ thống luôn còn ít nhất 1 admin hoạt động.
- Khoá tài khoản / đổi vai trò / đặt lại mật khẩu → đăng xuất mọi thiết bị của người đó; token đang dùng bị từ chối ngay ở request kế tiếp.
- Tài khoản bị khoá đăng nhập đúng mật khẩu → `403` "Tài khoản đã bị khoá – liên hệ quản trị viên"; sai mật khẩu vẫn báo "Email hoặc mật khẩu không đúng".

## 2. Homestay

| Trường | Quy tắc |
|---|---|
| `name` | 1 – 120 ký tự |
| `slug` | URL (`da-lat-pine-house`), duy nhất, `a-z0-9-`, tự sinh từ tên (bỏ dấu) nếu để trống |
| `province`, `address` | Tỉnh / thành bắt buộc (dùng để tìm kiếm), địa chỉ 1 – 255 ký tự |
| `description` | ≤ 5 000 ký tự (xuống dòng giữ nguyên) |
| `amenities` | Chọn từ danh mục cố định: `wifi, parking, kitchen, aircon, hot_water, washer, tv, pool, bbq, garden, balcony, mountain_view, sea_view, breakfast, pet_friendly, workspace` |
| `checkInTime` / `checkOutTime` | Mặc định `14:00` / `12:00` |
| `depositPercent` | 0 – 100 (mặc định **30**); 0 = không cần cọc → đặt online được xác nhận ngay |
| `holdHours` | Giờ giữ chỗ chờ cọc, 1 – 72 (mặc định **24**) |
| Chính sách huỷ | `freeCancelDays` (7), `partialRefundDays` (3), `partialRefundPercent` (50) – xem §7 |
| `status` | `active` (hiện cho khách) · `hidden` (chỉ quản trị thấy, không nhận đặt mới) |
| Ảnh | jpg / png / webp ≤ 5 MB, tối đa 20 ảnh / homestay, 1 ảnh bìa, sắp xếp được |

Xoá homestay: chỉ khi chưa có đặt phòng nào; đã có → `409` "Homestay đã có đặt phòng – hãy ẩn thay vì xoá".

## 3. Phòng

| Trường | Quy tắc |
|---|---|
| `name` | 1 – 80 ký tự, duy nhất trong homestay (VD "Phòng Đồi Thông", "Nguyên căn") |
| `baseGuests` / `maxGuests` | 1 ≤ base ≤ max ≤ 20 |
| `basePrice` | Giá 1 đêm, > 0, số nguyên VND |
| `weekendPrice` | Tuỳ chọn – áp cho **đêm thứ Sáu và thứ Bảy** |
| `extraGuestFee` | Phụ thu mỗi khách vượt `baseGuests` mỗi đêm (≥ 0) |
| `cleaningFee` | Phí dọn mỗi lượt đặt (≥ 0) |
| `minNights` | 1 – 30 (mặc định 1) |
| `bedInfo`, `areaM2`, `description` | Hiển thị |
| `status` | `active` · `inactive` (không nhận đặt mới; đặt phòng cũ giữ nguyên) |

Xoá phòng: chỉ khi chưa từng có đặt phòng / khoá phòng; ngược lại → ngưng hoạt động.

## 4. Giá

Giá của **một đêm** (đêm = ngày nhận phòng tới sáng hôm sau) theo thứ tự ưu tiên:

1. **Giá riêng theo ngày** (`rate_overrides`) – quản trị đặt cho khoảng ngày (lễ, Tết, mùa cao điểm), chọn nhiều phòng một lần; xoá được.
2. **Giá cuối tuần** nếu đêm đó là thứ Sáu / thứ Bảy và phòng có `weekendPrice`.
3. **Giá cơ bản** `basePrice`.

Báo giá (`quote`) cho phòng, ngày nhận `checkIn`, ngày trả `checkOut`, số khách `guests`:

```
nights          = checkOut − checkIn
roomTotal       = Σ giá từng đêm
extraGuestTotal = max(0, guests − baseGuests) × extraGuestFee × nights
total           = roomTotal + extraGuestTotal + cleaningFee
deposit         = làm tròn lên hàng nghìn( total × depositPercent / 100 )
```

- Kết quả trả **từng đêm** (`[{date, price, source: override|weekend|base}]`) để khách thấy rõ vì sao giá khác nhau.
- Đặt phòng lưu **ảnh chụp giá** lúc đặt (`booking_nights.price`, các tổng) → sửa giá sau đó không ảnh hưởng đặt phòng cũ.
- Khi đặt, backend tính lại giá; nếu khác báo giá khách đã xem → `409` "Giá đã thay đổi" kèm báo giá mới (khách xác nhận lại).

## 5. Tìm kiếm & tình trạng phòng

| Tham số | Quy tắc |
|---|---|
| `checkIn`, `checkOut` | `checkIn ≥ hôm nay`, `checkIn ≤ hôm nay + 365`, `1 ≤ đêm ≤ 30` |
| `guests` | 1 – 20 |
| `province` / `q` | Lọc tỉnh; `q` tìm trong tên homestay |
| `amenities`, `priceMax` | Lọc thêm (tuỳ chọn) |

Phòng **còn trống** khi: phòng `active`, homestay `active`, `maxGuests ≥ guests`, `đêm ≥ minNights`,
và **không có** dòng `room_nights` nào trong `[checkIn, checkOut)`.

Kết quả gom theo homestay: ảnh bìa, điểm đánh giá, số phòng còn trống, **"giá từ"** = tổng thấp nhất (gồm phụ thu khách thêm, phí dọn) / số đêm
(không chọn ngày → giá cơ bản thấp nhất / đêm, không tính phòng trống). Homestay không còn phòng nào phù hợp bị loại khỏi kết quả.
Sắp xếp: phù hợp (mặc định – tên bắt đầu bằng từ khoá, nhiều phòng trống, nhiều đánh giá) · giá tăng · đánh giá.
Trang homestay có **lịch 2 tháng** cho từng phòng: ngày còn trống / đã kín và giá mỗi đêm.

## 6. Đặt phòng

### 6.1 Thông tin

`code` (mã cho khách, VD `HS-7KQ2-M9XA`, ngẫu nhiên – không đoán được), homestay, phòng, `userId` (nếu khách có tài khoản),
`guestName`, `guestPhone` (bắt buộc, chuẩn hoá về dạng bắt đầu bằng `0`: di động 10 số, cố định 11 số; `+84 912 345 678` → `0912345678`), `guestEmail`, `guests`, `checkIn`, `checkOut`, các tổng tiền (§4),
`depositAmount`, `status`, `source` (`web` · `phone` · `walk_in` · `ota`), `guestNote` (≤ 500), `internalNote` (chỉ nhân viên thấy),
`holdUntil`, các mốc thời gian (`confirmedAt`, `checkedInAt`, `checkedOutAt`, `cancelledAt`), `cancelReason`, `refundAmount`.

### 6.2 Khoá đêm (chống đặt trùng)

- Mỗi đặt phòng đang hiệu lực giữ 1 dòng `room_nights(room_id, night)` cho **mỗi đêm**; khoá phòng (bảo trì) cũng vậy.
- `room_nights` có **unique (room_id, night)** → 2 yêu cầu đặt cùng phòng cùng đêm gửi cùng lúc: DB chỉ cho 1 cái thành công,
  cái còn lại nhận `409` "Phòng vừa có người đặt trong khoảng ngày này – hãy chọn ngày hoặc phòng khác".
- Tạo đặt phòng + khoá đêm trong **1 transaction**. Huỷ / hết hạn / no-show → xoá các dòng khoá tương ứng trong cùng transaction.
- Trạng thái có khoá đêm: `pending`, `confirmed`, `checked_in`. Các trạng thái còn lại không giữ đêm.

### 6.3 Vòng đời

```
                 cọc đủ / không cần cọc
  (đặt online) pending ──────────────────▶ confirmed ──check-in──▶ checked_in ──check-out──▶ checked_out
       │  │                                  │   │                                     (đánh giá được)
       │  └─ quá holdUntil ─▶ expired        │   └─ quá ngày nhận, khách không đến ─▶ no_show
       └──────── huỷ ───────────▶ cancelled ◀┘ huỷ
```

| Chuyển | Ai | Điều kiện |
|---|---|---|
| tạo → `pending` | Khách (web), nhân viên | Còn trống, giá hợp lệ; `holdUntil = now + holdHours` |
| tạo → `confirmed` | Nhân viên | Khi tạo hộ và đã thu cọc (ghi kèm khoản thu), hoặc `depositPercent = 0` |
| `pending → confirmed` | Nhân viên (hoặc tự động) | Tổng đã thu ≥ `depositAmount` – tự chuyển khi ghi khoản thu; nhân viên cũng xác nhận tay được (ghi lý do) |
| `pending → expired` | Hệ thống | `now > holdUntil` (dịch vụ nền chạy mỗi phút) |
| `→ cancelled` | Khách (đặt của mình), nhân viên | Từ `pending` / `confirmed`, **trước ngày nhận phòng** (nhân viên: cả ngày nhận phòng); tính hoàn tiền §7 |
| `confirmed → checked_in` | Nhân viên | `hôm nay ≥ checkIn` và `hôm nay < checkOut` |
| `checked_in → checked_out` | Nhân viên | Đã thu đủ (`còn phải thu = 0`) hoặc nhân viên ghi lý do bỏ qua; trả phòng sớm → nhả các đêm còn lại |
| `confirmed → no_show` | Nhân viên | `hôm nay > checkIn`; giữ tiền cọc, nhả đêm |

- Rời trạng thái giữ đêm (`cancelled`, `expired`, `no_show`, `checked_out`) → xoá **mọi** dòng `room_nights` của đặt phòng (đêm đã qua không ai đặt được nữa).
  Trả phòng sớm: đêm còn lại trống ngay; lịch phòng vẽ thanh tới ngày thực trả; bảng điều khiển tính vào "khách đi hôm nay".
- Tổng tiền không tự đổi khi trả sớm / no-show – nhân viên ghi khoản hoàn nếu đồng ý trả lại.

Đổi ngày / đổi phòng (nhân viên, khi `pending` / `confirmed`): khoá đêm mới + nhả đêm cũ trong 1 transaction (trùng → `409`, đặt phòng giữ nguyên); tính lại giá
(hoặc "giữ giá cũ" – chỉ khi giữ nguyên số đêm, bắt buộc lý do). Mọi thay đổi trạng thái, đổi ngày, thu / hoàn tiền ghi vào **nhật ký đặt phòng** (`booking_events`: ai, lúc nào, nội dung).

## 7. Cọc, huỷ & hoàn tiền

Chính sách theo homestay; `daysBefore = checkIn − hôm nay`:

| Huỷ khi | Hoàn lại (trên số khách đã trả) |
|---|---|
| `daysBefore ≥ freeCancelDays` (mặc định 7) | 100% |
| `partialRefundDays ≤ daysBefore < freeCancelDays` (3 – 6 ngày) | `partialRefundPercent` (50%) |
| `daysBefore < partialRefundDays` | 0% |
| Đặt `pending` chưa trả gì | Huỷ miễn phí |

- Khách xem trước số tiền được hoàn trước khi bấm huỷ. Nhân viên có thể sửa số hoàn (≤ số đã trả, ghi lý do – VD thiên tai).
- Số hoàn làm tròn tới đồng (1 155 000 × 50% = 577 500). Chính sách lấy theo homestay **lúc huỷ**.
- Số hoàn được ghi là **khoản hoàn** (`refund`) khi nhân viên thực hiện chuyển trả; trước đó đặt phòng hiện "chờ hoàn X đ".

## 8. Thanh toán (ghi nhận)

| Trường | Quy tắc |
|---|---|
| `kind` | `deposit` (cọc) · `balance` (phần còn lại) · `extra` (phụ thu phát sinh: minibar, thêm khách…) · `refund` (hoàn) |
| `method` | `cash` · `bank_transfer` · `card` · `ewallet` |
| `amount` | > 0, số nguyên VND; hoàn không vượt tổng đã thu |
| `paidAt`, `note`, `recordedBy` | Người ghi = nhân viên đang đăng nhập |

```
paid        = Σ (deposit + balance + extra) − Σ refund
amountDue   = total + Σ extra − paid          (còn phải thu)
paymentStatus: unpaid (paid = 0) · deposit_paid (0 < paid < total) · paid (paid ≥ total) · refunded (đã huỷ và hoàn hết)
```

Ghi nhầm → xoá khoản thu trong vòng 24 giờ (admin: bất kỳ lúc nào); có ghi nhật ký. Xoá không tự lùi trạng thái (VD đã `confirmed` vẫn giữ) –
nhân viên xem nhật ký và xử lý. Không xoá được khoản thu nếu sau khi xoá số đã hoàn lớn hơn số đã thu (xoá khoản hoàn trước).

- Đặt phòng `cancelled` / `expired` / `no_show` chỉ nhận khoản **hoàn** (khách chuyển cọc muộn sau khi hết hạn → tạo đặt phòng mới hoặc hoàn lại).
- `paidAt` không được ở tương lai; bỏ trống = lúc ghi.

## 9. Khoá phòng (bảo trì / chủ dùng)

Phòng, `from`, `to` (bao gồm 2 đầu), lý do. Không được trùng đêm đã có đặt phòng → `409` liệt kê mã đặt phòng bị trùng. Hiện trên lịch phòng màu xám.

## 10. Đánh giá

- Chỉ khách sở hữu đặt phòng `checked_out`, trong vòng 30 ngày sau trả phòng, **1 lần / đặt phòng**.
- `rating` 1 – 5, `comment` ≤ 1 000 ký tự. Admin ẩn được (spam), không sửa nội dung.
- Homestay hiện điểm trung bình (1 chữ số thập phân) và số đánh giá (chỉ tính đánh giá đang hiện).

## 11. Báo cáo (quản trị)

Theo khoảng ngày (≤ 366 ngày), lọc homestay:

| Chỉ số | Công thức |
|---|---|
| Đêm có thể bán | Σ phòng `active` × số ngày − đêm bị khoá bảo trì |
| Đêm đã bán | Số `booking_nights` thuộc đặt phòng `confirmed / checked_in / checked_out` trong khoảng |
| **Công suất phòng** (occupancy) | Đêm đã bán / đêm có thể bán |
| Doanh thu phòng (theo đêm ở) | Σ giá các đêm đã bán (+ phụ thu khách thêm chia theo đêm) |
| **ADR** | Doanh thu phòng / đêm đã bán |
| **RevPAR** | Doanh thu phòng / đêm có thể bán |
| Tiền thu thực tế | Σ khoản thu − Σ hoàn theo `paidAt` |
| Theo nguồn | Số đặt phòng, doanh thu theo `source` |
| Tỉ lệ huỷ | Đặt phòng `cancelled + no_show` / tổng đặt phòng tạo trong khoảng |

Bảng điều khiển hằng ngày: khách đến hôm nay, khách đi hôm nay, đang ở, đặt phòng `pending` sắp hết hạn giữ chỗ, công suất 7 ngày tới.

## 12. Thông báo email

Gửi khi: đặt phòng tạo (`pending` – kèm hướng dẫn chuyển khoản), xác nhận, huỷ (kèm số hoàn), nhắc trước ngày nhận phòng 1 ngày.
Ghi vào hàng đợi `email_outbox`, dịch vụ nền gửi qua SMTP; dev không cấu hình SMTP → ghi file `.eml` vào `storage/mail/` để xem.

## 13. Bảo mật dữ liệu

- Mọi truy vấn của khách lọc theo `user_id` từ JWT; endpoint `/api/admin/*` yêu cầu vai trò `staff` / `admin`.
- Số điện thoại, email khách chỉ nhân viên thấy; trang công khai chỉ hiện tên viết tắt người đánh giá ("Minh T.").
- Ảnh upload: kiểm tra định dạng thật (magic bytes), đổi tên ngẫu nhiên, không chạy được như script.
