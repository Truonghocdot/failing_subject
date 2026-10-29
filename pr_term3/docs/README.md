# Homestay Booking – Hệ thống đặt & quản lý homestay

Ứng dụng web cho **một đơn vị kinh doanh homestay** (nhiều homestay, mỗi homestay nhiều phòng):

- **Khách** tìm homestay theo nơi đến / ngày / số người, xem ảnh – tiện nghi – giá từng đêm, đặt phòng online,
  chuyển khoản tiền cọc, theo dõi / huỷ đặt phòng, đánh giá sau khi ở.
- **Lễ tân** xử lý đặt phòng trên **lịch phòng** (phòng × ngày): xác nhận cọc, nhận đặt qua điện thoại / khách vãng lai,
  check-in, check-out, ghi thu tiền, hoàn tiền, khoá phòng bảo trì.
- **Quản trị (chủ)** quản lý homestay, phòng, giá (ngày thường / cuối tuần / mùa cao điểm), chính sách cọc – huỷ,
  tài khoản nhân viên, xem báo cáo công suất phòng và doanh thu.

| Thành phần | Công nghệ | Thư mục | Cổng dev |
|---|---|---|---|
| Backend API | .NET 10 Minimal API, EF Core 10, MySQL | `backend/` | `5100` |
| Frontend | React 19 + Vite 8 + TypeScript, Tailwind CSS 4 | `fe/` | `5174` |
| Test backend | xUnit + WebApplicationFactory, MySQL `homestay_test` | `backend.Tests/` | – |
| Database | MySQL (Docker `mysql-dev`, chỉ giữ dịch vụ DB) | – | `3306` |

> **Nguyên tắc chạy:** mọi thứ chạy thủ công bằng lệnh hoặc service lẻ, **không đóng gói Docker**.
> Docker trên máy dev chỉ giữ MySQL ở cổng 3306.

## Tài liệu

| # | File | Nội dung |
|---|---|---|
| 1 | [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) | Kiến trúc, luồng xác thực, luồng đặt phòng, chạy & triển khai |
| 2 | [02-nghiep-vu.md](02-nghiep-vu.md) | **Quy tắc nghiệp vụ**: vai trò, giá, tình trạng phòng, vòng đời đặt phòng, cọc – huỷ – hoàn tiền, báo cáo |
| 3 | [03-backend-dotnet.md](03-backend-dotnet.md) | Package, cấu trúc, danh sách endpoint, lỗi, cấu hình |
| 4 | [04-frontend-react.md](04-frontend-react.md) | Trang khách / trang quản trị, cấu trúc, state, hiển thị |
| 5 | [05-ke-hoach-phase.md](05-ke-hoach-phase.md) | **Kế hoạch triển khai theo phase** + checklist + DoD |
| 6 | [06-database.md](06-database.md) | Database: ERD, bảng, index, chống đặt trùng, `.env` |
| 7 | [07-van-hanh.md](07-van-hanh.md) | **Vận hành**: publish, `.env` production, chạy `backend.exe`, Windows Service, cập nhật, bảo mật, sự cố |

## Phạm vi & giả định (đã chọn mặc định – sửa ở đây nếu khác)

| Câu hỏi | Mặc định |
|---|---|
| Mô hình | 1 đơn vị vận hành sở hữu nhiều homestay (không phải sàn nhiều chủ – để Phase mở rộng) |
| Đơn vị đặt | 1 đặt phòng = 1 phòng (nhà nguyên căn = 1 "phòng"); nhóm nhiều phòng = nhiều đặt phòng |
| Thanh toán | Cọc bằng chuyển khoản (nội dung = mã đặt phòng), lễ tân xác nhận đã nhận; tiền mặt / thẻ khi check-in. Cổng VNPay sandbox ở Phase mở rộng |
| Tiền tệ / múi giờ | VND, `Asia/Ho_Chi_Minh` |
| Khách không có tài khoản | Lễ tân tạo đặt phòng hộ; khách tra cứu bằng **mã đặt phòng + số điện thoại** |

## Chạy nhanh (dev)

Cần: .NET SDK 10, Node 22+, MySQL ở `localhost:3306` đã có database + user `homestay` ([06 §2](06-database.md#2-khởi-tạo-database-làm-1-lần)).
Lần đầu: copy `.env.example` → `.env` ở `backend/` và `fe/`, điền mật khẩu DB + JWT secret.

```bash
# Terminal 1 – backend
cd backend
dotnet tool restore
dotnet run                     # http://localhost:5100 – Development tự áp migration
curl http://localhost:5100/health

# Test backend (MySQL thật, DB riêng homestay_test)
dotnet test ../Homestay.slnx

# Terminal 2 – frontend
cd fe
npm install
npm run dev                    # http://localhost:5174 – /api được proxy sang backend
```
