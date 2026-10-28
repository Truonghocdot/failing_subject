# 06 – Database (MySQL)

MySQL chạy sẵn ở **`localhost:3306`** (trên máy dev là container `mysql-dev` – Docker **chỉ** giữ dịch vụ DB, ứng dụng không đóng gói Docker).
Schema do **EF Core migration** sinh ra (`backend/Data/Migrations/*_InitialCreate`); DDL bên dưới là bản tương đương để đọc / review.
Tên index thật do EF đặt theo quy ước `ix_<bảng>_<cột>` (unique cũng là `ix_…`), khoá ngoại `fk_<bảng>_<bảng cha>_<cột>`.

## 1. Quy ước

| Quy ước | Giá trị |
|---|---|
| Database / user | `expense` / `expense` – user chỉ có quyền trên `expense.*` |
| Charset / collation | `utf8mb4` / `utf8mb4_unicode_ci` (so sánh không phân biệt hoa thường, tiếng Việt có dấu) |
| Tên bảng, cột | `snake_case` số nhiều (`EFCore.NamingConventions`) |
| Khoá chính | `users.id` = `char(36)` (GUID, nằm trong JWT `sub`); các bảng khác `bigint AUTO_INCREMENT` |
| Thời điểm | `datetime(6)` **UTC** (`created_at`, `updated_at`, `expires_at`…) |
| Ngày nghiệp vụ | `date` (`occurred_on`), tháng ngân sách lưu là `date` ngày 01 (`2026-10-01`) |
| Tiền | `decimal(15,2)` – không bao giờ dùng `float/double` |
| Enum | `varchar` chữ thường (`expense`, `bank`…) – dễ đọc khi truy vấn tay |
| Xoá | Xoá user → `CASCADE` toàn bộ dữ liệu của user; giữa các bảng con dùng `RESTRICT` (service kiểm tra trước và trả `409`) |

## 2. Khởi tạo database (làm 1 lần)

Chạy bằng tài khoản root (trên máy dev: mật khẩu root lấy từ biến môi trường của container, không gõ ra màn hình):

```bash
docker exec -i mysql-dev sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD"' <<'SQL'
CREATE DATABASE IF NOT EXISTS expense CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'expense'@'%' IDENTIFIED BY '<mật khẩu trong backend/.env>';
GRANT ALL PRIVILEGES ON expense.* TO 'expense'@'%';
-- DB cho backend.Tests (test tự xoá + tạo lại, không bao giờ đụng DB expense)
GRANT ALL PRIVILEGES ON `expense_test`.* TO 'expense'@'%';
FLUSH PRIVILEGES;
SQL
```

Sau đó tạo bảng: `cd backend && dotnet tool restore && dotnet ef database update` (Development cũng tự migrate khi `dotnet run`).

## 3. ERD

```
users 1───* refresh_tokens
  │
  ├──1───* wallets ◀──────────────┐ wallet_id (nguồn)
  │           ▲                   │
  │           └── to_wallet_id ── transactions *───0..1 categories
  │                (chỉ transfer)      │                    ▲
  ├──1───* categories                  │                    │
  │                                    user_id              │
  └──1───* budgets *──────────────────────────────────────1─┘ (chỉ danh mục expense)
```

## 4. Bảng

### 4.1 `users`

```sql
CREATE TABLE users (
  id            CHAR(36)     NOT NULL,
  email         VARCHAR(254) NOT NULL,           -- lower(trim)
  password_hash VARCHAR(100) NOT NULL,           -- BCrypt
  display_name  VARCHAR(100) NOT NULL,
  currency      CHAR(3)      NOT NULL DEFAULT 'VND',
  created_at    DATETIME(6)  NOT NULL,
  updated_at    DATETIME(6)  NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ix_users_email (email)
);
```

### 4.2 `refresh_tokens`

```sql
CREATE TABLE refresh_tokens (
  id               BIGINT      NOT NULL AUTO_INCREMENT,
  user_id          CHAR(36)    NOT NULL,
  token_hash       CHAR(64)    NOT NULL,          -- SHA-256 hex, không lưu token gốc
  expires_at       DATETIME(6) NOT NULL,
  created_at       DATETIME(6) NOT NULL,
  revoked_at       DATETIME(6) NULL,
  replaced_by_hash CHAR(64)    NULL,              -- != NULL: đã xoay vòng → dùng lại = bị đánh cắp
  user_agent       VARCHAR(200) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ix_refresh_tokens_token_hash (token_hash),
  KEY ix_refresh_tokens_user_id (user_id),
  CONSTRAINT fk_refresh_tokens_users FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
```

### 4.3 `wallets`

```sql
CREATE TABLE wallets (
  id              BIGINT        NOT NULL AUTO_INCREMENT,
  user_id         CHAR(36)      NOT NULL,
  name            VARCHAR(50)   NOT NULL,
  type            VARCHAR(10)   NOT NULL,          -- cash | bank | ewallet | credit
  opening_balance DECIMAL(15,2) NOT NULL DEFAULT 0,
  color           CHAR(7)       NOT NULL,          -- #RRGGBB
  is_archived     TINYINT(1)    NOT NULL DEFAULT 0,
  sort_order      INT           NOT NULL DEFAULT 0,
  created_at      DATETIME(6)   NOT NULL,
  updated_at      DATETIME(6)   NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ix_wallets_user_id_name (user_id, name),
  CONSTRAINT fk_wallets_users FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
```

### 4.4 `categories`

```sql
CREATE TABLE categories (
  id          BIGINT      NOT NULL AUTO_INCREMENT,
  user_id     CHAR(36)    NOT NULL,
  kind        VARCHAR(10) NOT NULL,              -- expense | income
  name        VARCHAR(50) NOT NULL,
  icon        VARCHAR(40) NOT NULL,              -- tên icon lucide
  color       CHAR(7)     NOT NULL,
  is_archived TINYINT(1)  NOT NULL DEFAULT 0,
  sort_order  INT         NOT NULL DEFAULT 0,
  created_at  DATETIME(6) NOT NULL,
  updated_at  DATETIME(6) NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ix_categories_user_id_kind_name (user_id, kind, name),
  CONSTRAINT fk_categories_users FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);
```

### 4.5 `transactions`

```sql
CREATE TABLE transactions (
  id           BIGINT        NOT NULL AUTO_INCREMENT,
  user_id      CHAR(36)      NOT NULL,
  kind         VARCHAR(10)   NOT NULL,           -- expense | income | transfer
  wallet_id    BIGINT        NOT NULL,           -- ví nguồn
  to_wallet_id BIGINT        NULL,               -- chỉ transfer
  category_id  BIGINT        NULL,               -- chỉ expense / income
  amount       DECIMAL(15,2) NOT NULL,           -- luôn > 0
  occurred_on  DATE          NOT NULL,
  note         VARCHAR(500)  NULL,
  created_at   DATETIME(6)   NOT NULL,
  updated_at   DATETIME(6)   NOT NULL,
  PRIMARY KEY (id),
  KEY ix_transactions_user_id_occurred_on (user_id, occurred_on, id),   -- danh sách + báo cáo theo khoảng ngày
  KEY ix_transactions_wallet_id (wallet_id),
  KEY ix_transactions_to_wallet_id (to_wallet_id),
  KEY ix_transactions_category_id_occurred_on (category_id, occurred_on), -- ngân sách / báo cáo danh mục
  CONSTRAINT fk_transactions_users      FOREIGN KEY (user_id)      REFERENCES users (id)      ON DELETE CASCADE,
  CONSTRAINT fk_transactions_wallets    FOREIGN KEY (wallet_id)    REFERENCES wallets (id)    ON DELETE RESTRICT,
  CONSTRAINT fk_transactions_to_wallets FOREIGN KEY (to_wallet_id) REFERENCES wallets (id)    ON DELETE RESTRICT,
  CONSTRAINT fk_transactions_categories FOREIGN KEY (category_id)  REFERENCES categories (id) ON DELETE RESTRICT,
  CONSTRAINT ck_transactions_amount   CHECK (amount > 0),
  CONSTRAINT ck_transactions_shape    CHECK (
       (kind IN ('expense','income') AND category_id IS NOT NULL AND to_wallet_id IS NULL)
    OR (kind = 'transfer' AND category_id IS NULL AND to_wallet_id IS NOT NULL AND to_wallet_id <> wallet_id))
);
```

> **Đã kiểm chứng 28/10/2026:** `DELETE FROM users` với user có giao dịch chuyển tiền bị MySQL chặn
> (`ERROR 1451 … fk_transactions_wallets_to_wallet_id … ON DELETE RESTRICT`) vì cascade xoá `wallets` trước `transactions`.
> Xoá user phải làm theo thứ tự trong 1 transaction:
>
> ```sql
> START TRANSACTION;
> SET @u = (SELECT id FROM users WHERE email = '...');
> DELETE FROM transactions WHERE user_id = @u;
> DELETE FROM budgets WHERE user_id = @u;
> DELETE FROM users WHERE id = @u;   -- wallets, categories, refresh_tokens tự CASCADE
> COMMIT;
> ```

### 4.6 `budgets`

```sql
CREATE TABLE budgets (
  id          BIGINT        NOT NULL AUTO_INCREMENT,
  user_id     CHAR(36)      NOT NULL,
  category_id BIGINT        NOT NULL,            -- danh mục kind = expense
  month       DATE          NOT NULL,            -- luôn là ngày 01
  amount      DECIMAL(15,2) NOT NULL,
  created_at  DATETIME(6)   NOT NULL,
  updated_at  DATETIME(6)   NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ix_budgets_user_id_month_category_id (user_id, month, category_id),
  CONSTRAINT fk_budgets_users      FOREIGN KEY (user_id)     REFERENCES users (id)      ON DELETE CASCADE,
  CONSTRAINT fk_budgets_categories FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE RESTRICT,
  CONSTRAINT ck_budgets_amount CHECK (amount > 0)
);
```

## 5. Truy vấn tiêu biểu

```sql
-- Số dư từng ví của 1 user
SELECT w.id, w.name,
       w.opening_balance
       + COALESCE(SUM(CASE WHEN t.wallet_id = w.id AND t.kind = 'income'   THEN t.amount END), 0)
       - COALESCE(SUM(CASE WHEN t.wallet_id = w.id AND t.kind IN ('expense','transfer') THEN t.amount END), 0)
       + COALESCE(SUM(CASE WHEN t.to_wallet_id = w.id THEN t.amount END), 0) AS balance
FROM wallets w
LEFT JOIN transactions t ON t.wallet_id = w.id OR t.to_wallet_id = w.id
WHERE w.user_id = @user
GROUP BY w.id, w.name, w.opening_balance;

-- Chi theo danh mục trong tháng 10/2026
SELECT c.name, SUM(t.amount) AS total, COUNT(*) AS cnt
FROM transactions t JOIN categories c ON c.id = t.category_id
WHERE t.user_id = @user AND t.kind = 'expense' AND t.occurred_on BETWEEN '2026-10-01' AND '2026-10-31'
GROUP BY c.id, c.name
ORDER BY total DESC;
```

## 6. Entity EF Core (tóm tắt)

| Entity | Ghi chú cấu hình |
|---|---|
| `User` | `Id: Guid` → `char(36)`; `Email` unique |
| `RefreshToken` | `TokenHash` unique, `IsActive => RevokedAt == null && ExpiresAt > now` (không map) |
| `Wallet` | `Type: WalletType` enum → `HasConversion<string>()` lưu chữ thường |
| `Category` | `Kind: CategoryKind` enum → string |
| `Transaction` | `Kind: TransactionKind`; `OccurredOn: DateOnly`; `Amount: decimal` `HasPrecision(15,2)`; 2 navigation `Wallet`, `ToWallet` |
| `Budget` | `Month: DateOnly` (ngày 01) |

Áp dụng chung trong `ConfigureConventions`: `decimal` → `(15,2)`, `DateTime` → `datetime(6)` + converter UTC, `Guid` → `char(36)`,
`DateOnly` → `date` (converter qua `DateTime`), 3 enum → `varchar(10)` chữ thường (`LowercaseEnumConverter`).
`CreatedAt` / `UpdatedAt` của entity `ITimestamped` được `AppDbContext.SaveChangesAsync` gán theo `TimeProvider`.

## 6.1 Đối chiếu code / test

| Nội dung | Code | Test |
|---|---|---|
| 6 bảng, index, FK, CHECK | `Data/Configurations/EntityConfigurations.cs`, `Data/Migrations/` | `SchemaTests` (CHECK `amount > 0`, hình dạng transfer, tên ví trùng không phân biệt hoa thường, RESTRICT khi xoá ví đang dùng, enum chữ thường) |
| Dữ liệu mặc định khi đăng ký | `Data/DefaultData.cs` | `AuthApiTests.Register_SeedsDefaultWalletAndCategories` |
| Refresh token (hash, xoay vòng, thu hồi) | `Services/AuthService.cs`, `Auth/TokenService.cs` | `AuthApiTests`, `SessionLifetimeTests` |
| Ngân sách (unique `(user, month, category)`, `spent` theo tháng) | `Services/BudgetService.cs` | `BudgetReportApiTests` |
| Báo cáo (§5 – loại chuyển tiền, gom theo ngày rồi gộp tháng) | `Services/ReportService.cs` | `BudgetReportApiTests` |

## 7. Biến môi trường `.env`

`backend/.env` (không commit – mẫu ở `backend/.env.example`):

```dotenv
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://0.0.0.0:5090
ConnectionStrings__Default=Server=localhost;Port=3306;Database=expense;User=expense;Password=<bí mật>;CharSet=utf8mb4
Jwt__Issuer=expense-api
Jwt__Audience=expense-web
Jwt__Secret=<chuỗi ngẫu nhiên ≥ 32 ký tự>
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=30
Jwt__RefreshCookieSecure=false
RateLimit__AuthPermitPerMinute=10
App__TimeZone=Asia/Ho_Chi_Minh
```

`fe/.env` (không chứa bí mật – xem [04 §7](04-frontend-react.md#7-cấu-hình-env-vite)):

```dotenv
VITE_APP_NAME=Sổ Chi Tiêu
VITE_API_BASE_URL=/api
API_PROXY_TARGET=http://localhost:5090
```

## 8. Sao lưu

```bash
docker exec mysql-dev sh -c 'mysqldump -uroot -p"$MYSQL_ROOT_PASSWORD" --single-transaction expense' > expense-$(date +%F).sql
```
