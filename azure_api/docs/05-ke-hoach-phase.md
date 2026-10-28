# 05 – Kế hoạch triển khai theo Phase

Tổng thời gian ước tính: **~7.5 ngày làm việc** cho 1 người.
MVP ghi chép được (ví + danh mục + giao dịch) ở cuối **Phase 4**, có báo cáo / ngân sách ở **Phase 5**, phát hành ở **Phase 6**.
Mỗi phase kết thúc bằng **1 commit/PR** chạy được, có tiêu chí hoàn thành (DoD) rõ ràng.

```
Phase 0 ──▶ Phase 1 ──▶ Phase 2 ─────────────┐
 Khung       BE: DB      BE: Ví, Danh mục,    │
 + .env      + Auth      Giao dịch, Ngân sách,│
             │           Báo cáo              ▼
             └────────▶ Phase 3 ──▶ Phase 4 ══▶ Phase 5 ══▶ Phase 6 ──▶ (Phase 7)
                        FE: nền     FE: Ví,     FE: Tổng    Hoàn thiện   Mở rộng
                        + Đăng nhập Danh mục,   quan, Báo   Test, build, (Azure, định kỳ,
                                    Giao dịch   cáo, Ngân   1 service    hoá đơn…)
                                                sách
```

| Phase | Nội dung | Ước tính | Phụ thuộc | Trạng thái |
|---|---|---|---|---|
| 0 | Khung dự án, `.env`, kết nối MySQL | 0.5 ngày | – | ✅ Xong |
| 1 | Backend: schema + Auth | 1 ngày | 0 | ✅ Xong |
| 2 | Backend: Ví, Danh mục, Giao dịch, Ngân sách, Báo cáo | 1.5 ngày | 1 | ✅ Xong |
| 3 | Frontend nền tảng + Đăng nhập / Đăng ký | 1 ngày | 1 | ✅ Xong |
| 4 | Frontend: Ví + Danh mục + Giao dịch | 1.5 ngày | 2, 3 | ✅ Xong (MVP) |
| 5 | Frontend: Tổng quan + Báo cáo + Ngân sách | 1 ngày | 4 | ✅ Xong |
| 6 | Hoàn thiện, test, build, chạy 1 service | 1 ngày | 5 | ✅ Xong |
| 7 | Mở rộng (tuỳ chọn) | – | 6 | ⬜ |

> Phase 3 chỉ phụ thuộc Phase 1 → làm nhóm 2 người: người A làm **2** (backend), người B làm **3** (frontend) song song, rồi ghép ở Phase 4.

---

## Phase 0 – Khung dự án (0.5 ngày)

**Mục tiêu**: hai project chạy được bằng lệnh, cấu hình môi trường và kết nối MySQL sẵn sàng.

Database
- [x] Tạo database `expense` (utf8mb4) + user `expense` chỉ có quyền trên `expense.*` ở MySQL `localhost:3306`.

Backend
- [x] Đổi `backend.csproj` sang `Sdk="Microsoft.NET.Sdk.Web"`, thêm package ([03 §1](03-backend-dotnet.md#1-package)) + local tool `dotnet-ef`.
- [x] `backend/.env` (mật khẩu DB + JWT secret ngẫu nhiên), `.env.example`, `.gitignore`.
- [x] `Program.cs`: `Env.TraversePath().Load()`, validate `JwtOptions`, `AppDbContext` + MySQL + snake_case, ProblemDetails, OpenAPI.
- [x] `GET /health` kiểm tra kết nối DB (`503` nếu DB down).
- [x] `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (cổng 5090), `backend.http`.
- [x] Thư mục `Auth/`, `Contracts/`, `Data/{Entities,Configurations}`, `Endpoints/`, `Services/`.

Frontend
- [x] Vite 8 + React 19 + TypeScript (template `react-ts`), giữ tên package `fe`.
- [x] Thêm package ([04 §1](04-frontend-react.md#1-package)); Tailwind qua `@tailwindcss/vite`.
- [x] `vite.config.ts`: proxy `/api` + `/health` → `API_PROXY_TARGET`; cấu hình Vitest (jsdom).
- [x] `fe/.env`, `.env.example`, `.gitignore` chặn `.env`.
- [x] Khung `src/app` (App, router, layout có sidebar + tab dưới), trang tạm cho 7 route, trang 404.
- [x] Thanh trạng thái gọi `/health` để thấy FE ↔ BE ↔ DB thông nhau.

**DoD** (đã kiểm tra 28/10/2026)
- ✅ `dotnet run` → `curl http://localhost:5090/health` trả `{"status":"ok","database":"up"}`; `/openapi/v1.json` → 200.
- ✅ `npm run build`, `npm run lint`, `npm test` (4 test) đều qua; `npm run dev` → `http://localhost:5173/health` qua proxy trả `ok`,
  giao diện hiện layout + "Máy chủ: hoạt động" (máy tính: sidebar, màn hẹp: 7 tab dưới).

---

## Phase 1 – Backend: schema + Auth (1 ngày)

**Mục tiêu**: đủ 6 bảng ở [06](06-database.md), đăng ký / đăng nhập / refresh / đăng xuất chạy thật.

- [x] Entity + `IEntityTypeConfiguration` cho 6 bảng; `ConfigureConventions` (decimal, datetime UTC, Guid, `DateOnly`, enum chữ thường); `ITimestamped`.
- [x] Migration `InitialCreate` (3 CHECK constraint); Development tự `MigrateAsync()`.
- [x] `JwtOptions`, `TokenService` (access JWT HS256 15', refresh 64 byte → SHA-256), `RefreshCookie` (cookie `rt` HttpOnly, SameSite=Strict, Path=/api/auth).
- [x] `AuthService`: register (tạo ví + 16 danh mục mặc định – `DefaultData`), login (chống dò email bằng hash giả), refresh xoay vòng + phát hiện dùng lại (cho phép 30 giây cho nhiều tab), logout.
- [x] `/api/me` GET/PUT, `/api/me/password` (thu hồi mọi phiên, cấp phiên mới).
- [x] `RequestValidator` + `ApiExceptionHandler` (400/401/404/409/429 ProblemDetails, thông báo tiếng Việt).
- [x] Rate limit `/api/auth/*` và đổi mật khẩu.
- [x] `backend.Tests` + `Expense.slnx`: `ApiFactory` (MySQL `expense_test`, `FakeTimeProvider`), `AuthApiTests`, `SessionLifetimeTests`, `SchemaTests`.

**DoD** (đã kiểm tra 28/10/2026)
- ✅ Đăng ký → DB có 1 user, 1 ví "Tiền mặt", 16 danh mục; login sai → `401`; refresh dùng lại token cũ sau 30 giây → mọi phiên bị thu hồi.
- ✅ `dotnet test Expense.slnx` – 33/33 pass trên MySQL thật.
- ✅ Chạy thật qua proxy Vite (`:5173/api/...`): register `201` + cookie HttpOnly → `/api/me` → refresh `200` → logout `204` → refresh `401`; DB: 2 token, 0 còn hiệu lực.

Phát hiện khi làm (đã sửa, có test): provider Oracle đọc `date` ra `DateTime` → thêm converter `DateOnly`;
`UseExceptionHandler` xoá header `Set-Cookie` → `/refresh` trả 401 ngay trong endpoint để xoá được cookie.

---

## Phase 2 – Backend: nghiệp vụ (1.5 ngày)

**Mục tiêu**: toàn bộ endpoint [03 §3.2 – §3.6](03-backend-dotnet.md#3-endpoint) theo đúng quy tắc [02](02-nghiep-vu.md).
Phần ví / danh mục / giao dịch làm sớm cùng Phase 3 + 4 (giao diện cần); ngân sách + báo cáo làm sau, trước Phase 5.

- [x] `WalletService` + endpoint: CRUD, số dư tính bằng SQL (2 `GROUP BY`), `total`, chặn xoá ví có giao dịch / ví cuối.
- [x] `CategoryService` + endpoint: CRUD, không đổi `kind`, chặn xoá danh mục đang dùng (giao dịch hoặc ngân sách).
- [x] `TransactionService` + endpoint: tạo/sửa/xoá 3 loại (đổi được cả `kind`), lọc + tìm + phân trang + `totals`, xuất CSV (BOM, chống injection).
- [x] `AppClock` – "hôm nay" / "tháng này" theo `App__TimeZone` (mặc định `Asia/Ho_Chi_Minh`), không theo UTC.
- [x] `BudgetService` + endpoint: upsert theo (tháng, danh mục chi), `spent/remaining/percent/status`, xoá, sao chép tháng (chỉ thêm danh mục chưa có, bỏ qua danh mục đã lưu trữ).
- [x] `ReportService` + endpoint: summary (chi trung bình chia số ngày **đã qua** của khoảng đang diễn ra), by-category, trend `day|month` (đủ kỳ, kỳ trống = 0).
- [x] Mọi truy vấn lọc `user_id`; id của user khác → `404` (trong form: "Không tìm thấy ví").
- [x] Test: `WalletCategoryApiTests` (13), `TransactionApiTests` (22) – số dư sau chuyển tiền, lưu trữ, quyền sở hữu, validate, lọc, CSV;
  `BudgetReportApiTests` (13) – ngưỡng trạng thái, upsert, sao chép, quyền sở hữu, báo cáo loại chuyển tiền, đủ kỳ, giới hạn khoảng.

**DoD**
- ✅ Kịch bản: ví A 1 000 000 → chi 200 000 → chuyển 300 000 sang ví B → số dư A = 500 000, B = 300 000, tổng = 800 000 (`DoDScenario_…`).
- ✅ Ngân sách Ăn uống 1 000 000, chi 850 000 → `percent 85`, `status warning` (`DoDScenario_Budget85Percent_IsWarning`).
- ✅ `dotnet test` – 82/82 pass.
- ✅ Chạy thật (28/10/2026, backend + MySQL qua proxy Vite): ngân sách / summary / by-category khớp truy vấn SQL viết tay trên cùng dữ liệu
  (chi 242 000, thu 15 000 000, chuyển 300 000 không tính; Ăn uống 85.1% `warning`, Đi lại 105% `over`).

---

## Phase 3 – Frontend nền tảng + Đăng nhập (1 ngày)

**Mục tiêu**: đăng nhập được, F5 vẫn giữ phiên, layout hoàn chỉnh.

- [x] `lib/api.ts`: base URL, Bearer, `ApiError` từ ProblemDetails, refresh single-flight khi 401, mất mạng → thông báo tiếng Việt.
- [x] `AuthProvider` (access token trong RAM, refresh lúc khởi động), `RequireAuth` / `GuestOnly`, chuyển về `?next=` (chặn chuyển hướng ra ngoài).
- [x] `LoginPage`, `RegisterPage` (react-hook-form + zod, lỗi server map vào từng ô – `lib/forms.ts`).
- [x] Component: `Button`, `IconButton`, `TextField`, `SelectField`, `MoneyInput`, `Dialog`, `ConfirmDialog`, `Card`, `Money`, `EmptyState`, `Spinner`, `Alert`, `CategoryIcon`, `ColorPicker`, `IconPicker`.
- [x] `lib/format.ts` (tiền, ngày, khoảng tháng) + test.
- [x] `SettingsPage`: đổi tên / tiền tệ / mật khẩu, đăng xuất.

**DoD** (đã kiểm tra 28/10/2026 trên Edge)
- ✅ Đăng ký → vào thẳng trang định vào; F5 vẫn đăng nhập; đăng xuất → về `/login`, mở lại trang cần đăng nhập → `/login?next=…`.
- ✅ `npm test` pass (api refresh, form validate, chuyển hướng).

---

## Phase 4 – Frontend: Ví + Danh mục + Giao dịch (1.5 ngày)

**Mục tiêu**: dùng app hằng ngày được – **MVP**.

- [x] Trang Ví: thẻ số dư, tổng tài sản, form thêm/sửa (màu theo loại), lưu trữ / dùng lại, xoá (hiện lỗi 409 dễ hiểu).
- [x] Trang Danh mục: tab Chi/Thu (trên URL), chọn icon (51 icon) + màu, lưu trữ.
- [x] `TransactionFormDialog`: 3 tab Chi / Thu / Chuyển, ô tiền tự định dạng (focus sẵn), chọn danh mục bằng ô icon, nhớ ví dùng gần nhất, mở từ mọi trang (nút sidebar / nút "+" nổi trên điện thoại), sửa + xoá.
- [x] Trang Giao dịch: bộ lọc (tháng / khoảng ngày, loại, ví, danh mục, tìm ghi chú) lưu trên URL, nhóm theo ngày, tổng của bộ lọc, phân trang, xuất CSV.
- [x] Invalidate cache đúng bảng [04 §5](04-frontend-react.md#5-state--cache-tanstack-query) (`invalidateLedger`).

**DoD** (đã kiểm tra 28/10/2026 – Playwright điều khiển Edge, backend + MySQL thật)
- ✅ Ví VCB 1 000 000 → chi 200 000 → chuyển 300 000 sang Tiền mặt → thu 15 000 000 → Tiền mặt chi 35 000:
  VCB = 15 500 000, Tiền mặt = 265 000, tổng 15 765 000 – khớp ngay sau mỗi thao tác.
- ✅ CSV: UTF-8 BOM, đủ 4 dòng, tiếng Việt đúng.
- ✅ Điện thoại 360 / 390 px: không cuộn ngang, không số tiền nào bị cắt.
- ✅ `npm test` – 43/43; `npm run build`, `npm run lint` qua.

Lỗi phát hiện khi chạy thử (đã sửa, có test hồi quy):
1. Hộp thoại cướp focus khỏi ô số tiền (`querySelector` với danh sách selector trả phần tử đầu theo thứ tự DOM) → `onBlur` cũ xoá trắng ô tiền khi **sửa** giao dịch.
2. "Bỏ lọc" ngay sau khi gõ tìm kiếm: ô tìm kiếm (chờ 300 ms) ghi đè lại URL cũ (`setSearchParams(prev)` dùng URL lúc render) → bộ lọc loại quay lại, CSV thiếu dòng.
3. Rate limit áp cho cả `/api/auth/refresh` → mở / F5 trang > 10 lần/phút (hoặc nhiều người chung IP) bị 429 và văng khỏi phiên. Nay chỉ giới hạn đăng ký / đăng nhập / đổi mật khẩu.
4. Giao diện: số tiền dài xuống dòng tách "₫"; 3 ô tổng bị cắt số ở 390 px → 2 cột trên điện thoại.

Còn lại cho Phase 6: bundle JS ~600 kB (Vite cảnh báo > 500 kB) → tách code theo route (sau Phase 5: ~980 kB).

---

## Phase 5 – Frontend: Tổng quan + Báo cáo + Ngân sách (1 ngày)

- [x] Dashboard: 4 thẻ (thu / chi / chênh lệch tháng này, tổng số dư), tròn chi theo danh mục (tháng này), cột thu-chi 6 tháng,
  5 giao dịch gần nhất (bấm để sửa), ngân sách tháng này xếp theo `percent` giảm dần (tối đa 4).
- [x] Trang Báo cáo: chọn khoảng (tháng này / tháng trước / 3 tháng / năm nay / tuỳ chọn – lưu trên URL), tab Chi / Thu,
  tròn (top 5 + "Khác") + bảng chi tiết, cột thu-chi (≤ 31 ngày theo ngày, dài hơn theo tháng) + "Xem dạng bảng".
- [x] Trang Ngân sách: chọn tháng (URL), 3 ô tổng, thanh tiến độ 3 màu **kèm icon + chữ** trạng thái, thêm / sửa / xoá, sao chép tháng trước.
- [x] Trạng thái rỗng có hướng dẫn ("Chưa có giao dịch – thêm khoản chi đầu tiên", "Chưa có ngân sách… / Chép từ tháng trước").
- [x] Component dùng chung: `StatTile`, `Segmented` (tab Chi / Thu), `TransactionRow`; `features/reports/charts.tsx` (Recharts).
- [x] Test: `dashboard.test.tsx`, `budgets.test.tsx`, `reports.test.tsx`, thêm `format.test.ts` (nhãn trục / kỳ).

**DoD** (đã kiểm tra 29/10/2026 – Playwright điều khiển Edge, backend + MySQL thật)
- ✅ Số trên dashboard khớp truy vấn SQL [06 §5](06-database.md#5-truy-vấn-tiêu-biểu) cùng dữ liệu: thu 15 000 000, chi 1 092 000,
  tổng số dư 14 908 000; chi theo danh mục Mua sắm 850 000 / Ăn uống 200 000 / Đi lại 42 000.
- ✅ Ghi khoản chi 850 000 vào Mua sắm (ngân sách 1 000 000) từ nút sidebar → trang Ngân sách tự cập nhật 85% "Sắp hết" (invalidate đúng).
- ✅ Sao chép tháng 11 → 12: "Đã thêm 1 ngân sách"; khoảng tuỳ chọn > 366 ngày → hiện "Khoảng ngày tối đa 366 ngày".
- ✅ Chế độ tối; điện thoại 360 px: không cuộn ngang.
- ✅ `npm test` – 60/60; `npm run build`, `npm run lint` qua.

Phát hiện khi chạy thử (đã sửa): cột biểu đồ theo ngày không hiện trên điện thoại lúc chụp (hiệu ứng vẽ chưa xong) → tắt hiệu ứng
(đổi bộ lọc cũng không vẽ lại từ 0); chú thích biểu đồ tròn bị kéo giãn trên màn rộng; bảng danh mục chật ở 360 px → ẩn cột "Số giao dịch".
Lỗi 400 không có `detail` (VD khoảng ngày sai) trước hiện "Dữ liệu không hợp lệ" → nay hiện lỗi đầu tiên trong `errors`.

---

## Phase 6 – Hoàn thiện, test, phát hành (1 ngày)

- [x] Tách code theo route (`React.lazy`) + chunk `react` / `vendor`: file JS ban đầu 980 kB → 55 kB code app + 314 kB `react` + 169 kB `vendor`
  (gzip ~170 kB); Recharts (390 kB) chỉ tải ở Tổng quan / Báo cáo; hết cảnh báo > 500 kB. Chunk lỗi sau cập nhật → "Tải lại trang".
- [x] Backend phục vụ giao diện (`Hosting/WebHosting.MapSpa`): `assets/*` cache 1 năm, `index.html` `no-cache`, fallback SPA, `/api/*` lạ → 404 JSON;
  `dotnet publish` tự `npm run build` và chép `fe/dist` → `publish/wwwroot` (`-p:SkipFrontend=true` để bỏ qua).
- [x] `UseWindowsService()`; `.env` tìm theo thư mục của exe (service chạy ở System32), biến môi trường thật thắng `.env`;
  `scripts/install-service.ps1` / `uninstall-service.ps1` (LocalService, delayed-auto, tự khởi động lại, ép Production); log ra file theo ngày (giữ 14 ngày).
- [x] Production: tắt OpenAPI, tự migrate khi khởi động (`appsettings.Production.json`), security header (CSP, `nosniff`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy`) trên mọi response; `Jwt__RefreshCookieSecure` – xem [07 §2](07-van-hanh.md#2-file-env-cạnh-backendexe).
- [x] Rà soát 7 trang + login / register bằng axe-core (WCAG 2.1 AA) ở chế độ sáng và tối, 360 px, bàn phím (Tab / Enter / Esc).
- [x] Test thủ công toàn bộ luồng trên bản publish (`:5090`, không Vite); tài liệu [07 – Vận hành](07-van-hanh.md).
- [x] Test: `HostingTests` (12 – fallback SPA, cache, 404 API, security header, OpenAPI tắt, `.env`, log file), FE thêm test trả focus khi đóng hộp thoại.

**DoD** (đã kiểm tra 29/10/2026)
- ✅ `dotnet publish -c Release -o publish` → chạy `publish\backend.exe` **từ `C:\`** với `ASPNETCORE_ENVIRONMENT=Production`: content root = thư mục publish,
  migration "already up to date", `/health` ok, `/reports` (F5 link sâu) trả `index.html`, `/openapi/v1.json` → 404, `/api/khong-co` → 404 JSON,
  log ghi `publish\logs\backend-20261029.log`.
- ✅ Luồng Phase 4 (đăng ký → ví → 4 giao dịch → lọc → sửa → CSV → danh mục → F5 → cài đặt → đăng xuất / đăng nhập lại → 360 px) chạy đúng trên `:5090`,
  **không có lỗi CSP** trong console.
- ✅ axe-core: 0 vi phạm ở 9 màn hình × sáng / tối + hộp thoại; 360 px không trang nào cuộn ngang.
- ✅ `dotnet test` 94/94; `npm test` 61/61; `npm run build`, `npm run lint` qua.
- ⚠️ Chưa cài thử Windows Service trên máy này (cần PowerShell quyền Administrator) – script đã kiểm tra cú pháp, chạy theo [07 §4](07-van-hanh.md#4-windows-service).

Lỗi phát hiện khi rà soát (đã sửa):
1. Tương phản chữ dưới chuẩn AA: nút chính / link `brand-600` (3,8:1), chữ khoản thu `#059669` (3,8:1), chữ "VND" trong ô tiền `slate-400` (2,6:1),
   tổng theo ngày ở chế độ tối → `brand-700`, `#047857`, `slate-500`, `dark:text-slate-400`.
2. Đóng hộp thoại (Esc) không trả focus về nút đã mở (focus rơi về `body`): ô `autoFocus` được focus trước effect nên "phần tử trước đó" ghi nhận sai
   → lấy từ `relatedTarget` của lần focus đầu tiên vào hộp thoại; có test hồi quy.
3. Chạy `backend.exe` từ thư mục khác → không thấy `wwwroot` (content root = thư mục hiện tại) → bản publish dùng thư mục của exe.
4. `dotnet-tools.json` (manifest dev) bị chép vào bản publish → loại ra; log tiếng Việt trên console vỡ dấu → `Console.OutputEncoding = UTF8`.

---

## Phase 7 – Mở rộng (tuỳ chọn)

| Hạng mục | Ghi chú |
|---|---|
| Triển khai **Azure** | App Service (.NET 10) + Azure Database for MySQL Flexible Server; `ConnectionStrings__Default`, `Jwt__Secret` đặt trong App Settings / Key Vault; deploy bằng `az webapp deploy` gói zip từ `dotnet publish` |
| Giao dịch định kỳ | Bảng `recurring_rules`, `BackgroundService` sinh giao dịch hằng ngày |
| Ảnh hoá đơn | Azure Blob Storage, SAS upload trực tiếp từ trình duyệt |
| Nhập CSV | Map cột, xem trước, chống trùng |
| Nhiều tiền tệ | Tiền tệ theo ví + bảng tỷ giá |
| PWA | Cài lên màn hình chính, ghi giao dịch offline rồi đồng bộ |
