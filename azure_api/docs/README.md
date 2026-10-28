# Sổ Chi Tiêu – Ứng dụng quản lý chi tiêu cá nhân (React + .NET)

Ứng dụng web giúp một người ghi lại **thu / chi / chuyển tiền** giữa các ví, đặt **ngân sách theo danh mục** mỗi tháng
và xem **báo cáo** (tổng quan tháng, cơ cấu theo danh mục, xu hướng theo thời gian).

- **Backend**: ASP.NET Core (.NET 10) Minimal API + EF Core + MySQL, xác thực JWT (access token) + refresh token trong cookie `HttpOnly`.
- **Frontend**: React 19 + Vite 8 + TypeScript, TanStack Query, React Router, Tailwind CSS, Recharts.

> Dự án phục vụ mục đích học tập (bài tập lớn).

## Mục lục tài liệu

| # | Tài liệu | Nội dung |
|---|----------|----------|
| 1 | [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) | Mục tiêu, phạm vi, kiến trúc, luồng xác thực, cách chạy & triển khai |
| 2 | [02-nghiep-vu.md](02-nghiep-vu.md) | Quy tắc nghiệp vụ: ví, danh mục, giao dịch, chuyển tiền, ngân sách, công thức số dư |
| 3 | [03-backend-dotnet.md](03-backend-dotnet.md) | Thiết kế backend: endpoint, DTO, lỗi, cấu trúc thư mục, package |
| 4 | [04-frontend-react.md](04-frontend-react.md) | Thiết kế frontend: trang, component, state, gọi API, cấu trúc thư mục |
| 5 | [05-ke-hoach-phase.md](05-ke-hoach-phase.md) | **Kế hoạch triển khai theo phase** + checklist + DoD |
| 6 | [06-database.md](06-database.md) | Database MySQL: ERD, bảng, index, entity EF Core, biến môi trường `.env` |
| 7 | [07-van-hanh.md](07-van-hanh.md) | Vận hành: publish, chạy `backend.exe`, Windows Service, cập nhật, bảo mật production |

## Cấu trúc repo

```
azure_api/
├── Expense.slnx        # solution .NET (backend + backend.Tests)
├── backend/            # ASP.NET Core (.NET 10) – Minimal API + EF Core (MySQL)
│   ├── .env            # biến môi trường local (không commit) – mẫu: .env.example
│   └── dotnet-tools.json
├── backend.Tests/      # xUnit trên MySQL DB expense_test – chạy: dotnet test Expense.slnx
├── fe/                 # React + Vite + TypeScript
│   └── .env            # cấu hình dev server (không commit) – mẫu: .env.example
└── docs/               # Tài liệu (thư mục này)
```

## Chạy nhanh

Hệ thống chạy **thủ công bằng lệnh**, không đóng gói Docker. Yêu cầu:
- .NET SDK 10, Node.js 24+ (npm 11+).
- MySQL đang chạy ở `localhost:3306`, đã có database và user `expense` ([06 §2](06-database.md#2-khởi-tạo-database-làm-1-lần)).

Lần đầu: copy `.env.example` → `.env` ở cả `backend/` và `fe/`, điền mật khẩu DB + JWT secret.

```bash
# 1. Backend (terminal 1)
cd backend
dotnet tool restore
dotnet run                     # http://localhost:5090 – Development tự áp migration
curl http://localhost:5090/health

# Test backend (cần MySQL đang chạy – dùng DB riêng expense_test)
dotnet test ../Expense.slnx

# 2. Frontend (terminal 2)
cd fe
npm install
npm run dev                    # http://localhost:5173 – /api được proxy sang backend
```

Bản phát hành: backend phục vụ luôn giao diện → chỉ còn **1 tiến trình** `backend.exe` ở `http://localhost:5090`
(chạy tay, hoặc Windows Service) – xem [07 – Vận hành](07-van-hanh.md).

```powershell
cd backend
dotnet publish -c Release -o publish     # tự build fe → publish\wwwroot
# tạo publish\.env (ASPNETCORE_ENVIRONMENT=Production…) rồi:
.\publish\backend.exe                    # hoặc (Admin): ..\scripts\install-service.ps1
```
