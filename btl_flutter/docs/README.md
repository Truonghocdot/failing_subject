# VsMovie – Ứng dụng xem phim (Flutter + .NET)

Ứng dụng xem phim nhỏ gọn, lấy dữ liệu từ API công khai của **vsmov.com**.
Backend .NET đóng vai trò BFF (proxy + cache + chuẩn hoá dữ liệu), app Flutter hiển thị và phát phim qua trình phát nhúng.

> Dự án phục vụ mục đích học tập (bài tập lớn). Nội dung phim và bản quyền thuộc về nguồn cung cấp.

## Mục lục tài liệu

| # | Tài liệu | Nội dung |
|---|----------|----------|
| 1 | [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) | Mục tiêu, phạm vi, kiến trúc tổng thể, luồng dữ liệu |
| 2 | [02-api-nguon-vsmov.md](02-api-nguon-vsmov.md) | Tài liệu API bên thứ 3 (đã khảo sát thực tế) |
| 3 | [03-backend-dotnet.md](03-backend-dotnet.md) | Thiết kế backend ASP.NET Core: endpoint, DTO, cache, cấu trúc thư mục |
| 4 | [04-mobile-flutter.md](04-mobile-flutter.md) | Thiết kế app Flutter: màn hình, package, cấu trúc, state |
| 5 | [05-ke-hoach-phase.md](05-ke-hoach-phase.md) | **Kế hoạch triển khai theo phase** + checklist |
| 6 | [06-database.md](06-database.md) | Database PostgreSQL: ERD, bảng, entity EF Core, API tài khoản, biến môi trường `.env` |
| – | [samples/](samples/) | JSON mẫu lấy từ API thật – dùng làm fixture cho test |

## Cấu trúc repo

```
btl_flutter/
├── docker-compose.yml  # PostgreSQL 17
├── backend/            # ASP.NET Core (.NET 10) – Minimal API + EF Core
│   ├── .env            # biến môi trường local (không commit) – mẫu: .env.example
│   └── dotnet-tools.json
├── mobile/             # Flutter app (Dart 3.13+)
│   └── .env            # cấu hình app (không commit) – mẫu: .env.example
└── docs/               # Tài liệu (thư mục này)
```

## Chạy nhanh

Lần đầu: nếu chưa có `.env` thì copy từ `.env.example` ở cả `backend/` và `mobile/`.

```bash
# 1. Database (tại btl_flutter/)
docker compose up -d

# 2. Backend
cd backend
dotnet tool restore
dotnet ef database update      # từ Phase 5
dotnet run                     # http://localhost:5080

# 3. Mobile – Android emulator truy cập máy host qua 10.0.2.2
cd mobile
flutter run --dart-define-from-file=.env
```

Trên Windows cần bật **Developer Mode** (`start ms-settings:developers`) thì mới build được Flutter có plugin.
