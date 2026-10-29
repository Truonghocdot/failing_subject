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
| 2 | Backend: homestay, phòng, ảnh, giá, tìm kiếm, lịch trống, báo giá | 2 ngày | 1 | ✅ Xong |
| 3 | Backend: đặt phòng, khoá đêm, vòng đời, cọc – huỷ – hoàn, thu tiền, khoá phòng, hết hạn giữ chỗ | 2 ngày | 2 | ✅ Xong |
| 4 | Frontend khu khách: tìm kiếm, chi tiết, đặt phòng, đặt phòng của tôi, tra cứu | 2 ngày | 3 | ✅ Xong |
| 5 | Frontend quản trị: hôm nay, **lịch phòng**, đặt phòng, homestay / phòng / giá, nhân viên | 2.5 ngày | 3 | ✅ Xong |
| 6 | Báo cáo (công suất, ADR, RevPAR, doanh thu), đánh giá, email | 1 ngày | 4, 5 | ✅ Xong |
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

- [x] `CatalogService` + `/api/admin/homestays`, `/homestays/{id}/rooms`, `/rooms/{id}` (CRUD, slug tự sinh bỏ dấu – trùng thêm `-2`, sửa tên không đổi slug;
  đã có đặt phòng / khoá phòng → `409` "hãy ẩn / ngưng thay vì xoá"). Lễ tân **đọc** được danh sách homestay / phòng (cần cho lịch, tạo đặt phòng), ghi: chỉ admin.
- [x] Upload ảnh (magic bytes JPG / PNG / WEBP + đọc kích thước từ header, ≤ 5 MB, ≤ 20 ảnh, tên guid), sắp xếp (ảnh đầu = bìa), xoá (xoá cả file);
  phục vụ `/uploads/...` chỉ 3 loại ảnh, `Cache-Control: immutable`, `nosniff`.
- [x] `Pricing` (hàm thuần) + `PricingService`: giá từng đêm (override > cuối tuần > cơ bản), phụ thu khách, phí dọn, cọc làm tròn lên nghìn (không vượt tổng);
  `/api/admin/rates` GET / PUT / DELETE cho khoảng ngày (≤ 366), nhiều phòng (≤ 50).
- [x] `AvailabilityService`: `/api/provinces`, `/api/stays` (tỉnh, `q`, ngày, khách, tiện nghi, `priceMax`, sắp xếp, phân trang 12), `/api/stays/{slug}`,
  `/calendar` (≤ 62 ngày), `/api/quotes`. "Giá từ" = tổng thấp nhất (gồm phụ thu, phí dọn) / số đêm; không chọn ngày → giá cơ bản thấp nhất.
- [x] Test: `StayTests` (DoD đúng ngày 31/12/2026 → 03/01/2027, làm tròn cọc, báo giá qua API có thứ Sáu override + thứ Bảy, lỗi ngày / khách / min nights,
  tìm kiếm loại homestay ẩn + phòng kín + lọc khách / tiện nghi / giá, tỉnh, lịch), `CatalogTests` (slug, chính sách, phòng, quyền, xoá, ảnh, giá theo ngày).

**DoD**: phòng 800 000 / cuối tuần 1 000 000, ngày 01/01 giá 1 500 000, phụ thu 150 000 / khách vượt 2, phí dọn 100 000:
báo giá 3 khách từ thứ Năm 31/12 → Chủ nhật 03/01 = 800 000 + 1 500 000 + 1 000 000 + 3 × 150 000 + 100 000 = **3 850 000**, cọc 30% = **1 155 000**.

**Kết quả** (đã kiểm tra 29/10/2026)
- ✅ Trên app thật (phòng mẫu "Phòng Đồi Thông", admin đặt giá 01/01 = 1 500 000): `POST /api/quotes` 31/12/2026 → 03/01/2027, 3 khách →
  `800 000 (base) + 1 500 000 (override) + 1 000 000 (weekend) + 450 000 + 100 000 = 3 850 000`, cọc **1 155 000**; lịch hiện đúng giá 01/01.
- ✅ Upload PNG thật qua proxy `:5174` → 201 (64×40), `GET /uploads/...` → `image/png` + `immutable` + `nosniff`; file `<?php` đổi đuôi `.jpg` → 400; xoá → file mất (404).

---

## Phase 3 – Backend: đặt phòng (2 ngày)

- [x] `BookingService`: tạo (khách / nhân viên – kèm cọc thu ngay), mã `HS-XXXX-XXXX` (Crockford base32, `RandomNumberGenerator`), ảnh chụp giá
  (`booking_nights` + tổng), khoá `room_nights` trong transaction, `409` khi trùng (lỗi 1062 / deadlock 1213 / lock wait 1205) / giá đổi (kèm `quote` mới).
- [x] Vòng đời [02 §6.3](02-nghiep-vu.md#63-vòng-đời): confirm (chưa đủ cọc → bắt buộc lý do), check-in (trong khoảng ngày ở), check-out (còn nợ → bắt buộc lý do;
  trả sớm nhả đêm), no-show (từ ngày sau ngày nhận), huỷ (xem trước + tính hoàn; nhân viên sửa số hoàn), đổi ngày / phòng (tính lại giá hoặc giữ giá cũ).
  Mọi thay đổi khoá dòng booking (`SELECT … FOR UPDATE`) trước → thu tiền, huỷ, hết hạn chạy đồng thời không ghi đè nhau.
- [x] `PaymentService`: ghi / xoá khoản thu – hoàn (lễ tân xoá trong 24 giờ, admin bất kỳ lúc nào), tự xác nhận khi đủ cọc, `money: {paid, amountDue, pendingRefund, status}`.
- [x] Khoá phòng bảo trì (`/api/admin/blocks`, trùng đặt phòng → `409` kèm danh sách mã); `booking_events` cho mọi thay đổi;
  `/api/bookings` của khách (người khác → `404`), `/lookup` (mã + SĐT, rate limit), hướng dẫn chuyển khoản (`Payment__*`).
- [x] `HoldExpiryWorker` (mỗi phút, `PeriodicTimer` theo `TimeProvider`) → `BookingService.ExpireDueAsync` – test bằng `FakeTimeProvider`.
- [x] `/api/admin/calendar` (≤ 62 ngày, thanh đặt phòng + khoá + giá từng ngày), `/api/admin/dashboard`, `/api/admin/bookings` (lọc trạng thái, homestay, ngày, nguồn, tìm mã / tên / SĐT).
- [x] Test: `BookingTests` (**6 request đặt chồng đêm song song → đúng 1 thành công**, huỷ nhả đêm → đặt lại được, giá đổi, chính sách 10 / 7 / 6 / 3 / 2 ngày,
  quyền xem, tra cứu, vòng đời khách vãng lai, quy tắc trạng thái, khoá phòng, đổi ngày / phòng, lọc + xoá khoản thu),
  `BookingClockTests` (tua đồng hồ – chạy tuần tự: hết hạn giữ chỗ, no-show, trả sớm, xoá khoản thu quá 24 giờ).

**DoD**: kịch bản đặt online → hết hạn sau 24 giờ (đồng hồ giả) → phòng trống lại; đặt → ghi cọc → `confirmed` → check-in → thu đủ → check-out;
huỷ trước 5 ngày đặt phòng đã cọc 1 155 000 → hoàn 577 500 (50%). `dotnet test` pass.

**Kết quả** (đã kiểm tra 29/10/2026)
- ✅ `dotnet test Homestay.slnx` – **111/111** (chạy 2 lần liên tiếp); không có thay đổi model (`has-pending-model-changes` → không cần migration mới).
- ✅ Hết hạn giữ chỗ: tua 23 giờ → vẫn `pending`; tua thêm 1 giờ 1 phút → `expired`, `room_nights` trống, báo giá `available = true`, ghi cọc muộn → `409`.
- ✅ Kịch bản HTTP qua proxy Vite `:5174` – **33/33** (cả Phase 2): giá đổi → `409` kèm báo giá mới; đặt online → `pending`, mã `HS-…`, hướng dẫn CK 1 155 000;
  khách khác đặt chồng → `409`, xem → `404`; ghi cọc 1 155 000 → tự `confirmed`; huỷ trước 5 ngày → hoàn **577 500** → ghi hoàn → `refunded`; huỷ nhả đêm → đặt lại được;
  khách vãng lai: tạo + cọc → `confirmed` → check-in → check-out khi còn nợ `409` → thu đủ → `checked_out` / `paid`; tra cứu mã + SĐT; 2 request song song → `201` + `409`;
  khoá phòng → báo giá `available = false`; bảng điều khiển, lịch phòng 14 ngày; khách gọi lịch phòng → `403`.

Phát hiện khi làm:
- Đăng nhập cùng tài khoản song song bị **deadlock** MySQL (chèn refresh token giữ khoá chia sẻ dòng `users` qua FK + `UPDATE last_login_at` cần khoá ghi trong cùng
  transaction) → tách `last_login_at` thành lệnh riêng (`AuthService.LoginAsync`). Lỗi có từ Phase 1, lộ ra khi test đăng nhập admin seed song song nhiều hơn.
- Trả phòng sớm ngay trong ngày không hiện ở "khách đi hôm nay" → bảng điều khiển tính cả đặt phòng `checked_out` có `checked_out_at` trong ngày.
- EF ghi log mức Error cho lỗi trùng khoá đã được đổi thành `409` → tắt category `Microsoft.EntityFrameworkCore.Update` (lỗi thật vẫn được middleware ghi log 500).

---

## Phase 4 – Frontend khu khách (2 ngày) ✅

- [x] `lib/api.ts`: access token chỉ trong RAM, 401 → refresh **single-flight** rồi gửi lại, `ApiError.extensions` (VD `quote` khi giá đổi);
  `AuthProvider` (vai trò, F5 vẫn đăng nhập nhờ cookie refresh), `RequireAuth` / `GuestOnly` + `?next=` (chặn `//evil.com`); đăng nhập / đăng ký / tài khoản (hồ sơ, đổi mật khẩu).
- [x] Trang chủ (ô tìm kiếm: nơi đến từ `/api/provinces`, nhận – trả phòng, số khách; tỉnh phổ biến; homestay điểm cao); `/stays` – mọi bộ lọc trên URL
  (tỉnh, tên, ngày, khách, tiện nghi, giá tối đa, sắp xếp, trang; ngày sai / quá khứ trên URL bị bỏ thay vì gửi lên gây `400`);
  `/stays/:slug?room&checkIn&checkOut&guests` – ảnh, tiện nghi, chính sách, phòng, **lịch tháng** (giá từng đêm, đêm đã bán gạch, bấm 2 lần chọn ngày),
  khung đặt phòng dính (máy tính) / thanh tổng tiền dính đáy (điện thoại) với báo giá từng đêm.
- [x] `/book/:roomId?stay&checkIn&checkOut&guests` – điền sẵn từ tài khoản, đồng ý chính sách; `409` giá đổi → hiện giá mới, **giữ nguyên thông tin đã nhập**, bấm lại để chốt;
  `409` phòng vừa bị đặt → báo + làm mới báo giá, khoá nút. `/bookings/:code` – hướng dẫn chuyển khoản (nút chép), **đồng hồ giữ chỗ** đếm từng giây
  (hết giờ → tải lại trạng thái), các mốc, huỷ (hộp thoại xem trước số hoàn + lý do). `/bookings` – tab Sắp tới · Đã ở · Đã huỷ (trên URL). `/lookup` – mã + SĐT (chuẩn hoá `+84`).
- [x] Test (Vitest + mock API): `api.test` (refresh 1 lần cho 3 request 401 song song, refresh lỗi không lặp), `stays.test` (URL ↔ bộ lọc, báo giá, phòng kín, 404),
  `booking.test` (đăng nhập giữa chừng quay lại đúng trang, giá đổi → đặt lại, phòng vừa bị đặt, huỷ, không được huỷ, tab, tra cứu, tài khoản, đăng ký).

**DoD**: trên điện thoại 360 px: tìm → chọn phòng → đặt → thấy mã + hướng dẫn chuyển khoản → huỷ được; không cuộn ngang.

**Kết quả** (đã kiểm tra 29/10/2026)
- ✅ `npm test` – **30/30**; `tsc -b`, `oxlint` sạch; `npm run build` OK (bundle 575 kB – tách code theo route để Phase 7).
- ✅ Chạy thật (backend `:5100` + Vite `:5174` + MySQL), Chrome headless **360 × 780** – **31/31**: trang chủ chọn tỉnh + ngày → `/stays?…` (F5 giữ nguyên) →
  chi tiết → chọn phòng → báo giá 2 đêm 2 200 000 → "Đặt ngay" → `/login?next=/book/…` → đăng ký → quay về đúng trang đặt, điền sẵn tên + SĐT →
  chưa tick đồng ý bị chặn → đặt → mã `HS-…`, hướng dẫn chuyển khoản (nội dung = mã), đồng hồ 11:59:59 → 11:59:58 → danh sách "Sắp tới" →
  huỷ (xem trước hoàn 0 ₫) → "Đã huỷ" → đăng xuất → tra cứu mã chữ thường + SĐT `+84 …` → đăng nhập lại về `/bookings`, tab Đã huỷ;
  **không cuộn ngang ở cả 7 màn hình**, không lỗi JS trên console.

Ghi chú: trang `/book` nhận thêm `?stay=<slug>` để hiện tên homestay / phòng / chính sách (báo giá chỉ trả `roomId`, `homestayId`).

---

## Phase 5 – Frontend quản trị (2.5 ngày) ✅

- [x] `/admin` nằm trong `RequireRole(staff, admin)` – khách đăng nhập → "Không có quyền", chưa đăng nhập → `/login?next`; mục 🔑 bọc thêm `RequireRole(admin)`,
  lễ tân không thấy menu (gõ thẳng URL → "Không có quyền", API vẫn `403`); sidebar có người dùng + đăng xuất.
  **Hôm nay** (`?date=` lùi / tới): khách đến / đi / đang ở / chờ cọc (đếm ngược giữ chỗ), công suất 7 ngày (bảng số + thanh).
- [x] **Lịch phòng** `/admin/calendar?homestayId&from&days=14|30`: lưới phòng × ngày (cột tên phòng dính trái, cuộn ngang trong khung), giá từng ô,
  thanh đặt phòng màu theo trạng thái (kèm chữ + `aria-label`), khoá phòng sọc chéo; xếp làn khi thanh chồng (đêm đã nhả được đặt lại);
  bấm ô trống → tạo đặt phòng (điền sẵn phòng + ngày) / khoá phòng ngay trong hộp thoại (trùng → `409` kèm mã); bấm khoá → bỏ khoá; lùi / tới 7 ngày, chọn ngày.
- [x] Đặt phòng: danh sách (trạng thái, homestay, nguồn, khoảng ngày, tìm mã / tên / SĐT – trên URL; bảng máy tính, thẻ điện thoại);
  `/admin/bookings/new` (báo giá sống, thu cọc / thanh toán đủ ngay → tự xác nhận); chi tiết: nút theo trạng thái (xác nhận – thiếu cọc bắt buộc lý do,
  check-in, check-out – còn nợ bắt buộc lý do, không đến, huỷ – xem trước + sửa số hoàn + lý do), ghi khoản thu / hoàn (số tiền gợi ý theo loại), xoá khoản ghi nhầm,
  đổi ngày / phòng (báo giá mới hoặc giữ giá + lý do), sửa thông tin khách / ghi chú nội bộ, nhật ký đầy đủ người làm.
  Mọi thao tác ghi thẳng kết quả vào cache chi tiết + `invalidateBookings` (lịch, hôm nay, danh sách, khách, phía khách).
- [x] Homestay (thêm, sửa thông tin + chính sách, ẩn / xoá), ảnh (tải nhiều ảnh, **kéo thả** hoặc nút ← → / "đặt làm bìa" cho bàn phím, xoá), phòng (thêm / sửa / ngưng / xoá);
  **giá theo ngày** (bảng tháng theo nguồn giá, đặt / bỏ giá riêng nhiều phòng); **nhân viên** (tạo, vai trò, khoá, đặt lại mật khẩu – không tự hạ quyền / khoá mình);
  **khách** – backend mới `GET /api/admin/guests?q&page` (gộp theo SĐT: số lần đặt / ở / sắp tới / huỷ, đã trả, lần nhận phòng gần nhất), bấm → lịch sử lưu trú.
- [x] Test: `admin.test` (lịch: thanh, ô đã bán khoá, khoá phòng, đêm đã nhả bấm được, 30 ngày + tuần sau; **vòng đời DoD** với API giả có trạng thái;
  xác nhận / huỷ bắt buộc lý do + sửa số hoàn; danh sách lọc trên URL; hôm nay; khách; ảnh + phòng; chính sách), `layout.test` (admin / lễ tân / khách / chưa đăng nhập);
  backend `Guests_GroupedByPhone…`.

**DoD**: lễ tân nhận khách gọi điện → tạo đặt phòng từ lịch → ghi cọc → check-in → thu đủ → check-out, lịch cập nhật ngay sau mỗi thao tác.

**Kết quả** (đã kiểm tra 29/10/2026)
- ✅ `npm test` – **43/43**; `tsc -b`, `oxlint` sạch; `dotnet test` – **112/112**.
- ✅ Chạy thật (backend `:5100` + Vite `:5174` + MySQL), Chrome 1366 px – **21/21**: admin đăng nhập → tạo tài khoản lễ tân qua giao diện; trang sửa homestay, giá theo ngày mở được →
  lễ tân đăng nhập: menu chỉ 4 mục, `/admin/users` → "Không có quyền" → lịch phòng: bấm ô trống hôm nay → tạo đặt phòng (điền sẵn phòng + ngày), thu cọc tiền mặt 270 000 →
  **Đã xác nhận**, thanh hiện trên lịch → check-in → lịch "Đang ở" → check-out khi còn nợ: hộp thoại báo còn phải thu → ghi khoản thu (gợi ý 630 000) → "Đã thanh toán đủ" →
  check-out → lịch "Đã trả phòng" (cập nhật sau ≤ 0,2 giây, không cần F5) → trang Khách gộp đúng SĐT "1 lần ở" → bấm sang danh sách lọc theo SĐT;
  điện thoại 360 px: trang không cuộn ngang, lịch cuộn trong khung; không lỗi JS. Chạy lại kịch bản Phase 4 – **31/31**.

Phát hiện khi làm:
- Trả phòng ngay trong ngày nhận phòng: đêm đã nhả nhưng thanh "Đã trả phòng" phủ kín ô → không bấm được để đặt lại → thanh của đặt phòng đã nhả đêm
  (trả phòng, không đến) vẽ thành dải mỏng phía trên ô. Nhật ký ghi "nhả **0** đêm" dù đã nhả cả đêm nay → sửa công thức `BookingService.CheckOutAsync`
  (đếm từ `max(hôm nay, ngày nhận)`), test thêm câu nhật ký.
- Lưới lịch dãn theo chữ dài trong thanh (14 ngày chỉ hiện 7 cột) → đặt bề rộng tối thiểu cố định cho lưới, chữ trong thanh cắt `…`.

---

## Phase 6 – Báo cáo, đánh giá, email (1 ngày) ✅

- [x] `ReportService` + `GET /api/admin/reports?from&to&homestayId` (🔑, ≤ 366 ngày, mặc định tháng này) – tổng + theo tháng / homestay / nguồn:
  đêm có thể bán (phòng đang mở bán × ngày − đêm khoá), đêm đã bán, **công suất**, doanh thu phòng (giá đêm + phụ thu chia theo đêm, không gồm phí dọn),
  **ADR**, **RevPAR**, tiền thu thực tế (thu − hoàn theo `paid_at` giờ VN, hoàn tách riêng), đặt phòng tạo trong kỳ, **tỉ lệ huỷ** (huỷ + không đến; hết hạn đếm riêng).
  Trang **Báo cáo**: chọn nhanh kỳ / khoảng ngày / homestay trên URL, 6 ô chỉ số, 2 biểu đồ cột riêng (doanh thu · công suất – không dùng 2 trục), bảng tháng / homestay / nguồn,
  **xuất CSV** từng bảng (BOM UTF-8 cho Excel). Biểu đồ SVG tự viết (`components/ColumnChart`) theo skill dataviz – màu `#0d9488` đã chạy validator cả nền sáng / tối,
  hover / focus từng cột có tooltip; không thêm `recharts`.
- [x] Đánh giá: `POST /api/bookings/{code}/review` (chủ đặt phòng, đã trả phòng ≤ 30 ngày, 1 lần – unique `booking_id`, 2 request đồng thời → `409`),
  `BookingDto.review` + `canReview`; `GET /api/stays/{slug}/reviews?page` (tên rút gọn "Hà P.", không lộ SĐT / email); admin `GET /api/admin/reviews?homestayId&hidden&page`,
  `PUT /api/admin/reviews/{id} {isHidden}`; điểm trung bình chỉ tính đánh giá đang hiện. FE: chọn sao (nhóm radio, dùng bàn phím được) trên trang đặt phòng,
  mục Đánh giá + "Xem thêm" trên trang homestay, trang quản trị ẩn / hiện.
- [x] Email outbox (`EmailService.Queue` trong **cùng transaction** với thay đổi đặt phòng): tạo (chờ cọc → kèm hướng dẫn chuyển khoản; đủ cọc ngay → thư xác nhận),
  xác nhận (tay hoặc tự động khi ghi đủ cọc), huỷ (kèm số hoàn + lý do), nhắc trước 1 ngày (ghi event `reminder` → không nhắc trùng); khách không có email → bỏ qua.
  `EmailOutboxWorker` mỗi 30 giây gửi qua `IMailSender`: có `Smtp__Host` → MailKit (465 SSL / 587 STARTTLS), trống → `.eml` vào `storage/mail`; lỗi thử lại tối đa 5 lần rồi `failed`.
- [x] Test: backend `Phase6Tests` (.eml đọc lại bằng MimeKit có mã / ngày / số tiền, chuỗi thư giữ chỗ → xác nhận → huỷ, nhắc 1 lần, gửi lỗi → failed,
  đánh giá trước / sau trả phòng / trùng / người khác / ẩn, cửa sổ 30 ngày, tên rút gọn, **báo cáo so với SQL viết tay** trên DB test, quyền 🔑, giới hạn 366 ngày);
  FE `phase6.test` (chỉ số, biểu đồ + tooltip, chọn kỳ trên URL, CSV, viết đánh giá, danh sách đánh giá + xem thêm, ẩn đánh giá).

**DoD**: số liệu báo cáo khớp truy vấn SQL viết tay trên cùng dữ liệu; mở `.eml` thấy đúng mã, ngày, số tiền.

**Kết quả** (đã kiểm tra 29/10/2026)
- ✅ `dotnet test` – **123/123**; `npm test` – **49/49**; `tsc -b`, `oxlint` sạch; không cần migration mới (bảng `reviews`, `email_outbox` có từ Phase 1).
- ✅ Báo cáo tháng 10/2026 trên DB dev – SQL viết tay chạy bằng `docker exec … mysql` khớp từng số với trang Báo cáo: đêm bán / có thể bán **6 / 155** (3,9%),
  doanh thu phòng **4 350 000** → ADR **725 000**, RevPAR **28 065**; tiền thu thực tế **6 105 000** (đã trừ hoàn 1 155 000); 18 đặt phòng tạo, 10 huỷ → **55,6%**.
- ✅ `.eml` trong `backend/storage/mail` (đọc bằng thư viện `email` của Python): thư "Đã giữ chỗ – vui lòng chuyển khoản cọc" có mã `HS-1Q2E-H9EH`,
  "Thứ Năm, 29/10/2026 từ 14:00 → Thứ Sáu, 30/10/2026 trước 12:00", tổng 750 000, cọc 225 000, hướng dẫn chuyển khoản (nội dung = mã);
  thư "đã được xác nhận" sau khi ghi đủ tiền, đã thanh toán 750 000.
- ✅ Kịch bản trình duyệt – **13/13**: đặt online → worker ghi `.eml` → lễ tân thu đủ, check-in, check-out → khách đánh giá 5 sao (thiếu sao bị chặn) →
  trang homestay hiện "Hà P." → admin ẩn → trang homestay hết đánh giá đó → báo cáo có tooltip, tải CSV (BOM + tiêu đề tiếng Việt) → 360 px không cuộn ngang.
  Chạy lại kịch bản Phase 4 (**31/31**) và Phase 5 (**21/21**).

Phát hiện khi làm:
- `WebUtility.HtmlEncode` đổi mọi chữ có dấu thành `&#225;…` trong thư → dùng `HtmlEncoder.Create(UnicodeRanges.All)` (chỉ mã hoá `< > & " '`).
- Biểu đồ trên điện thoại làm trang rộng 637 px: SVG đo lần đầu 600 px kéo cột grid rộng theo → `contain: inline-size` cho khung biểu đồ.

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
