# 05 – Kế hoạch triển khai theo Phase

Tổng ước tính: **~12 ngày làm việc** cho 1 người. Khách đặt phòng online được ở cuối **Phase 4**, lễ tân vận hành được ở cuối **Phase 5**,
phát hành 1 tiến trình ở **Phase 7**. Mỗi phase kết thúc bằng 1 commit / PR chạy được, có DoD rõ ràng.

```
Phase 0 ─▶ Phase 1 ─▶ Phase 2 ─▶ Phase 3 ──────────────┐
 Khung      BE: DB +    BE: Homestay,  BE: Đặt phòng,    │
 + .env     Auth + vai  phòng, giá,    khoá đêm, cọc –   │
            trò         tìm kiếm       huỷ, thu tiền     ▼
                         │                     Phase 4 ═══▶ Phase 5 ═══▶ Phase 6 ──▶ Phase 7 ──▶ (Phase 8)
                         └──────────────────▶  FE khách    FE quản trị   Báo cáo,    Hoàn thiện,  Mở rộng
                                               (tìm, đặt,  (lịch phòng,  đánh giá,   phát hành,   (VNPay, đa
                                               của tôi)    đặt phòng,    email       1 service    chủ, iCal…)
                                                           homestay)
```

| Phase | Nội dung | Ước tính | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 0 | Khung dự án, `.env`, MySQL, layout 2 khu | 0.5 ngày | – | ✅ Xong |
| 1 | Backend: schema đầy đủ + Auth + vai trò + admin đầu tiên | 1.5 ngày | 0 | ✅ Xong |
| 2 | Backend: homestay, phòng, ảnh, giá, tìm kiếm, lịch trống, báo giá | 2 ngày | 1 | ⬜ |
| 3 | Backend: đặt phòng, khoá đêm, vòng đời, cọc – huỷ – hoàn, thu tiền, khoá phòng, hết hạn giữ chỗ | 2 ngày | 2 | ⬜ |
| 4 | Frontend khu khách: tìm kiếm, chi tiết, đặt phòng, đặt phòng của tôi, tra cứu | 2 ngày | 3 | ⬜ |
| 5 | Frontend quản trị: hôm nay, **lịch phòng**, đặt phòng, homestay / phòng / giá, nhân viên | 2.5 ngày | 3 | ⬜ |
| 6 | Báo cáo (công suất, ADR, RevPAR, doanh thu), đánh giá, email | 1 ngày | 4, 5 | ⬜ |
| 7 | Hoàn thiện: phục vụ `fe/dist`, Windows Service, bảo mật, trợ năng, test thủ công | 1 ngày | 6 | ⬜ |
| 8 | Mở rộng (tuỳ chọn) | – | 7 | ⬜ |

> Phase 4 và 5 chỉ phụ thuộc Phase 3 → làm nhóm 2 người: A làm **4**, B làm **5** song song.

---

## Phase 0 – Khung dự án (0.5 ngày)

**Mục tiêu**: backend + frontend chạy được bằng lệnh, kết nối MySQL, khung 2 khu giao diện.

Database
- [x] Database `homestay` + `homestay_test` (utf8mb4), user `homestay` chỉ có quyền trên 2 DB này ở MySQL `localhost:3306`.

Backend
- [x] `backend.csproj` → `Sdk="Microsoft.NET.Sdk.Web"`, package nền (EF Core + MySQL + snake_case, DotNetEnv, OpenAPI), local tool `dotnet-ef`
  (JwtBearer, BCrypt thêm ở Phase 1; MailKit Phase 6; WindowsServices Phase 7 – [03 §1](03-backend-dotnet.md#1-package)).
- [x] `.env` (mật khẩu DB + JWT secret + mật khẩu admin ngẫu nhiên), `.env.example`, `.gitignore`.
- [x] `Program.cs`: nạp `.env` (`Hosting/EnvFile`), `AppOptions` + `AppClock`, `AppDbContext` + MySQL + snake_case, ProblemDetails, OpenAPI (dev).
- [x] `GET /health` kiểm tra DB (`503` khi DB down); `appsettings*.json`, `launchSettings.json` (cổng 5100), `backend.http`.
- [x] `backend.Tests` + `Homestay.slnx`: `ApiFactory` (MySQL `homestay_test`), `HealthTests`.

Frontend
- [x] Vite 8 + React 19 + TypeScript, Tailwind 4, TanStack Query, React Router; proxy `/api`, `/uploads`, `/health` → `API_PROXY_TARGET`.
- [x] `fe/.env`, `.env.example`, `.gitignore` chặn `.env`.
- [x] Layout khu khách (thanh trên + chân trang) và khu quản trị (sidebar + thanh dưới), trang tạm cho mọi route ở [04 §2](04-frontend-react.md#2-trang), 404.
- [x] Thanh trạng thái gọi `/health` (FE ↔ BE ↔ DB thông nhau). Vitest + Testing Library.

**DoD** (đã kiểm tra 29/10/2026)
- ✅ `dotnet run` → `curl http://localhost:5100/health` → `{"status":"ok","database":"up"}`, `/openapi/v1.json` → 200 (Development);
  `dotnet test Homestay.slnx` – 3/3 (health, OpenAPI tắt ngoài Development, 404 ProblemDetails).
- ✅ `npm run build`, `npm run lint`, `npm test` (9 test) qua; `npm run dev` → `http://localhost:5174/health` qua proxy trả `ok`;
  trang chủ khách và `/admin` hiện đúng layout + "Máy chủ: hoạt động" (Edge: máy tính, 360 px – menu ngăn kéo, chế độ tối; axe-core 0 vi phạm, không cuộn ngang).

Phát hiện khi làm: `.gitignore` ghi `data/` (thư mục ảnh upload) làm git trên Windows (không phân biệt hoa thường) bỏ qua luôn mã nguồn `Data/`
→ dữ liệu runtime đổi sang `backend/storage/` (`/storage/` trong `.gitignore`).

---

## Phase 1 – Backend: schema + Auth + vai trò (1.5 ngày)

**Mục tiêu**: đủ bảng ở [06 §4](06-database.md#4-bảng), đăng ký / đăng nhập theo vai trò chạy thật.

- [x] Entity + configuration cho mọi bảng (15 bảng); conventions (decimal, `DateOnly`, `TimeOnly`, UTC, enum snake_case chữ thường
  `checked_in` / `bank_transfer`); 21 CHECK constraint; migration `InitialCreate`.
- [x] Auth như Sổ Chi Tiêu (JWT 15', refresh cookie xoay vòng + phát hiện dùng lại, BCrypt + chống dò email, rate limit) + claim `role`.
- [x] Policy `Staff` (staff | admin), `Admin`; khách gọi `/api/admin/*` → `403`; tài khoản bị khoá → không đăng nhập (`403`) / refresh (`401`) được.
  Mỗi request kiểm tra tài khoản còn hoạt động + vai trò trong token còn đúng → khoá / đổi vai trò có hiệu lực **ngay**, không chờ token 15' hết hạn.
- [x] `Seeder`: tạo admin đầu tiên từ `Seed__AdminEmail/Password` nếu chưa có admin (không tự nâng quyền email đã là tài khoản khác);
  Development (`Seed:SampleData`) thêm dữ liệu mẫu (2 homestay, 5 phòng). Chạy lại không tạo trùng.
- [x] `/api/me` GET / PUT (họ tên, SĐT chuẩn hoá), `/api/me/password`; `/api/admin/users` (danh sách, tạo, sửa / đổi vai trò / khoá, đặt lại mật khẩu)
  – admin không tự hạ quyền / khoá mình; khoá dòng admin (`FOR UPDATE`) để luôn còn ≥ 1 admin kể cả khi 2 admin thao tác cùng lúc.
- [x] Test: `AuthApiTests`, `SessionLifetimeTests`, `RoleTests`, `SchemaTests` (khoá chính `room_nights`, CHECK, kiểu dữ liệu), `HealthTests`.

**DoD**: đăng ký → vai trò `guest`; admin seed đăng nhập được, tạo nhân viên; khách gọi API quản trị → `403`; `dotnet test` pass.

**Kết quả** (đã kiểm tra 29/10/2026)
- ✅ `dotnet test Homestay.slnx` – **71/71** (chạy 2 lần liên tiếp, không lỗi chập chờn).
- ✅ `dotnet run` (Development) → migrate DB `homestay`, log "Đã tạo tài khoản admin đầu tiên", "Đã thêm dữ liệu mẫu: 2 homestay, 5 phòng";
  khởi động lại không tạo thêm. MySQL: 15 bảng, 21 CHECK, collation `utf8mb4_unicode_ci`.
- ✅ Kịch bản HTTP qua proxy Vite `:5174` – 18/18: đăng ký → `guest` (SĐT `+84 912 345 678` → `0912345678`), khách / chưa đăng nhập gọi
  `/api/admin/users` → `403` / `401`, admin seed đăng nhập → tạo lễ tân → lễ tân đăng nhập (`staff`) nhưng gọi API chỉ-admin → `403`,
  admin tự hạ quyền → `409`, khoá lễ tân → token cũ `401` ngay, refresh `401`, đăng nhập `403` "Tài khoản đã bị khoá…", lỗi nhập tiếng Việt theo từng ô.

Ghi chú khi làm: MySQL cấm CHECK trên cột có hành động FK (`CASCADE` / `SET NULL`) → FK của `room_nights` để `RESTRICT`,
service xoá dòng khoá đêm trong cùng transaction (Phase 3). Số cố định VN có 11 số (`028 3822 1234`) → chấp nhận cả di động 10 số và cố định 11 số.

---

## Phase 2 – Backend: homestay, phòng, giá, tìm kiếm (2 ngày)

- [ ] `CatalogService` + `/api/admin/homestays`, `/rooms` (CRUD, slug tự sinh bỏ dấu, ẩn / ngưng thay vì xoá khi đã có đặt phòng).
- [ ] Upload ảnh (kiểm tra magic bytes, ≤ 5 MB, ≤ 20 ảnh, đổi tên guid), sắp xếp, ảnh bìa; phục vụ `/uploads/...`.
- [ ] `PricingService`: giá từng đêm (override > cuối tuần > cơ bản), phụ thu khách, phí dọn, cọc làm tròn nghìn; `/api/admin/rates` (đặt / xoá cho khoảng ngày, nhiều phòng).
- [ ] `AvailabilityService`: `/api/stays` (lọc tỉnh, ngày, khách, tiện nghi, giá; "giá từ"; phân trang), `/api/stays/{slug}`, `/calendar`, `/api/quotes`.
- [ ] Test: giá qua tuần có thứ Sáu / thứ Bảy + ngày lễ, phụ thu, min nights, tìm kiếm loại phòng kín / homestay ẩn.

**DoD**: phòng 800 000 / cuối tuần 1 000 000, ngày 01/01 giá 1 500 000, phụ thu 150 000 / khách vượt 2, phí dọn 100 000:
báo giá 3 khách từ thứ Năm 31/12 → Chủ nhật 03/01 = 800 000 + 1 500 000 + 1 000 000 + 3 × 150 000 + 100 000 = **3 850 000**, cọc 30% = **1 155 000**.

---

## Phase 3 – Backend: đặt phòng (2 ngày)

- [ ] `BookingService`: tạo (khách / nhân viên), mã `HS-XXXX-XXXX`, ảnh chụp giá, khoá `room_nights` trong transaction, `409` khi trùng / giá đổi.
- [ ] Vòng đời [02 §6.3](02-nghiep-vu.md#63-vòng-đời): confirm, check-in, check-out (trả sớm nhả đêm), no-show, huỷ (xem trước + tính hoàn), đổi ngày / phòng.
- [ ] `PaymentService`: ghi / xoá khoản thu – hoàn, tự xác nhận khi đủ cọc, `paid`, `amountDue`, `paymentStatus`.
- [ ] Khoá phòng bảo trì; `booking_events` cho mọi thay đổi; `/api/bookings` của khách, `/lookup`.
- [ ] `HoldExpiryWorker` (mỗi phút) – test bằng `FakeTimeProvider`.
- [ ] `/api/admin/calendar`, `/api/admin/dashboard`, `/api/admin/bookings` (lọc, tìm).
- [ ] Test: **2 request đặt cùng phòng cùng đêm song song → đúng 1 thành công**; huỷ nhả đêm → đặt lại được; chính sách hoàn 7 / 3 ngày; hết hạn giữ chỗ.

**DoD**: kịch bản đặt online → hết hạn sau 24 giờ (đồng hồ giả) → phòng trống lại; đặt → ghi cọc → `confirmed` → check-in → thu đủ → check-out;
huỷ trước 5 ngày đặt phòng đã cọc 1 155 000 → hoàn 577 500 (50%). `dotnet test` pass.

---

## Phase 4 – Frontend khu khách (2 ngày)

- [ ] `lib/api.ts` (refresh single-flight), `AuthProvider` có vai trò, đăng nhập / đăng ký / tài khoản.
- [ ] Trang chủ + ô tìm kiếm (chọn khoảng ngày, số khách), kết quả tìm kiếm (bộ lọc trên URL), chi tiết homestay (ảnh, lịch trống, báo giá dính).
- [ ] Đặt phòng (giữ thông tin khi `409`), trang đặt phòng: hướng dẫn chuyển khoản + đồng hồ hạn giữ chỗ, huỷ (xem trước số hoàn), danh sách của tôi, tra cứu.
- [ ] Test component + luồng đặt phòng (mock API).

**DoD**: trên điện thoại 360 px: tìm → chọn phòng → đặt → thấy mã + hướng dẫn chuyển khoản → huỷ được; không cuộn ngang.

---

## Phase 5 – Frontend quản trị (2.5 ngày)

- [ ] Layout `/admin` + `RequireRole`; trang Hôm nay.
- [ ] **Lịch phòng**: lưới phòng × ngày, thanh đặt phòng theo trạng thái, bấm ô trống → tạo đặt phòng / khoá phòng, chuyển tuần / tháng.
- [ ] Đặt phòng: danh sách + chi tiết với nút theo trạng thái, khoản thu – hoàn, đổi ngày / phòng, nhật ký; tạo đặt phòng hộ khách.
- [ ] Homestay / phòng / ảnh (kéo thả sắp xếp) / chính sách; giá theo ngày; nhân viên; khách.

**DoD**: lễ tân nhận khách gọi điện → tạo đặt phòng từ lịch → ghi cọc → check-in → thu đủ → check-out, lịch cập nhật ngay sau mỗi thao tác.

---

## Phase 6 – Báo cáo, đánh giá, email (1 ngày)

- [ ] `ReportService` + trang Báo cáo (công suất, ADR, RevPAR, doanh thu theo tháng / homestay / nguồn, tỉ lệ huỷ, xuất CSV).
- [ ] Đánh giá: khách viết sau trả phòng, admin ẩn / hiện, điểm trung bình trên trang homestay.
- [ ] Email outbox + `EmailOutboxWorker` (SMTP qua MailKit; dev ghi `.eml`): tạo, xác nhận, huỷ, nhắc trước 1 ngày.

**DoD**: số liệu báo cáo khớp truy vấn SQL viết tay trên cùng dữ liệu; mở `.eml` thấy đúng mã, ngày, số tiền.

---

## Phase 7 – Hoàn thiện & phát hành (1 ngày)

- [ ] Tách code theo route; backend phục vụ `fe/dist` + fallback SPA; `dotnet publish` tự build FE.
- [ ] `UseWindowsService`, script cài / gỡ service, log ra file, tự migrate khi khởi động (Production).
- [ ] Security header (CSP…), tắt OpenAPI production, `Jwt__RefreshCookieSecure` khi có HTTPS.
- [ ] Rà soát axe-core (WCAG AA), bàn phím, chế độ tối, 360 px; test thủ công toàn bộ luồng trên bản publish; tài liệu vận hành.

**DoD**: máy chỉ cần MySQL + .NET runtime – chạy `backend.exe` (hoặc service) là dùng được ở `http://localhost:5100`.

---

## Phase 8 – Mở rộng (tuỳ chọn)

| Hạng mục | Ghi chú |
|---|---|
| Cổng thanh toán **VNPay** (sandbox) | Thanh toán cọc online, IPN tự xác nhận đặt phòng |
| Đồng bộ **iCal** với Airbnb / Booking.com | Nhập lịch bận từ OTA thành khoá phòng, xuất lịch phòng ra iCal |
| Mã giảm giá, giá theo số đêm | Bảng `coupons`, giảm % khi ở ≥ 7 đêm |
| Đặt nhiều phòng 1 lần | Đơn nhóm gồm nhiều `bookings` |
| Sàn nhiều chủ homestay | Vai trò `host`, phân quyền theo homestay, đối soát hoa hồng |
| Triển khai Azure | App Service + Azure Database for MySQL, ảnh lên Blob Storage |
