# 03 – Backend .NET

ASP.NET Core 10 **Minimal API**, project `backend/backend.csproj` (`Sdk="Microsoft.NET.Sdk.Web"`), namespace gốc `backend`.
Quy ước giống project Sổ Chi Tiêu (`azure_api`): endpoint mỏng → service chứa quy tắc [02](02-nghiep-vu.md) → `TypedResults`.

## 1. Package

| Package | Dùng cho |
|---|---|
| `MySql.EntityFrameworkCore` 10.0.x | Provider EF Core cho MySQL (Oracle) |
| `Microsoft.EntityFrameworkCore.Relational` 10.0.14 | Ghim phiên bản (tránh xung đột MSB3277) |
| `Microsoft.EntityFrameworkCore.Design` | `dotnet ef` (local tool trong `dotnet-tools.json`) |
| `EFCore.NamingConventions` | Bảng / cột `snake_case` |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Access token |
| `Microsoft.AspNetCore.OpenApi` | `/openapi/v1.json` (chỉ Development) |
| `BCrypt.Net-Next` | Băm mật khẩu |
| `DotNetEnv` | Nạp `backend/.env` |
| `Microsoft.Extensions.Hosting.WindowsServices` | (Phase 7) Windows Service |
| `MailKit` | (Phase 6) gửi email SMTP |

Test (`backend.Tests`): xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing`. Chạy trên MySQL thật, DB `homestay_test`.

## 2. Cấu trúc thư mục

```
backend/
├── Program.cs                 # DI, middleware, map endpoint
├── appsettings*.json          # mặc định KHÔNG bí mật
├── .env / .env.example        # bí mật + cấu hình local (không commit .env)
├── backend.http               # request mẫu
├── Auth/                      # JwtOptions, TokenService, RefreshCookie, Roles, ClaimsPrincipalExtensions
├── Contracts/                 # DTO request / response (record)
├── Data/
│   ├── AppDbContext.cs        # conventions: decimal, DateOnly, DateTime UTC, enum chữ thường, timestamps
│   ├── Entities/
│   ├── Configurations/
│   ├── Seed.cs                # admin đầu tiên + dữ liệu mẫu (Development)
│   └── Migrations/
├── Endpoints/                 # Public*, Booking*, Admin* – mỗi nhóm route 1 file; RequestValidator, ApiExceptionHandler
├── Hosting/                   # EnvFile, (Phase 7) WebHosting, FileLogger
├── Services/                  # AuthService, CatalogService, PricingService, AvailabilityService, BookingService,
│                              # PaymentService, CalendarService, ReviewService, ReportService, EmailService
└── Workers/                   # HoldExpiryWorker, EmailOutboxWorker (BackgroundService + PeriodicTimer)
```

## 3. Endpoint

🔓 công khai · 👤 khách đăng nhập · 🛎 `staff` / `admin` · 🔑 chỉ `admin`

### 3.1 Hệ thống & xác thực

| Method | Route | Mô tả |
|---|---|---|
| GET | `/health` | 🔓 `{status, database}` – `503` nếu DB down |
| POST | `/api/auth/register` | 🔓 `{email, password, fullName, phone?}` → `201` + cookie `rt` (luôn vai trò `guest`) |
| POST | `/api/auth/login` · `/refresh` · `/logout` | 🔓 như Sổ Chi Tiêu (rate limit login / register 10/phút/IP) |
| GET / PUT | `/api/me` | 👤 hồ sơ (tên, số điện thoại) |
| PUT | `/api/me/password` | 👤 đổi mật khẩu → thu hồi mọi phiên, cấp phiên mới |

### 3.2 Khu khách

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/provinces` | 🔓 tỉnh / thành có homestay đang mở (cho ô tìm kiếm) |
| GET | `/api/stays?province&q&checkIn&checkOut&guests&amenities&priceMax&sort&page` | 🔓 kết quả tìm kiếm ([02 §5](02-nghiep-vu.md#5-tìm-kiếm--tình-trạng-phòng)) |
| GET | `/api/stays/{slug}` | 🔓 chi tiết: ảnh, tiện nghi, chính sách, phòng, điểm đánh giá |
| GET | `/api/stays/{slug}/calendar?from&to` | 🔓 mỗi phòng × ngày: `available`, `price` (≤ 62 ngày) |
| GET | `/api/stays/{slug}/reviews?page` | 🔓 đánh giá đang hiện |
| POST | `/api/quotes` | 🔓 `{roomId, checkIn, checkOut, guests}` → giá từng đêm, tổng, cọc, `available` |
| POST | `/api/bookings` | 👤 `{roomId, checkIn, checkOut, guests, guestName, guestPhone, guestEmail?, note?, expectedTotal}` → `201 BookingDto` |
| GET | `/api/bookings?status` | 👤 đặt phòng của tôi |
| GET | `/api/bookings/{code}` | 👤 chi tiết + hướng dẫn chuyển khoản + nhật ký trạng thái |
| GET | `/api/bookings/{code}/cancel-preview` | 👤 số tiền được hoàn nếu huỷ bây giờ |
| POST | `/api/bookings/{code}/cancel` | 👤 `{reason?}` |
| POST | `/api/bookings/{code}/review` | 👤 `{rating, comment}` |
| POST | `/api/bookings/lookup` | 🔓 `{code, phone}` → chi tiết rút gọn (rate limit 10/phút/IP) |

### 3.3 Khu quản trị (`/api/admin`)

| Method | Route | Mô tả |
|---|---|---|
| GET | `/dashboard?date` | 🛎 đến / đi hôm nay, đang ở, pending sắp hết hạn, công suất 7 ngày |
| GET | `/calendar?homestayId&from&to` | 🛎 lưới phòng × ngày: đặt phòng (mã, tên khách, trạng thái), khoá, giá (≤ 62 ngày) |
| GET | `/bookings?status&homestayId&from&to&q&source&page` | 🛎 danh sách (q: mã / tên / SĐT) |
| POST | `/bookings` | 🛎 tạo hộ khách (`source`, tuỳ chọn `payment` cọc ngay → `confirmed`) |
| GET | `/bookings/{id}` | 🛎 chi tiết + khoản thu + nhật ký |
| PUT | `/bookings/{id}` | 🛎 sửa thông tin khách, ghi chú nội bộ |
| POST | `/bookings/{id}/reschedule` | 🛎 `{roomId, checkIn, checkOut, keepPrice, reason?}` |
| POST | `/bookings/{id}/confirm` · `/check-in` · `/check-out` · `/no-show` | 🛎 chuyển trạng thái (§6.3) |
| POST | `/bookings/{id}/cancel` | 🛎 `{reason, refundAmount?}` |
| POST / DELETE | `/bookings/{id}/payments` · `/payments/{paymentId}` | 🛎 ghi / xoá khoản thu – hoàn |
| GET / POST / DELETE | `/blocks` | 🛎 khoá phòng bảo trì |
| CRUD | `/homestays`, `/homestays/{id}/photos` (multipart), `/rooms` | 🔑 |
| GET / PUT / DELETE | `/rates?roomIds&from&to` | 🔑 giá riêng theo ngày (đặt cho khoảng ngày, nhiều phòng) |
| GET / POST | `/users?q` | 🔑 danh sách lễ tân / quản trị · tạo `{email, password, fullName, phone?, role: staff\|admin}` |
| PUT | `/users/{id}` | 🔑 `{fullName, phone?, role, isActive}` – không tự hạ quyền / khoá mình (`409`), luôn còn ≥ 1 admin |
| POST | `/users/{id}/reset-password` | 🔑 `{password}` → `204`, đăng xuất mọi thiết bị của nhân viên đó |
| GET | `/guests?q` | 🛎 khách (từ tài khoản + đặt phòng hộ), lịch sử lưu trú |
| PUT | `/reviews/{id}` | 🔑 ẩn / hiện đánh giá |
| GET | `/reports/summary` · `/occupancy` · `/revenue` · `/sources` | 🔑 [02 §11](02-nghiep-vu.md#11-báo-cáo-quản-trị); xuất CSV |

## 4. Lỗi (ProblemDetails)

| Tình huống | Status | Ví dụ |
|---|---|---|
| Dữ liệu sai | `400` + `errors: {field: [msg]}` | "Ngày trả phòng phải sau ngày nhận phòng" |
| Chưa đăng nhập / token hết hạn | `401` | – |
| Không đủ quyền (khách gọi `/api/admin`) | `403` | – |
| Không tồn tại / không phải của mình | `404` | "Không tìm thấy đặt phòng" |
| Trùng lịch, sai trạng thái, giá đổi | `409` | "Phòng vừa có người đặt…", "Chỉ check-in được đặt phòng đã xác nhận", "Giá đã thay đổi" |
| Quá nhiều yêu cầu | `429` | – |

## 5. Cấu hình (`.env` dùng `__`)

| Khoá | Mặc định | Ghi chú |
|---|---|---|
| `ASPNETCORE_URLS` | `http://0.0.0.0:5100` | |
| `ConnectionStrings__Default` | – (bắt buộc) | DB `homestay`, user `homestay` |
| `Jwt__Secret` | – (bắt buộc, ≥ 32 ký tự) | `Jwt__Issuer`, `Jwt__Audience`, `Jwt__AccessTokenMinutes` (15), `Jwt__RefreshTokenDays` (30), `Jwt__RefreshCookieSecure` |
| `Seed__AdminEmail` / `Seed__AdminPassword` | – | Tạo admin đầu tiên khi DB chưa có admin (mật khẩu ≥ 8 ký tự) |
| `Seed__SampleData` | `false` (`true` ở `appsettings.Development.json`) | 2 homestay + 5 phòng mẫu khi chưa có homestay |
| `Database__MigrateOnStartup` / `Database__SeedOnStartup` | `false` / `true` | Development luôn migrate |
| `App__TimeZone` | `Asia/Ho_Chi_Minh` | "Hôm nay", hạn giữ chỗ, chính sách huỷ |
| `App__PublicBaseUrl` | `http://localhost:5174` | Link trong email |
| `Payment__BankName` / `__AccountNumber` / `__AccountName` | – | Hiện trong hướng dẫn chuyển khoản |
| `Uploads__Path` | `storage/uploads` | Ảnh homestay (tương đối theo thư mục backend) |
| `Smtp__Host` / `__Port` / `__User` / `__Password` / `__From` | trống → ghi `.eml` vào `storage/mail` | Phase 6 |
| `RateLimit__AuthPermitPerMinute` | `10` | Đăng ký / đăng nhập / tra cứu |

## 6. Ghi chú triển khai

- Enum ↔ chuỗi snake_case (`Data/EnumText.cs`) dùng chung cho DB, JSON (`JsonStringEnumConverter(SnakeCaseLower)`), CHECK constraint.
- JwtBearer `OnTokenValidated` (`Auth/Roles.cs`): mỗi request có token kiểm tra user còn `is_active` và vai trò khớp claim `role` (1 truy vấn theo khoá chính).
- `DateOnly` ↔ `date` (converter riêng – provider Oracle đọc `date` ra `DateTime`), `DateTime` UTC ↔ `datetime(6)`, tiền `decimal(15,2)` (VND làm tròn số nguyên).
- Tạo / đổi / huỷ đặt phòng chạy trong transaction; vi phạm unique `room_nights` (MySQL error 1062) → `409` – có test 2 request song song.
- `HoldExpiryWorker`: mỗi phút `UPDATE … WHERE status = 'pending' AND hold_until < now` theo lô, nhả `room_nights`, ghi `booking_events`.
- Ảnh phục vụ tĩnh ở `/uploads/{homestayId}/{guid}.webp|jpg|png` với `Cache-Control` dài hạn (tên file không bao giờ đổi nội dung).
