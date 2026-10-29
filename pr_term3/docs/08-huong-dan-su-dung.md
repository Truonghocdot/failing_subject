# 08 – Hướng dẫn sử dụng: hệ thống có những tính năng gì

Tài liệu dành cho **người dùng** (khách đặt phòng, lễ tân, chủ homestay). Không cần biết kỹ thuật.
Quy tắc chi tiết (công thức giá, điều kiện từng trạng thái) nằm ở [02 – Quy tắc nghiệp vụ](02-nghiep-vu.md).

**Homestay Booking** là website cho **một đơn vị kinh doanh có nhiều homestay**, mỗi homestay có một hoặc nhiều phòng
(nhà nguyên căn được tính là 1 phòng). Website có 2 khu:

| Khu | Địa chỉ | Ai dùng | Để làm gì |
|---|---|---|---|
| **Khu khách** | `http://<máy chủ>:5100/` | Mọi người (khách vãng lai, khách có tài khoản) | Tìm homestay, xem giá, đặt phòng, theo dõi / huỷ đặt phòng, đánh giá |
| **Khu quản trị** | `http://<máy chủ>:5100/admin` | Lễ tân, Quản trị (chủ) | Vận hành đặt phòng trên lịch, thu tiền, quản lý homestay, giá, nhân viên, báo cáo |

---

## 1. Ai được làm gì

| Việc | Khách chưa đăng nhập | Khách có tài khoản | Lễ tân | Quản trị |
|---|:-:|:-:|:-:|:-:|
| Tìm homestay, xem ảnh, tiện nghi, lịch trống, giá từng đêm | ✅ | ✅ | ✅ | ✅ |
| Tra cứu đặt phòng bằng mã + số điện thoại (chỉ xem) | ✅ | ✅ | ✅ | ✅ |
| Đặt phòng online, xem / huỷ đặt phòng của mình, đánh giá | – | ✅ | – | – |
| Lịch phòng, danh sách đặt phòng, tạo đặt phòng hộ khách | – | – | ✅ | ✅ |
| Xác nhận cọc, check-in, check-out, khách không đến, huỷ, đổi ngày / phòng | – | – | ✅ | ✅ |
| Ghi khoản thu / hoàn tiền, khoá phòng bảo trì, xem danh sách khách | – | – | ✅ | ✅ |
| Thêm / sửa homestay, phòng, ảnh, chính sách cọc – huỷ | – | – | – | ✅ |
| Đặt giá theo ngày (lễ, Tết, mùa cao điểm) | – | – | – | ✅ |
| Ẩn đánh giá, xem báo cáo doanh thu, quản lý nhân viên | – | – | – | ✅ |

Lễ tân không thấy các mục chỉ dành cho Quản trị trong menu; gõ thẳng địa chỉ thì sẽ thấy trang "Không có quyền".

---

## 2. Dành cho khách

### 2.1 Tìm homestay

- **Trang chủ**: nhập nơi đến (tỉnh / thành), ngày nhận – trả phòng, số khách, rồi bấm tìm. Bên dưới có homestay nổi bật và các tỉnh phổ biến.
- **Trang kết quả** (menu **Tìm homestay**):
  - Lọc theo **tiện nghi** (Wi-Fi, bếp, hồ bơi, view biển, cho mang thú cưng…) và **giá tối đa**.
  - Sắp xếp theo: phù hợp nhất, giá tăng dần, điểm đánh giá.
  - Mỗi homestay hiện ảnh bìa, điểm đánh giá, số phòng còn trống và **"giá từ … / đêm"** (đã gồm phí dọn và phụ thu khách thêm, nếu có).
  - Chỉ hiện homestay còn ít nhất 1 phòng trống cho đúng ngày và số khách đã chọn.
- Bộ lọc nằm trên địa chỉ trang, nên có thể **gửi link cho bạn bè** hoặc tải lại trang mà không mất lựa chọn.

### 2.2 Xem chi tiết homestay

- Thư viện ảnh, mô tả, tiện nghi, giờ nhận / trả phòng (mặc định 14:00 / 12:00), **chính sách huỷ** viết thành câu dễ hiểu.
- Danh sách phòng: sức chứa, giường, diện tích, giá cơ bản.
- **Lịch tháng của từng phòng**: ngày còn trống / đã kín, kèm giá mỗi đêm. Bấm ngày nhận rồi bấm ngày trả để chọn khoảng ngày.
- **Khung đặt phòng** (trên điện thoại là thanh tổng tiền dính ở đáy màn hình):
  - Hiện **giá từng đêm**, ghi rõ đêm nào là giá cuối tuần hay giá dịp đặc biệt.
  - Hiện phụ thu khách thêm, phí dọn, tổng tiền và **số tiền cọc**.
- Đánh giá của khách đã ở: điểm trung bình, nội dung. Tên người đánh giá được viết tắt (VD "Minh T.").

### 2.3 Đặt phòng online

1. Ở khung đặt phòng, bấm **Đặt phòng**. Nếu chưa đăng nhập, hệ thống yêu cầu đăng nhập hoặc **đăng ký** (chỉ cần email, họ tên, số điện thoại, mật khẩu), rồi quay lại đúng trang đặt.
2. Kiểm tra thông tin khách (điền sẵn từ tài khoản, có thể đặt hộ người khác), ghi chú cho chủ nhà (VD "đến muộn khoảng 20:00"), đồng ý chính sách rồi xác nhận.
3. Bạn nhận **mã đặt phòng** dạng `HS-7KQ2-M9XA`. Trang chi tiết hiện:
   - **Hướng dẫn chuyển khoản cọc**: ngân hàng, số tài khoản, chủ tài khoản, số tiền. **Nội dung chuyển khoản là mã đặt phòng.**
   - **Đồng hồ đếm ngược hạn giữ chỗ** (mặc định 24 giờ). Phòng được giữ riêng cho bạn trong thời gian này.
4. Lễ tân thấy tiền về thì xác nhận. Đặt phòng chuyển sang **Đã xác nhận** và bạn nhận email xác nhận.
   Homestay không yêu cầu cọc thì đặt phòng được xác nhận ngay.

Lưu ý:
- **Không có chuyện 2 người đặt trùng 1 phòng.** Nếu ai đó vừa đặt trước bạn vài giây, hệ thống báo phòng vừa có người đặt và bạn chọn ngày hoặc phòng khác.
- Nếu giá vừa thay đổi trong lúc bạn đang điền thông tin, hệ thống báo **giá mới** và giữ nguyên thông tin đã nhập để bạn xác nhận lại.
- Quá hạn giữ chỗ mà chưa cọc, đặt phòng tự chuyển sang **Hết hạn giữ chỗ** và phòng được mở cho người khác.
- Giá đã chốt lúc đặt **không bị ảnh hưởng** nếu sau đó homestay đổi giá.

### 2.4 Theo dõi đặt phòng của tôi

Menu **Đặt phòng của tôi** chia 3 tab: **Sắp tới**, **Đã ở**, **Đã huỷ**. Bấm vào một đặt phòng để xem:
- Trạng thái và tình trạng thanh toán (Chưa thanh toán, Đã cọc, Đã thanh toán đủ, Đã hoàn tiền).
- Số ngày còn lại tới ngày nhận phòng; số tiền đã thanh toán.
- Hướng dẫn chuyển khoản, nếu đang chờ cọc.

Trạng thái đặt phòng (luôn có chữ đi kèm màu):

| Trạng thái | Nghĩa |
|---|---|
| 🟡 **Chờ cọc** | Đã giữ chỗ, đang chờ bạn chuyển cọc |
| 🔵 **Đã xác nhận** | Homestay đã nhận cọc, phòng chắc chắn là của bạn |
| 🟢 **Đang ở** | Đã nhận phòng |
| ⚪ **Đã trả phòng** | Đã trả phòng, có thể đánh giá |
| 🔴 **Đã huỷ** / **Hết hạn giữ chỗ** / **Không đến** | Đặt phòng không còn hiệu lực |

### 2.5 Huỷ đặt phòng

Bấm **Huỷ đặt phòng** ở trang chi tiết (chỉ được huỷ **trước ngày nhận phòng**). Trước khi xác nhận, hệ thống hiện **số tiền sẽ được hoàn**.
Chính sách mặc định (mỗi homestay có thể khác, xem ở trang homestay):

| Huỷ trước ngày nhận phòng | Hoàn lại trên số tiền đã trả |
|---|---|
| Từ 7 ngày trở lên | 100% |
| Từ 3 đến 6 ngày | 50% |
| Dưới 3 ngày | Không hoàn |
| Chưa chuyển cọc | Huỷ miễn phí |

Tiền hoàn do homestay chuyển trả. Trong lúc chờ, đặt phòng hiện "chờ hoàn X đ".

### 2.6 Tra cứu đặt phòng không cần tài khoản

Khách đặt qua điện thoại hoặc đến trực tiếp (lễ tân tạo hộ) vào menu **Tra cứu đặt phòng**, nhập **mã đặt phòng + số điện thoại** đã dùng khi đặt.
Kết quả chỉ để xem; muốn huỷ thì liên hệ homestay. Để chống dò mã, mỗi máy chỉ tra được 10 lần / phút.

### 2.7 Đánh giá sau khi ở

Trong **30 ngày sau khi trả phòng**, mở đặt phòng ở tab **Đã ở** để chấm **1 – 5 sao** và viết nhận xét (tối đa 1 000 ký tự).
Mỗi đặt phòng chỉ đánh giá được 1 lần.

### 2.8 Tài khoản

Trang **Tài khoản**: sửa họ tên, số điện thoại, **đổi mật khẩu**. Phiên đăng nhập được giữ khi tải lại trang. Đăng xuất ở góc trên.

### 2.9 Email tự động

Nếu có email trong đặt phòng, khách nhận thư khi:
- Vừa đặt: kèm hướng dẫn chuyển khoản.
- Đặt phòng được xác nhận.
- Đặt phòng bị huỷ: kèm số tiền hoàn.
- **1 ngày trước ngày nhận phòng**: thư nhắc.

---

## 3. Dành cho lễ tân

Đăng nhập bằng tài khoản nhân viên rồi vào `/admin`, hoặc bấm link **Khu quản trị** ở chân trang. Trên điện thoại, menu quản trị mở bằng nút ☰.

### 3.1 Hôm nay (bảng điều khiển)

Mở đầu ca làm việc ở đây:
- **Khách đến**, **Khách đi**, **Đang ở** của ngày đang chọn. Có nút **Ngày trước / Ngày sau** để xem trước hoặc xem lại.
- **Chờ cọc**: đặt phòng sắp hết hạn giữ chỗ được xếp lên đầu, để gọi nhắc khách kịp.
- **Công suất 7 ngày tới**: tỉ lệ phòng đã bán mỗi ngày.
- Nút **Tạo đặt phòng** nhanh.

### 3.2 Lịch phòng

Màn hình chính của lễ tân: một bảng có **mỗi hàng là một phòng, mỗi cột là một ngày**, xem 14 hoặc 30 ngày và chọn từng homestay.
- Mỗi đặt phòng là **một thanh màu theo trạng thái**, có tên khách. Bấm vào thanh để mở chi tiết.
- **Bấm vào ô trống** rồi chọn:
  - **Tạo đặt phòng**: điền sẵn phòng và ngày.
  - **Khoá phòng**: khoá để bảo trì hoặc chủ dùng, có ghi lý do. Ô khoá hiện màu xám, khách không đặt được.
- Khách trả phòng sớm: thanh hiện dải mỏng tới ngày thực trả, các ô phía sau trống và **đặt được ngay**.
- Dùng được bằng bàn phím (Enter để mở ô / thanh). Trên điện thoại, bảng cuộn ngang và cột tên phòng luôn đứng yên bên trái.

### 3.3 Tạo đặt phòng hộ (khách gọi điện, khách vãng lai, OTA)

Mở **Tạo đặt phòng** từ Lịch phòng, trang Hôm nay hoặc trang Đặt phòng:
1. Chọn homestay, phòng, ngày, số khách. **Báo giá cập nhật ngay** khi đổi lựa chọn.
2. Nhập tên, số điện thoại (bắt buộc) và email khách; chọn nguồn đặt (**Điện thoại**, **Khách vãng lai**, **OTA**); ghi chú của khách và ghi chú nội bộ.
3. Nếu khách **trả cọc ngay**: ghi số tiền và hình thức (tiền mặt, chuyển khoản, thẻ, ví điện tử). Đủ cọc thì đặt phòng **được xác nhận luôn**.
   Nếu chưa trả, đặt phòng ở trạng thái Chờ cọc như đặt online.
4. Đưa **mã đặt phòng** cho khách để họ tự tra cứu sau này.

### 3.4 Danh sách đặt phòng

Menu **Đặt phòng**: lọc theo trạng thái, homestay, khoảng ngày, nguồn đặt; tìm theo **mã, tên hoặc số điện thoại** khách.
Bộ lọc được giữ khi tải lại trang.

### 3.5 Xử lý một đặt phòng

Trang chi tiết chỉ hiện **những nút hợp lệ với trạng thái hiện tại**:

| Nút | Khi nào | Kết quả |
|---|---|---|
| **Xác nhận** | Chờ cọc | Xác nhận tay, có ghi lý do. Thường không cần: ghi đủ tiền cọc thì hệ thống tự xác nhận |
| **Check-in** | Đã xác nhận, từ ngày nhận phòng | Chuyển sang Đang ở |
| **Check-out** | Đang ở | Trả phòng. Nếu còn tiền chưa thu, hệ thống nhắc và yêu cầu ghi lý do mới cho trả. Trả sớm thì các đêm còn lại được mở bán lại |
| **Ghi khoản thu** | Đặt phòng còn hiệu lực | Loại: **Cọc**, **Thu nốt**, **Phát sinh** (minibar, thuê xe…), **Hoàn tiền**. Kèm hình thức, thời điểm, ghi chú (VD mã giao dịch) |
| **Ghi hoàn tiền** | Đã huỷ, hết hạn, không đến | Ghi lại khoản đã chuyển trả khách |
| **Đổi ngày / phòng** | Chờ cọc, đã xác nhận | Tự tính lại giá, hoặc chọn **giữ giá cũ** (chỉ khi số đêm không đổi, phải ghi lý do). Nếu trùng lịch thì đặt phòng giữ nguyên như cũ |
| **Không đến** | Đã xác nhận, đã qua ngày nhận phòng | Giữ tiền cọc, mở lại phòng |
| **Huỷ đặt phòng** | Chờ cọc, đã xác nhận (lễ tân huỷ được cả trong ngày nhận phòng) | Hiện số hoàn đề xuất theo chính sách. Lễ tân **sửa được** số hoàn (VD huỷ do phía homestay thì hoàn đủ), bắt buộc ghi lý do |
| ✏️ **Sửa thông tin khách** | Mọi lúc | Tên, số điện thoại, email, ghi chú |

Trang chi tiết còn có:
- **Tiền**: tổng, cọc, đã thu (đã trừ hoàn), **còn phải thu** hoặc **còn phải hoàn**.
- **Giá đã chốt từng đêm** lúc đặt.
- **Danh sách khoản thu**: ghi nhầm thì xoá được trong 24 giờ (Quản trị xoá được bất kỳ lúc nào).
- **Nhật ký**: ai làm gì, lúc nào (tạo, đổi trạng thái, đổi ngày, thu / hoàn tiền, gửi thư nhắc).

### 3.6 Khách

Menu **Khách**: tìm theo tên, số điện thoại hoặc email. Mỗi khách có **lịch sử lưu trú** (các lần đặt) và đánh dấu nếu khách có tài khoản.

---

## 4. Dành cho quản trị (chủ homestay)

Quản trị dùng được **mọi tính năng của lễ tân**, cộng thêm các mục sau.

### 4.1 Homestay & phòng

- **Thông tin & chính sách** của homestay:
  - Tên, đường dẫn trang, tỉnh / thành, địa chỉ, mô tả, tiện nghi, giờ nhận / trả phòng.
  - **% cọc** (0% thì đặt online được xác nhận ngay), **số giờ giữ chỗ** chờ cọc (1 – 72 giờ).
  - **Chính sách huỷ**: số ngày huỷ miễn phí, số ngày hoàn một phần và % hoàn.
- Trạng thái: **Đang mở bán** hoặc **Ngưng nhận đặt** (ẩn với khách, đặt phòng cũ giữ nguyên). Homestay đã có đặt phòng thì không xoá được, chỉ ẩn được.
- **Ảnh**:
  - Tối đa 20 ảnh, định dạng jpg / png / webp, mỗi ảnh ≤ 5 MB.
  - Kéo thả để sắp xếp (hoặc dùng nút mũi tên); chọn **ảnh bìa**; xoá ảnh.
- **Phòng**:
  - Tên, giường, diện tích, khách tiêu chuẩn / tối đa.
  - **Giá cơ bản / đêm**, **giá T6, T7** (để trống thì dùng giá cơ bản), **phụ thu mỗi khách thêm / đêm**, **phí dọn mỗi lượt**.
  - **Số đêm tối thiểu**, thứ tự hiển thị.
  - **Ngưng** phòng khi không muốn nhận đặt mới.

### 4.2 Giá theo ngày

Menu **Giá theo ngày**: xem lịch giá từng phòng. **Đặt giá riêng cho một khoảng ngày** (lễ, Tết, mùa cao điểm) và áp cho **nhiều phòng một lần**; xoá được khi không cần nữa.

Hệ thống chọn giá một đêm theo thứ tự ưu tiên:
1. Giá riêng theo ngày.
2. Giá cuối tuần (đêm thứ Sáu, thứ Bảy).
3. Giá cơ bản.

### 4.3 Đánh giá

Menu **Đánh giá**: lọc **Tất cả / Đang hiện / Đang ẩn**. **Ẩn** đánh giá spam hoặc xúc phạm (đánh giá ẩn không tính vào điểm trung bình); hiện lại được.
Quản trị không sửa được nội dung khách viết.

### 4.4 Báo cáo

Menu **Báo cáo**: chọn nhanh **Tháng này**, **Tháng trước**, **3 tháng gần đây**, **Năm nay**, hoặc tự chọn khoảng ngày (tối đa 366 ngày). Lọc được theo homestay.

| Chỉ số | Ý nghĩa |
|---|---|
| **Công suất phòng** | Tỉ lệ đêm đã bán trên số đêm có thể bán (không tính đêm khoá bảo trì) |
| **Doanh thu phòng** | Tiền phòng của các đêm khách ở trong kỳ |
| **ADR** | Giá trung bình mỗi đêm đã bán |
| **RevPAR** | Doanh thu trên mỗi đêm có thể bán: vừa phản ánh giá vừa phản ánh công suất |
| **Tiền thu thực tế** | Tiền thực nhận trong kỳ (đã trừ hoàn) |
| **Tỉ lệ huỷ** | Đặt phòng bị huỷ hoặc khách không đến, trên tổng số đặt phòng tạo trong kỳ (hết hạn giữ chỗ báo riêng) |

Có biểu đồ cột theo tháng, bảng theo homestay và theo nguồn đặt (online, điện thoại, vãng lai, OTA). Nút **Xuất CSV** mở được bằng Excel và hiện đúng tiếng Việt.

### 4.5 Nhân viên

Menu **Nhân viên**:
- Tạo tài khoản với vai trò **Lễ tân** (vận hành đặt phòng) hoặc **Quản trị** (thêm homestay, giá, nhân viên), kèm mật khẩu ban đầu.
- Đổi vai trò, **khoá** tài khoản, **đặt lại mật khẩu**. Mỗi thao tác này đăng xuất người đó ngay trên mọi thiết bị.
- Để tránh mất quyền quản lý, quản trị không tự hạ quyền hay tự khoá mình, và hệ thống luôn còn ít nhất 1 quản trị.

---

## 5. Một vòng đời đặt phòng điển hình

```
Khách đặt online ──▶ Chờ cọc ──(khách chuyển khoản, lễ tân ghi khoản Cọc)──▶ Đã xác nhận ──(đến ngày)──▶ Check-in: Đang ở
     │                  │                                                         │
     │                  └─ quá hạn giữ chỗ ─▶ Hết hạn giữ chỗ (phòng mở lại)       ├─ khách không tới ─▶ Không đến
     │                                                                            └─ huỷ ─▶ Đã huỷ (hoàn theo chính sách)
     ▼
Lễ tân tạo hộ (thu cọc ngay) ─▶ Đã xác nhận ……  Đang ở ──(ghi khoản Thu nốt)──▶ Check-out: Đã trả phòng ──▶ khách đánh giá (≤ 30 ngày)
```

Email đi kèm từng bước: đặt phòng → xác nhận → nhắc trước 1 ngày → (nếu huỷ) thông báo huỷ kèm số hoàn.

---

## 6. Câu hỏi thường gặp

| Câu hỏi | Trả lời |
|---|---|
| Tôi chuyển khoản rồi mà vẫn "Chờ cọc"? | Lễ tân cần thời gian đối chiếu tiền về. Nhớ ghi **mã đặt phòng** trong nội dung chuyển khoản để được xác nhận nhanh |
| Hết hạn giữ chỗ rồi mới chuyển cọc? | Liên hệ homestay: lễ tân tạo đặt phòng mới (nếu còn phòng) hoặc hoàn lại tiền |
| Sao giá đêm thứ Sáu khác ngày thường? | Có giá cuối tuần hoặc giá dịp đặc biệt. Khung đặt phòng ghi rõ từng đêm |
| Muốn đổi ngày? | Khách không tự đổi được. Liên hệ homestay, lễ tân đổi ngày hoặc phòng giúp |
| Không có tài khoản thì xem đặt phòng thế nào? | Menu **Tra cứu đặt phòng**, nhập mã + số điện thoại |
| Đánh giá của tôi không hiện? | Có thể đã bị quản trị ẩn vì vi phạm. Mỗi đặt phòng chỉ đánh giá 1 lần, trong 30 ngày sau trả phòng |
| Dùng trên điện thoại được không? | Được. Giao diện tối ưu cho màn hình nhỏ, tự đổi sang **chế độ tối** theo cài đặt máy, dùng được bằng bàn phím và trình đọc màn hình |
