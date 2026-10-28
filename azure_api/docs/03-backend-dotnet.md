# 03 – Backend .NET

ASP.NET Core 10 **Minimal API**, project `backend/backend.csproj` (`Sdk="Microsoft.NET.Sdk.Web"`), namespace gốc `backend`.

## 1. Package

| Package | Dùng cho |
|---|---|
| `MySql.EntityFrameworkCore` 10.0.x | Provider EF Core cho MySQL (Oracle) |
| `Microsoft.EntityFrameworkCore.Relational` 10.0.x | Ghim phiên bản (tránh xung đột MSB3277 với provider) |
| `Microsoft.EntityFrameworkCore.Design` | `dotnet ef migrations` (local tool `dotnet-ef` trong `dotnet-tools.json`) |
| `EFCore.NamingConventions` | Tên bảng/cột `snake_case` |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Xác thực access token |
| `Microsoft.AspNetCore.OpenApi` | `/openapi/v1.json` |
| `BCrypt.Net-Next` | Băm mật khẩu |
| `DotNetEnv` | Nạp `backend/.env` vào biến môi trường |
| `Microsoft.Extensions.Hosting.WindowsServices` | Chạy như Windows Service (`UseWindowsService`) |

Test (`backend.Tests`): `xunit` 2.9, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`).
Test chạy trên **MySQL thật**, database `expense_test` (xoá + migrate lại mỗi lần chạy) – cần MySQL đang chạy, xem [06 §2](06-database.md#2-khởi-tạo-database-làm-1-lần).

## 2. Cấu trúc thư mục

```
backend/
├── Program.cs                 # cấu hình DI, middleware, map endpoint
├── appsettings.json           # giá trị mặc định KHÔNG bí mật
├── appsettings.Development.json
├── appsettings.Production.json # log ra file, tự migrate khi khởi động
├── .env / .env.example        # bí mật + cấu hình local
├── backend.http               # request mẫu (VS Code REST Client / Rider / VS)
├── Auth/                      # JwtOptions, TokenService, RefreshCookie, ClaimsPrincipalExtensions
├── Contracts/                 # DTO request/response (record)
├── Data/
│   ├── AppDbContext.cs
│   ├── Entities/              # User, RefreshToken, Wallet, Category, Transaction, Budget
│   ├── Configurations/        # IEntityTypeConfiguration<T>
│   ├── DefaultData.cs         # ví + danh mục mặc định khi đăng ký
│   └── Migrations/
├── Endpoints/                 # *Endpoints.cs – mỗi nhóm route 1 file, RequestValidator, ApiExceptionHandler
├── Hosting/                   # EnvFile (.env cạnh exe), FileLogger, WebHosting (wwwroot + fallback SPA + security header)
└── Services/                  # AuthService, WalletService, CategoryService, TransactionService, BudgetService, ReportService
```

Quy ước: endpoint **mỏng** (đọc tham số → gọi service → trả `TypedResults`); toàn bộ quy tắc ở [02](02-nghiep-vu.md) nằm trong `Services/`.
Lỗi nghiệp vụ ném `ApiProblemException(status, title, errors?)` → `ApiExceptionHandler` đổi sang `ProblemDetails`.

## 3. Endpoint

Tất cả trả JSON `camelCase`; ngày `yyyy-MM-dd`; tháng `yyyy-MM`; tiền là **số JSON** (decimal, tối đa 2 chữ số thập phân).
🔒 = cần `Authorization: Bearer <access>`.

### 3.1 Hệ thống & xác thực

| Method | Route | Mô tả |
|---|---|---|
| GET | `/health` | `{status, database}` – `503` nếu DB down |
| POST | `/api/auth/register` | `{email, password, displayName}` → `201` `AuthResponse` + cookie `rt` |
| POST | `/api/auth/login` | `{email, password}` → `200` `AuthResponse` + cookie `rt`; sai → `401` |
| POST | `/api/auth/refresh` | Cookie `rt` → `200` `AuthResponse` + cookie `rt` mới; không hợp lệ → `401` + xoá cookie |
| POST | `/api/auth/logout` | Thu hồi `rt` hiện tại, xoá cookie → `204` |
| GET 🔒 | `/api/me` | `UserDto` |
| PUT 🔒 | `/api/me` | `{displayName, currency}` → `UserDto` |
| PUT 🔒 | `/api/me/password` | `{currentPassword, newPassword}` → `200` `AuthResponse` + cookie `rt` mới; thu hồi **mọi** phiên cũ (thiết bị khác phải đăng nhập lại); sai mật khẩu hiện tại → `400` |

```jsonc
// AuthResponse
{ "accessToken": "eyJ…", "expiresIn": 900,
  "user": { "id": "0b9c…", "email": "an@vidu.vn", "displayName": "An", "currency": "VND", "createdAt": "2026-10-28T09:47:12Z" } }
```

Cookie refresh: `rt=<base64url 64 byte>; HttpOnly; SameSite=Strict; Path=/api/auth; Max-Age=<RefreshTokenDays>`, thêm `Secure` khi chạy HTTPS
(cấu hình `Jwt__RefreshCookieSecure`).

- `/refresh` lỗi → `401` **và** xoá cookie (trả lỗi ngay trong endpoint – `UseExceptionHandler` sẽ xoá mất header `Set-Cookie` nếu ném exception).
- Refresh bằng token vừa bị thay ≤ 30 giây (`AuthService.ReuseGracePeriod`) → vẫn cấp phiên mới (nhiều tab mở cùng lúc); quá 30 giây → thu hồi mọi phiên.
- Đăng nhập sai email hay sai mật khẩu đều trả cùng thông điệp, và email không tồn tại vẫn chạy BCrypt với hash giả → không dò được email qua thời gian phản hồi.
- Rate limit (10/phút/IP) chỉ áp cho **register, login, đổi mật khẩu** – không áp cho refresh / logout (mỗi lần mở / F5 trang đều refresh; token 64 byte ngẫu nhiên không dò được).

### 3.2 Ví 🔒

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/wallets?includeArchived=false` | `{ items: WalletDto[], total }` – `total` = tổng số dư ví đang hoạt động |
| POST | `/api/wallets` | `{name, type, openingBalance, color}` → `201` `WalletDto` |
| PUT | `/api/wallets/{id}` | `{name, type, openingBalance, color, isArchived}` → `WalletDto` |
| DELETE | `/api/wallets/{id}` | `204`; đã có giao dịch / ví cuối → `409` |

```jsonc
// WalletDto
{ "id": 3, "name": "Vietcombank", "type": "bank", "openingBalance": 5000000, "balance": 3250000, "color": "#2563eb", "isArchived": false }
```

### 3.3 Danh mục 🔒

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/categories?kind=&includeArchived=false` | `CategoryDto[]` sắp theo `sortOrder, name` |
| POST | `/api/categories` | `{name, kind, icon, color}` → `201` |
| PUT | `/api/categories/{id}` | `{name, icon, color, isArchived}` (không đổi `kind`) |
| DELETE | `/api/categories/{id}` | `204`; đang dùng → `409` |

### 3.4 Giao dịch 🔒

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/transactions?from&to&walletId&categoryId&kind&q&page&pageSize` | `PagedTransactions` ([02 §4.1](02-nghiep-vu.md#41-lọc--tìm-kiếm-get-apitransactions)) |
| GET | `/api/transactions/{id}` | `TransactionDto` |
| POST | `/api/transactions` | `TransactionRequest` → `201` `TransactionDto` |
| PUT | `/api/transactions/{id}` | `TransactionRequest` → `TransactionDto` |
| DELETE | `/api/transactions/{id}` | `204` |
| GET | `/api/transactions/export?from&to&…` | `text/csv` (cùng bộ lọc, không phân trang, tối đa 10 000 dòng) |

```jsonc
// TransactionRequest
{ "kind": "expense", "walletId": 3, "toWalletId": null, "categoryId": 12, "amount": 45000, "occurredOn": "2026-10-28", "note": "Phở bò" }

// TransactionDto – kèm sẵn tên/màu để FE không phải tra cứu
{ "id": 120, "kind": "expense", "amount": 45000, "occurredOn": "2026-10-28", "note": "Phở bò",
  "wallet":   { "id": 3,  "name": "Vietcombank", "color": "#2563eb" },
  "toWallet": null,
  "category": { "id": 12, "name": "Ăn uống", "icon": "utensils", "color": "#f97316" },
  "createdAt": "2026-10-28T05:12:00Z" }

// PagedTransactions
{ "items": [ /* TransactionDto */ ], "page": 1, "pageSize": 20, "totalItems": 57, "totalPages": 3,
  "totals": { "income": 15000000, "expense": 6230000 } }
```

### 3.5 Ngân sách 🔒

| Method | Route | Mô tả |
|---|---|---|
| GET | `/api/budgets?month=2026-10` | `BudgetDto[]` (mặc định tháng hiện tại) |
| PUT | `/api/budgets` | `{month, categoryId, amount}` → upsert, `200 BudgetDto` |
| DELETE | `/api/budgets/{id}` | `204` |
| POST | `/api/budgets/copy` | `{from, to}` → `{ created: 4 }` |

```jsonc
// BudgetDto
{ "id": 7, "month": "2026-10", "category": { "id": 12, "name": "Ăn uống", "icon": "utensils", "color": "#f97316" },
  "amount": 3000000, "spent": 2550000, "remaining": 450000, "percent": 85.0, "status": "warning" }
```

- `month` dạng `yyyy-MM`, từ 01/2000 tới tháng hiện tại + 12; danh sách sắp theo thứ tự danh mục (dashboard tự sắp theo `percent`).
- `categoryId` phải là danh mục **chi** của user (`"Hãy chọn danh mục chi"` / `"Không tìm thấy danh mục"`);
  danh mục đã lưu trữ không tạo ngân sách mới được nhưng ngân sách sẵn có vẫn sửa được.
- `status` xét trên `percent` đã làm tròn 1 chữ số (79.96 → 80.0 → `warning`) để khớp con số người dùng thấy; `remaining` có thể âm.
- `copy`: `from ≠ to`; bỏ qua danh mục tháng đích đã có và danh mục đã lưu trữ.

### 3.6 Báo cáo 🔒

| Method | Route | Kết quả |
|---|---|---|
| GET | `/api/reports/summary?from&to` | `{from, to, income, expense, net, transactionCount, dailyAverageExpense}` |
| GET | `/api/reports/by-category?from&to&kind=expense` | `[{category, total, percent, count}]` |
| GET | `/api/reports/trend?from&to&groupBy=month` | `[{period: "2026-10", income, expense}]` (`day` → `period: "2026-10-28"`) |

- `from`/`to` giống bộ lọc giao dịch: bỏ trống → tháng hiện tại; chỉ có 1 đầu → trọn tháng của đầu đó; `to − from ≤ 366` (`groupBy=day`: ≤ 92).
- `transactionCount` chỉ đếm thu + chi (chuyển tiền không tính, giống `income`/`expense`).
- `dailyAverageExpense` = `expense / số ngày`, làm tròn 2 chữ số; khoảng **đang diễn ra** (chứa hôm nay) chỉ chia tới hôm nay –
  ngày 28 xem "tháng này" chia 28 chứ không chia 31.
- `by-category`: `kind` = `expense` (mặc định) | `income`; `percent` trên tổng của `kind`; sắp `total` giảm dần.
- `trend`: kỳ đầu tính từ `from` (khoảng bắt đầu giữa tháng thì kỳ đó chỉ gồm phần trong khoảng).

## 4. Lỗi (ProblemDetails – RFC 9457)

| Tình huống | Status | Ví dụ `title` |
|---|---|---|
| Dữ liệu không hợp lệ | `400` + `errors: { field: [msg] }` | "Dữ liệu không hợp lệ" |
| Tham số sai kiểu (`?page=abc`) | `400` | "Tham số không hợp lệ" |
| Chưa đăng nhập / token hết hạn | `401` | – |
| Không tồn tại hoặc thuộc user khác | `404` | "Không tìm thấy giao dịch" |
| Trùng tên, đang dùng, ví cuối | `409` (trùng tên kèm `errors.name`) | "Ví đã có giao dịch – hãy lưu trữ thay vì xoá" |
| Đăng nhập / đăng ký / đổi mật khẩu quá nhanh | `429` | – |
| Lỗi không lường trước | `500` (ẩn chi tiết ở Production) | "Lỗi máy chủ" |

## 5. Cấu hình

| Khoá (`.env` dùng `__`) | Mặc định | Ghi chú |
|---|---|---|
| `ASPNETCORE_URLS` | `http://0.0.0.0:5090` | |
| `ConnectionStrings__Default` | – (bắt buộc) | `Server=localhost;Port=3306;Database=expense;User=expense;Password=…` |
| `Jwt__Issuer` / `Jwt__Audience` | `expense-api` / `expense-web` | |
| `Jwt__Secret` | – (bắt buộc, ≥ 32 ký tự) | Validate lúc khởi động |
| `Jwt__AccessTokenMinutes` | `15` | |
| `Jwt__RefreshTokenDays` | `30` | |
| `Jwt__RefreshCookieSecure` | `false` (dev) | `true` khi chạy HTTPS |
| `RateLimit__AuthPermitPerMinute` | `10` | Đăng ký / đăng nhập / đổi mật khẩu |
| `App__TimeZone` | `Asia/Ho_Chi_Minh` | "Hôm nay" / "tháng này" (mặc định bộ lọc, giới hạn ngày giao dịch) – validate lúc khởi động |
| `Cors__AllowedOrigins__0` | (trống) | Chỉ cần khi FE ở origin khác; trống = không bật CORS |
| `Logging__File__Enabled` / `__Path` / `__RetainDays` | `false` (Production: `true`) / `logs` / `14` | Log ra `logs/backend-YYYYMMDD.log` cạnh exe |
| `Database__MigrateOnStartup` | `false` (Production: `true`) | Development luôn tự migrate |

`.env` được tìm từ thư mục của `backend.exe` đi lên, rồi từ thư mục hiện tại; biến môi trường thật thắng giá trị trong file (`NoClobber`).

## 6. Ghi chú triển khai

- `TimeProvider` được inject → test điều khiển được "hôm nay" (tháng hiện tại, giới hạn ngày, token hết hạn).
- `DateOnly` ↔ cột `date`; `DateTime` UTC ↔ `datetime(6)` (converter `Kind=Utc` khi đọc).
- Truy vấn tổng dùng `SUM` phía DB, nhóm theo `category_id` / tháng; số dư ví tính bằng 2 truy vấn `GROUP BY` (nguồn + đích) rồi ghép trong RAM.
- `MigrateAsync()` tự chạy ở Development và khi `Database:MigrateOnStartup=true` (bật sẵn ở Production); muốn tự kiểm soát thì tắt và chạy `dotnet ef database update` hoặc `dotnet ef migrations script <from> <to>`
  (**không** dùng `--idempotent`: provider Oracle sinh khối `IF … BEGIN … END` MySQL không chạy được ngoài stored procedure).
- `DateOnly` cần converter riêng (`DateOnlyConverter` trong `AppDbContext`) – provider Oracle đọc cột `date` ra `DateTime` và ném `InvalidCastException`.
- Phục vụ SPA (`Hosting/WebHosting.MapSpa`): `UseDefaultFiles` + `UseStaticFiles` (`assets/*` cache 1 năm `immutable`, còn lại `no-cache`),
  `MapFallback("/api/{**path}")` → `404` JSON, `MapFallbackToFile("index.html")` cho mọi đường dẫn không phải file (F5 ở `/reports` vẫn chạy).
- Content root = thư mục của exe khi là bản publish (có `wwwroot/index.html` cạnh exe) hoặc Windows Service → chạy từ đâu cũng được.
- Security header gắn bằng `Response.OnStarting` (giữ được cả khi `UseExceptionHandler` xoá header lúc lỗi); CSP xem [07 §6](07-van-hanh.md#6-bảo-mật-khi-chạy-production).
- `dotnet publish` gọi target `PublishFrontend` (`npm ci` nếu thiếu `node_modules`, `npm run build`, chép `fe/dist/**` → `wwwroot/`); `-p:SkipFrontend=true` để bỏ qua.
