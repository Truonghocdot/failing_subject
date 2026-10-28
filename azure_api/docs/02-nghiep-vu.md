# 02 – Quy tắc nghiệp vụ

Tài liệu này là "nguồn sự thật" cho cả backend (validate + tính toán) và frontend (form + hiển thị).
Mã lỗi/thông báo tiếng Việt ở đây được dùng nguyên văn trong `ValidationProblem` của backend.

## 1. Người dùng

| Trường | Quy tắc |
|---|---|
| Email | Bắt buộc, đúng định dạng, ≤ 254 ký tự, **không phân biệt hoa thường** (lưu dạng `lower(trim)`), duy nhất |
| Mật khẩu | 8 – 100 ký tự |
| Tên hiển thị | Bắt buộc, `trim`, 1 – 100 ký tự |
| Tiền tệ | Mã ISO 4217 3 chữ in hoa, mặc định `VND`; chỉ ảnh hưởng **cách hiển thị** (không quy đổi) |

Khi **đăng ký thành công**, hệ thống tạo sẵn trong cùng transaction:
- 1 ví **"Tiền mặt"** (loại `cash`, số dư đầu 0).
- Bộ danh mục mặc định (§3.2).

## 2. Ví (wallet)

| Trường | Quy tắc |
|---|---|
| `name` | Bắt buộc, 1 – 50 ký tự, **duy nhất trong các ví của user** (không phân biệt hoa thường) |
| `type` | `cash` (tiền mặt) · `bank` (ngân hàng) · `ewallet` (ví điện tử) · `credit` (thẻ tín dụng) |
| `openingBalance` | Số dư lúc bắt đầu dùng app, `-9 999 999 999 999.99 … 9 999 999 999 999.99`, mặc định 0 (thẻ tín dụng có thể âm) |
| `color` | Mã hex `#RRGGBB`, mặc định theo loại |
| `isArchived` | Ví đã lưu trữ: ẩn khỏi ô chọn khi tạo giao dịch, **vẫn tính** trong báo cáo / lịch sử |

**Số dư hiện tại** (luôn tính, không lưu):

```
balance = openingBalance
        + Σ amount (kind = income,   wallet_id    = ví)
        − Σ amount (kind = expense,  wallet_id    = ví)
        − Σ amount (kind = transfer, wallet_id    = ví)      -- chuyển đi
        + Σ amount (kind = transfer, to_wallet_id = ví)      -- nhận về
```

**Tổng tài sản** = Σ balance của các ví chưa lưu trữ (`GET /api/wallets` trả kèm `total`).

Xoá ví:
- Chưa có giao dịch nào (kể cả nhận chuyển khoản) → xoá hẳn.
- Đã có giao dịch → `409 Conflict` "Ví đã có giao dịch – hãy lưu trữ thay vì xoá".
- Không được lưu trữ / xoá **ví cuối cùng còn hoạt động** → `409`.

## 3. Danh mục (category)

### 3.1 Quy tắc

| Trường | Quy tắc |
|---|---|
| `kind` | `expense` (chi) hoặc `income` (thu) – **không đổi được** sau khi tạo |
| `name` | 1 – 50 ký tự, duy nhất trong cùng `kind` của user (không phân biệt hoa thường) |
| `icon` | Tên icon lucide (VD `utensils`), ≤ 40 ký tự; FE có danh sách chọn sẵn |
| `color` | `#RRGGBB` |
| `isArchived` | Ẩn khỏi ô chọn, vẫn hiện trong báo cáo |

Xoá danh mục: chưa dùng (không có giao dịch, không có ngân sách) → xoá; đã dùng → `409` "Danh mục đang được dùng – hãy lưu trữ".

### 3.2 Bộ mặc định (seed khi đăng ký)

| Chi (`expense`) | icon | Thu (`income`) | icon |
|---|---|---|---|
| Ăn uống | `utensils` | Lương | `banknote` |
| Đi lại | `bus` | Thưởng | `gift` |
| Nhà ở | `house` | Kinh doanh | `store` |
| Hoá đơn & tiện ích | `receipt` | Đầu tư | `trending-up` |
| Mua sắm | `shopping-bag` | Được cho / tặng | `hand-heart` |
| Giải trí | `clapperboard` | Thu khác | `circle-plus` |
| Sức khoẻ | `heart-pulse` | | |
| Giáo dục | `graduation-cap` | | |
| Gia đình & con cái | `baby` | | |
| Chi khác | `circle-ellipsis` | | |

## 4. Giao dịch (transaction)

| Trường | `expense` / `income` | `transfer` |
|---|---|---|
| `walletId` | Bắt buộc – ví của user, **chưa lưu trữ** (khi tạo / đổi ví) | Ví nguồn, như bên trái |
| `toWalletId` | Phải để trống | Bắt buộc, ví của user, chưa lưu trữ, **≠ walletId** |
| `categoryId` | Bắt buộc, cùng `kind` với giao dịch | Phải để trống |
| `amount` | `> 0` và `≤ 9 999 999 999 999.99`, tối đa 2 chữ số thập phân | như bên trái |
| `occurredOn` | Ngày (không giờ) `yyyy-MM-dd`, từ `2000-01-01` đến **hôm nay + 1 năm** | như bên trái |
| `note` | Tuỳ chọn, `trim`, ≤ 500 ký tự | như bên trái |

- `amount` luôn **dương**; dấu (+/−) suy ra từ `kind`. Không có giao dịch số tiền âm.
- Sửa giao dịch được đổi mọi trường **kể cả `kind`** (áp lại toàn bộ quy tắc trên).
- Giao dịch cũ trỏ tới ví / danh mục đã lưu trữ vẫn sửa được ghi chú / số tiền / ngày, miễn là **không đổi sang** một ví lưu trữ khác.
- Sắp xếp mặc định: `occurredOn` giảm dần, rồi `id` giảm dần (cái nhập sau đứng trên).

### 4.1 Lọc & tìm kiếm (`GET /api/transactions`)

| Tham số | Ý nghĩa |
|---|---|
| `from`, `to` | Khoảng ngày (bao gồm 2 đầu); mặc định **tháng hiện tại**; `to − from ≤ 366 ngày` |
| `walletId` | Giao dịch có `wallet_id` **hoặc** `to_wallet_id` = ví này |
| `categoryId` | Theo danh mục |
| `kind` | `expense` · `income` · `transfer` |
| `q` | Tìm trong `note` (LIKE, không phân biệt hoa thường), 1 – 100 ký tự |
| `page`, `pageSize` | `page ≥ 1`, `pageSize` 1 – 100 (mặc định 20) |

Response kèm `totals { income, expense }` của **toàn bộ** kết quả lọc (không chỉ trang hiện tại). Chuyển tiền không tính vào thu/chi.

## 5. Ngân sách (budget)

- Đặt theo **(tháng, danh mục chi)**: `month` dạng `yyyy-MM`, `amount > 0`.
- Mỗi (user, tháng, danh mục) tối đa 1 ngân sách → `PUT` là **upsert**.
- `spent` = Σ `amount` của giao dịch `expense` thuộc danh mục đó có `occurredOn` trong tháng.
- `percent = spent / amount × 100` (làm tròn 1 chữ số).

| Trạng thái | Điều kiện | Màu FE |
|---|---|---|
| `ok` | `percent < 80` | xanh |
| `warning` | `80 ≤ percent ≤ 100` | vàng |
| `over` | `percent > 100` | đỏ |

- **Sao chép tháng trước**: `POST /api/budgets/copy {from: "2026-09", to: "2026-10"}` – chỉ thêm các danh mục tháng đích **chưa có** ngân sách.
- Dashboard hiện các ngân sách tháng hiện tại, sắp xếp `percent` giảm dần.

## 6. Báo cáo

Mọi báo cáo nhận `from`, `to` (bao gồm 2 đầu, tối đa 366 ngày). **Chuyển tiền không được tính** vào thu / chi.

| Báo cáo | Kết quả |
|---|---|
| Tổng quan (`summary`) | `income`, `expense`, `net = income − expense`, `transactionCount` (thu + chi), `dailyAverageExpense = expense / số ngày trong khoảng` – khoảng chứa hôm nay chỉ tính tới hôm nay |
| Theo danh mục (`by-category`, `kind`) | Mỗi danh mục: `total`, `percent` (trên tổng của `kind`), `count`; sắp xếp `total` giảm dần |
| Xu hướng (`trend`, `groupBy=day|month`) | Mỗi kỳ: `income`, `expense` – **đủ mọi kỳ** trong khoảng, kỳ không có giao dịch trả 0 |

## 7. Bảo mật dữ liệu

- Mọi truy vấn đều lọc theo `user_id` lấy từ JWT – không bao giờ nhận `userId` từ client.
- Truy cập id của user khác → **`404`** (không trả 403, tránh lộ id tồn tại).
- Xuất CSV: UTF-8 có BOM (Excel đọc đúng tiếng Việt), cột `Ngày, Loại, Ví, Ví nhận, Danh mục, Số tiền, Ghi chú`;
  ô bắt đầu bằng `= + - @` được thêm `'` phía trước để chống CSV injection.
