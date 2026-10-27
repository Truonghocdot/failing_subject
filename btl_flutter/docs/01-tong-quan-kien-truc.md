# 01 – Tổng quan & Kiến trúc

## 1. Mục tiêu

Xây dựng nhanh một ứng dụng xem phim **nhỏ, gọn, chạy được end-to-end**:

- Duyệt phim mới cập nhật, phim bộ, phim lẻ, phim chiếu rạp.
- Tìm kiếm theo tên; lọc theo thể loại / quốc gia / năm.
- Xem chi tiết phim và danh sách tập theo server.
- Phát phim qua trình phát nhúng (embed player) của nguồn.
- Đăng ký / đăng nhập; lưu phim yêu thích và lịch sử xem trên server (PostgreSQL), dùng chung nhiều thiết bị.

## 2. Phạm vi

| Trong phạm vi | Ngoài phạm vi (có thể mở rộng sau) |
|---|---|
| Đọc dữ liệu từ vsmov qua backend proxy | Tự lưu trữ / transcode video |
| Phát video bằng WebView (embed) | Trình phát native (m3u8) – nguồn không cung cấp link trực tiếp |
| Tài khoản (email + mật khẩu, JWT); yêu thích, lịch sử lưu DB | Đăng nhập mạng xã hội, quên mật khẩu qua email |
| Khách vẫn duyệt và xem phim được, không cần đăng nhập | Lưu vị trí đang phát (embed player không cho đọc) |
| Android (ưu tiên), iOS | Desktop/Web (WebView hạn chế) |

## 3. Kiến trúc tổng thể

```
┌──────────────────────┐   HTTP/JSON    ┌───────────────────────────┐   HTTPS    ┌──────────────────┐
│   Flutter app        │ ─────────────▶ │  Backend .NET (BFF)       │ ─────────▶ │ vsmov.com/api/*  │
│  (mobile/)           │ ◀───────────── │  - Proxy + chuẩn hoá DTO  │ ◀───────── │  (bên thứ 3)     │
│                      │   DTO gọn      │  - IMemoryCache           │            └──────────────────┘
│  - UI, state         │                │  - Retry/timeout          │
│  - Token (secure     │                │  - Gom dữ liệu trang Home │
│    storage)          │                │  - Auth JWT, /api/me/*    │
│                      │                └─────────────┬─────────────┘
│                      │                              │ EF Core
│                      │                ┌─────────────▼─────────────┐
│                      │                │ PostgreSQL 17 (Docker)    │
│                      │                │ users, favorites,         │
│                      │                │ watch_history, movies...  │
│                      │                └───────────────────────────┘
│  Player (WebView) ───┼──── tải trực tiếp ───▶ https://vXX.streamvsmov.com/video/{id}
└──────────────────────┘                        (embed player, cho phép nhúng: frame-ancestors *)
```

Chi tiết database: [06-database.md](06-database.md).

### Vì sao cần backend thay vì gọi thẳng vsmov từ app?

1. **Chuẩn hoá dữ liệu**: API nguồn không nhất quán (`status` lúc là `true`, lúc là `"success"`; `vote_average` là chuỗi; `server_name` chứa `\r\n` và khoảng trắng; ảnh có thể trả đường dẫn tương đối). Backend trả về DTO sạch, camelCase, kiểu dữ liệu đúng → code Flutter đơn giản.
2. **Hiệu năng**: cache trong bộ nhớ, gom nhiều danh sách thành 1 request `/api/home` → mở app nhanh.
3. **Cô lập rủi ro**: nếu nguồn đổi cấu trúc / đổi domain chỉ cần sửa backend, không phải phát hành lại app.
4. **Đáp ứng yêu cầu bài tập**: có cả phần .NET và Flutter.

## 4. Luồng chính

### 4.1 Mở app → Trang chủ
1. App gọi `GET /api/home`.
2. Backend gọi song song 4 danh sách nguồn (mới cập nhật, phim bộ, phim lẻ, chiếu rạp) – trang 1, có cache 5 phút.
3. Trả về `sections[]`, app render banner + các hàng ngang.

### 4.2 Xem phim
1. Người dùng chọn phim → `GET /api/movies/{slug}` → hiển thị thông tin + danh sách server/tập.
2. Chọn tập → mở màn hình Player với `embedUrl` của tập.
3. WebView tải embed player; nếu đã đăng nhập, app gọi `PUT /api/me/history/{slug}` để ghi tập đang xem.

### 4.3 Đăng nhập
1. `POST /api/auth/login` → nhận `accessToken` (30 phút) + `refreshToken` (30 ngày).
2. App lưu token vào `flutter_secure_storage`; Dio interceptor gắn `Authorization: Bearer`.
3. Gặp `401` → tự gọi `/api/auth/refresh` một lần rồi thử lại; refresh thất bại → đăng xuất.

## 5. Công nghệ

| Thành phần | Lựa chọn |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal API, `IHttpClientFactory`, `IMemoryCache`, `Microsoft.Extensions.Http.Resilience`, OpenAPI |
| Database | PostgreSQL 17 (Docker), EF Core 10 + Npgsql, `EFCore.NamingConventions`, JWT Bearer, BCrypt, `DotNetEnv` |
| Mobile | Flutter 3.47 / Dart 3.13, `dio`, `flutter_riverpod`, `go_router`, `cached_network_image`, `webview_flutter`, `shared_preferences`, `flutter_secure_storage` |
| Test | xUnit (backend), `flutter_test` (mobile) |

## 6. Rủi ro & cách xử lý

| Rủi ro | Xử lý |
|---|---|
| Nguồn chậm / lỗi tạm thời | Timeout 10s + retry (resilience handler); cache; trả `502` dạng ProblemDetails |
| Nguồn đổi cấu trúc JSON | Model nguồn tách riêng (`Vsmov/`), mapping tập trung một chỗ, có test với `docs/samples/*.json` |
| Ảnh poster rất nặng (~1.5 MB) | Dùng `thumb_url` cho danh sách, `cached_network_image` + `memCacheWidth` |
| Embed player có quảng cáo / mở tab mới | `NavigationDelegate` chặn điều hướng ra ngoài domain player |
| Bản quyền nội dung | Chỉ dùng cho mục đích học tập; ghi rõ nguồn trong app |
