# 01 – Tổng quan & kiến trúc

## 1. Mục tiêu

Một ứng dụng web nhỏ, chạy nhanh, để một người dùng tự quản lý tiền của mình:

1. Ghi nhanh một khoản **chi / thu** (≤ 3 thao tác: số tiền → danh mục → lưu).
2. Biết **mỗi ví còn bao nhiêu tiền** (tiền mặt, tài khoản ngân hàng, ví điện tử, thẻ tín dụng).
3. Biết **tháng này đã tiêu bao nhiêu, vào đâu**, có vượt ngân sách không.
4. Xem lại lịch sử, lọc / tìm kiếm, xuất CSV.

## 2. Phạm vi

| Trong phạm vi (MVP) | Ngoài phạm vi (Phase 7 – mở rộng) |
|---|---|
| Đăng ký / đăng nhập email + mật khẩu | Đăng nhập Google / OTP |
| Ví: thêm, sửa, lưu trữ, số dư tính tự động | Đồng bộ ngân hàng, đọc SMS |
| Danh mục thu / chi (có sẵn bộ mặc định), màu + icon | Danh mục con nhiều cấp |
| Giao dịch thu / chi / **chuyển tiền giữa ví** | Giao dịch định kỳ tự động |
| Lọc theo khoảng ngày, ví, danh mục, loại, từ khoá; phân trang | Đính kèm ảnh hoá đơn (Azure Blob) |
| Ngân sách tháng theo danh mục chi + cảnh báo 80% / 100% | Ngân sách theo tuần / năm |
| Dashboard + báo cáo (tròn theo danh mục, cột theo thời gian) | Nhiều tiền tệ + tỷ giá |
| Xuất CSV giao dịch | Nhập CSV, chia sẻ sổ cho nhiều người |
| Giao diện tiếng Việt, responsive (máy tính + điện thoại) | PWA offline |

**1 người dùng = 1 sổ, 1 đơn vị tiền tệ** (mặc định `VND`) – mọi dữ liệu đều thuộc về đúng 1 `user_id`.

## 3. Kiến trúc tổng thể

```
┌──────────────────────────┐   /api/* (JSON)    ┌─────────────────────────────┐   TCP 3306   ┌──────────────┐
│ Trình duyệt              │ ─────────────────▶ │ backend (ASP.NET Core 10)   │ ───────────▶ │ MySQL        │
│ React SPA (fe/)          │ ◀───────────────── │ Minimal API + EF Core       │              │ db: expense  │
│ TanStack Query cache     │  Bearer access JWT │ JWT + refresh cookie        │              └──────────────┘
└──────────────────────────┘  Cookie rt (auth)  └─────────────────────────────┘
        ▲  Dev:  Vite dev server :5173 proxy /api ──▶ :5090
        └─ Prod: backend phục vụ luôn fe/dist (cùng origin, 1 tiến trình)
```

- **Cùng origin** ở cả dev (nhờ proxy Vite) lẫn prod (backend phục vụ file tĩnh) → không cần CORS cho trình duyệt,
  cookie refresh dùng được `SameSite=Strict`.
- Backend **không giữ state** ngoài DB (trừ rate limiter trong RAM) → khởi động lại không mất phiên.
- Toàn bộ tính toán tiền (số dư, tổng, ngân sách) làm ở **backend bằng SQL `SUM`** trên `decimal` – frontend chỉ hiển thị.

## 4. Luồng xác thực

```
Đăng nhập ──▶ POST /api/auth/login {email, password}
          ◀── 200 { accessToken (JWT 15'), expiresIn, user }  +  Set-Cookie: rt=<64 byte ngẫu nhiên>; HttpOnly; SameSite=Strict; Path=/api/auth
FE giữ accessToken trong RAM (không localStorage) → mỗi request gắn  Authorization: Bearer <access>

Access hết hạn (401) ──▶ POST /api/auth/refresh   (trình duyệt tự gửi cookie rt)
                     ◀── 200 access mới + cookie rt MỚI (xoay vòng; rt cũ bị đánh dấu replaced)
Mở lại tab           ──▶ FE gọi /api/auth/refresh ngay khi khởi động → còn cookie hợp lệ thì vào thẳng app
Dùng lại rt đã bị thay ──▶ ≤ 30 giây: coi là nhiều tab refresh cùng lúc → vẫn cấp phiên mới
                          > 30 giây: coi là bị đánh cắp → thu hồi TOÀN BỘ phiên của user, trả 401
Đổi mật khẩu         ──▶ thu hồi mọi phiên (mọi thiết bị) + cấp phiên mới cho thiết bị đang dùng
Đăng xuất            ──▶ POST /api/auth/logout → thu hồi rt + xoá cookie
```

- DB chỉ lưu **SHA-256** của refresh token; mật khẩu băm **BCrypt** (work factor 11).
- FE gom các request bị 401 lại, chỉ gọi refresh **1 lần** rồi phát lại (single-flight).
- `/api/auth/*` bị giới hạn 10 request/phút/IP (cấu hình `RateLimit__AuthPermitPerMinute`).

## 5. Công nghệ

| Lớp | Lựa chọn | Lý do |
|---|---|---|
| API | ASP.NET Core 10 Minimal API, OpenAPI | Gọn, nhanh, sinh tài liệu `/openapi/v1.json` |
| ORM | EF Core 10 + `MySql.EntityFrameworkCore` (Oracle) + snake_case | Provider chính thức hỗ trợ EF 10; migration bằng `dotnet ef` |
| DB | MySQL 8+/9+ (`localhost:3306`), `utf8mb4_unicode_ci` | Có sẵn trên máy dev; tiếng Việt có dấu |
| Auth | JWT HS256 + refresh token xoay vòng, BCrypt | Không cần dịch vụ ngoài |
| Test BE | xUnit + `WebApplicationFactory` + MySQL thật (DB riêng `expense_test`) | Test API thật; SQLite không `SUM` được `decimal` và phân biệt hoa thường → khác MySQL |
| FE | React 19, Vite 8, TypeScript 7 | Build nhanh, HMR |
| Data fetching | TanStack Query 5 | Cache, refetch, invalidate sau khi ghi |
| Router | React Router 8 | Route lồng nhau, bảo vệ route |
| Form | react-hook-form + zod | Validate phía client khớp backend |
| UI | Tailwind CSS 4, lucide-react (icon), Recharts (biểu đồ) | Nhẹ, không cần thư viện component lớn |
| Ngày / tiền | dayjs (`vi`), `Intl.NumberFormat('vi-VN')` | Định dạng chuẩn Việt Nam |
| Test FE | Vitest + Testing Library + jsdom | Chạy cùng cấu hình Vite |

## 6. Chạy & triển khai

**Nguyên tắc: mọi thứ chạy thủ công bằng lệnh hoặc service lẻ, không đóng gói Docker.**
Docker trên máy dev chỉ dùng để giữ dịch vụ MySQL ở cổng 3306.

| Môi trường | Cách chạy |
|---|---|
| Dev | 2 terminal: `dotnet run` (backend :5090) + `npm run dev` (fe :5173, proxy `/api`) |
| Release local | `dotnet publish -c Release -o publish` (tự `npm run build` ở `fe/` và chép `fe/dist` vào `publish/wwwroot`) → `publish\backend.exe` |
| Service Windows | `scripts\install-service.ps1` (Admin) – service `SoChiTieu`, tài khoản LocalService, delayed-auto, tự khởi động lại; backend gọi `UseWindowsService()` |
| Azure (tuỳ chọn, Phase 7) | App Service (Linux, .NET 10) chạy gói publish + Azure Database for MySQL Flexible Server; biến môi trường đặt trong App Settings |

Cổng mặc định: backend **5090** (tránh trùng VsMovie 5080), Vite **5173**. Chi tiết vận hành: [07](07-van-hanh.md).
