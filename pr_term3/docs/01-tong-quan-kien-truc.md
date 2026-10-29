# 01 – Tổng quan kiến trúc

## 1. Sơ đồ

```
 Trình duyệt (khách / lễ tân / quản trị)
        │  https? → http://localhost:5174 (dev, Vite)  |  http://localhost:5100 (release, backend phục vụ fe/dist)
        ▼
 ┌──────────────────────────────┐        ┌───────────────────────────────┐
 │ fe/ – React SPA              │  /api  │ backend/ – ASP.NET Core 10    │
 │  • Khu khách  (/ , /stays…)  │ ─────▶ │  Endpoints (Minimal API)      │
 │  • Khu quản trị (/admin/…)   │  JSON  │  Services (quy tắc nghiệp vụ) │
 │  TanStack Query, RHF + zod   │ ◀───── │  BackgroundService:           │
 └──────────────────────────────┘        │   – huỷ giữ chỗ quá hạn       │
                                         │   – gửi email (outbox)        │
                                         │  EF Core 10 ─────────┐        │
                                         └──────────────────────┼────────┘
                                                                ▼
                                    MySQL `homestay` (Docker mysql-dev :3306 – chỉ giữ DB)
                                    Ảnh upload: thư mục `storage/uploads/` cạnh backend
```

- **1 SPA, 2 khu**: khu khách (công khai + "Đặt phòng của tôi") và khu quản trị `/admin` (lễ tân, quản trị). Phân quyền ở **backend**;
  FE chỉ ẩn / hiện menu theo vai trò.
- Dev: Vite proxy `/api`, `/uploads`, `/health` → backend → **cùng origin**, cookie refresh `SameSite=Strict` hoạt động, không cần CORS.
- Release: backend phục vụ luôn `fe/dist` → 1 tiến trình.

## 2. Công nghệ

| Tầng | Chọn | Lý do |
|---|---|---|
| API | .NET 10 Minimal API, `TypedResults`, ProblemDetails | Gọn, nhanh, đồng bộ với các project khác trong repo |
| ORM | EF Core 10 + `MySql.EntityFrameworkCore` (Oracle) + snake_case | Migration, LINQ; MySQL đã có sẵn |
| Auth | JWT access 15' (RAM) + refresh token cookie HttpOnly (DB lưu SHA-256), vai trò `admin` / `staff` / `guest` | Không lưu token ở localStorage; thu hồi được |
| Chống đặt trùng | Bảng `room_nights` – **unique (room_id, night)** | DB đảm bảo, đúng cả khi 2 người bấm đặt cùng lúc ([02 §6.2](02-nghiep-vu.md#62-khoá-đêm-chống-đặt-trùng)) |
| Thời gian | `TimeProvider` + `AppClock` (`Asia/Ho_Chi_Minh`) | "Hôm nay", hạn giữ chỗ, chính sách huỷ test được |
| FE | React 19, Vite 8, TS, Tailwind 4, TanStack Query 5, React Router, react-hook-form + zod, dayjs, Recharts | Giống project Sổ Chi Tiêu – tái dùng kinh nghiệm |
| Test | xUnit + WebApplicationFactory trên MySQL thật (`homestay_test`); Vitest + Testing Library | SQLite khác MySQL (khoá unique, decimal, collation) |

## 3. Luồng xác thực

```
Đăng nhập ──▶ POST /api/auth/login {email, password}
          ◀── 200 { accessToken (15'), user{ role } }  + Set-Cookie: rt=<64 byte>; HttpOnly; SameSite=Strict; Path=/api/auth
Gọi API   ──▶ Authorization: Bearer <accessToken>
401       ──▶ FE tự POST /api/auth/refresh (cookie) → token mới (xoay vòng refresh token; dùng lại token cũ sau 30" → thu hồi mọi phiên)
Đăng xuất ──▶ POST /api/auth/logout → thu hồi + xoá cookie
```

- Vai trò nằm trong claim `role` của JWT; endpoint quản trị yêu cầu policy `Staff` (staff hoặc admin) hoặc `Admin`.
- Tài khoản admin đầu tiên tạo lúc khởi động từ `.env` (`Seed__AdminEmail`, `Seed__AdminPassword`) nếu chưa có admin nào.
- Khách tự đăng ký (vai trò `guest`); nhân viên do admin tạo.

## 4. Luồng đặt phòng (khách online)

```
Tìm (nơi đến, ngày, số khách) ─▶ GET /api/stays?…            danh sách homestay còn phòng + "giá từ"
Xem homestay                   ─▶ GET /api/stays/{slug}       ảnh, tiện nghi, phòng, lịch còn trống
Chọn phòng + ngày              ─▶ POST /api/quotes            giá từng đêm, phụ thu, phí dọn, tổng, tiền cọc
Đặt                            ─▶ POST /api/bookings          ① kiểm tra lại giá ② khoá đêm (room_nights) ③ status = pending,
                                                              hold_until = now + 24h → trả mã HS-xxxx + hướng dẫn chuyển khoản
Khách chuyển khoản cọc (nội dung = mã)
Lễ tân thấy tiền ─▶ POST /api/admin/bookings/{id}/payments {kind: deposit} ─▶ status = confirmed (email xác nhận)
Quá hạn chưa cọc ─▶ HoldExpiryService (mỗi phút) ─▶ status = expired, nhả đêm
Ngày đến ─▶ check-in ─▶ thu phần còn lại ─▶ check-out ─▶ khách đánh giá
```

## 5. Chạy & triển khai

**Nguyên tắc: chạy thủ công bằng lệnh hoặc service lẻ, không đóng gói Docker.** Docker trên máy dev chỉ giữ MySQL ở cổng 3306.

| Môi trường | Cách chạy |
|---|---|
| Dev | 2 terminal: `dotnet run` (backend :5100) + `npm run dev` (fe :5174, proxy `/api`) |
| Release | `dotnet publish -c Release -o publish` (tự build fe → `wwwroot`) → `publish\backend.exe` – 1 tiến trình ở `:5100` ([07](07-van-hanh.md)) |
| Windows Service | `scripts\install-service.ps1` (Run as Administrator) – service `HomestayBooking`, `UseWindowsService()` |
| Azure (tuỳ chọn) | App Service + Azure Database for MySQL (Phase 8) |

Cổng: backend **5100**, Vite **5174** (tránh trùng VsMovie 5080, Sổ Chi Tiêu 5090 / 5173).
