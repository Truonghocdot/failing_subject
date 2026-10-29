# 04 – Frontend React

React 19 + Vite 8 + TypeScript, Tailwind CSS 4. Một SPA, hai khu dùng chung component.

## 1. Package

| Package | Dùng cho |
|---|---|
| `react`, `react-dom`, `react-router` | UI, định tuyến (bộ lọc tìm kiếm trên URL) |
| `@tanstack/react-query` | Cache gọi API, làm mới sau thay đổi |
| `react-hook-form`, `zod`, `@hookform/resolvers` | Form + kiểm tra dữ liệu (lỗi server gắn vào từng ô) |
| `dayjs` (locale `vi`) | Ngày, lịch |
| `lucide-react` | Icon |
| _(không dùng thư viện biểu đồ)_ | Báo cáo dùng `components/ColumnChart` (SVG tự viết, tooltip hover / focus) – nhẹ hơn `recharts`, đủ cho cột theo tháng |
| Dev: `vitest`, `@testing-library/*`, `jsdom`, `oxlint`, `@tailwindcss/vite` | Test, lint, CSS |

## 2. Trang

### 2.1 Khu khách (layout: thanh trên cùng + chân trang)

| Route | Trang | Nội dung |
|---|---|---|
| `/` | Trang chủ | Ô tìm kiếm lớn (nơi đến, nhận – trả phòng, số khách), homestay nổi bật, tỉnh phổ biến |
| `/stays?province&checkIn&checkOut&guests…` | Kết quả tìm kiếm | Bộ lọc (tiện nghi, giá tối đa, sắp xếp) trên URL, thẻ homestay: ảnh, điểm, "giá từ / đêm" |
| `/stays/:slug?room&checkIn&checkOut&guests` | Chi tiết homestay | Thư viện ảnh, mô tả, tiện nghi, chính sách nhận – trả – huỷ, danh sách phòng + lịch tháng còn trống (bấm 2 lần chọn ngày), đánh giá, **khung đặt phòng** dính bên phải (điện thoại: thanh tổng tiền dính đáy) |
| `/book/:roomId?stay&checkIn&checkOut&guests` | Đặt phòng 🔒 | Thông tin khách (điền sẵn từ tài khoản), ghi chú, tóm tắt giá, đồng ý chính sách → tạo đặt phòng; `stay` = slug để hiện tên / chính sách |
| `/bookings/:code` | Đặt phòng của tôi (chi tiết) 🔒 | Trạng thái, **hướng dẫn chuyển khoản** + đồng hồ hạn giữ chỗ, huỷ (xem trước số hoàn), đánh giá (Phase 6) |
| `/bookings?tab=upcoming\|stayed\|cancelled` | Danh sách đặt phòng của tôi 🔒 | Tab: Sắp tới · Đã ở · Đã huỷ |
| `/lookup` | Tra cứu | Mã đặt phòng + số điện thoại |
| `/login`, `/register`, `/account` | Tài khoản | |

### 2.2 Khu quản trị `/admin` (layout: sidebar trái, thanh dưới trên điện thoại) – vai trò `staff` / `admin`

| Route | Trang | Nội dung |
|---|---|---|
| `/admin` | Hôm nay | Khách đến / đi / đang ở, pending sắp hết hạn giữ chỗ, công suất 7 ngày |
| `/admin/calendar?homestayId&from&days` | **Lịch phòng** | Lưới phòng × ngày (14 / 30 ngày), thanh đặt phòng màu theo trạng thái; bấm ô trống → tạo đặt phòng / khoá phòng, bấm thanh → chi tiết |
| `/admin/bookings/new?homestayId&roomId&checkIn&checkOut` | Tạo đặt phòng hộ | Báo giá sống, thu cọc / thanh toán đủ ngay (đủ cọc → xác nhận) |
| `/admin/bookings` | Đặt phòng | Lọc trạng thái, homestay, khoảng ngày, nguồn, tìm mã / tên / SĐT |
| `/admin/bookings/:id` | Chi tiết | Nút hành động theo trạng thái, khoản thu – hoàn, còn phải thu, đổi ngày / phòng, nhật ký |
| `/admin/homestays`, `/admin/homestays/:id` | Homestay 🔑 | Thông tin, ảnh (kéo thả sắp xếp), chính sách, phòng |
| `/admin/rates` | Giá theo ngày 🔑 | Lịch giá từng phòng, đặt giá cho khoảng ngày |
| `/admin/guests` | Khách | Tìm, lịch sử lưu trú |
| `/admin/reviews` | Đánh giá 🔑 | Ẩn / hiện |
| `/admin/reports` | Báo cáo 🔑 | Công suất, ADR, RevPAR, doanh thu theo tháng / homestay / nguồn |
| `/admin/users` | Nhân viên 🔑 | Tạo, vai trò, khoá |

Khách đã đăng nhập vào `/admin` → trang "Không có quyền"; nhân viên vào `/admin/users` → ẩn menu + backend trả `403`.

## 3. Cấu trúc thư mục

```
fe/src/
├── app/            # App, routes, layout PublicLayout / AdminLayout, guards (RequireAuth, RequireRole), NotFound
├── components/     # Button, Field, Dialog, DateRangePicker, GuestPicker, Money, StatusBadge, Pagination, ImageGallery…
├── lib/            # api.ts (fetch + refresh single-flight), format.ts (tiền, ngày, đêm, nhãn), types.ts, forms.ts (SĐT / email như backend)
├── test/           # render.tsx (mockApi theo "METHOD /path", reply có thể là hàm), fixtures.ts
└── features/
    ├── auth/  home/  stays/  booking/                   # khu khách (booking/ gồm cả đặt phòng của tôi + tra cứu)
    └── admin/                                           # khu quản trị: api.ts (hook + invalidateBookings / invalidateCatalog), shared.tsx, 1 file / trang
```

Mỗi feature có `api.ts` chứa query key + hook; component chỉ gọi hook.

## 4. State & cache

| Query key | Làm mới khi |
|---|---|
| `['stays', filters]`, `['stay', slug]`, `['calendar', …]` | (khách) sau khi đặt / huỷ |
| `['my-bookings']`, `['booking', code]` | đặt, huỷ, đánh giá |
| `['admin', 'bookings', …]`, `['admin', 'calendar', …]`, `['admin', 'dashboard']` | mọi thao tác trên đặt phòng, khoản thu, khoá phòng (`invalidateBookings`) |
| `['admin', 'homestays']`, `['admin', 'rates', …]` | sửa homestay / phòng / giá (kéo theo làm mới lịch) |

- Bộ lọc tìm kiếm, khoảng ngày lịch phòng, bộ lọc danh sách đặt phòng **nằm trên URL** (F5 / chia sẻ link giữ nguyên).
- `409` khi đặt (phòng vừa bị đặt / giá đổi) → hiện thông báo + làm mới báo giá, không mất thông tin khách đã nhập.

## 5. Hiển thị

- Tiền `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })` → `1.200.000 ₫`.
- Khoảng ngày: "T6, 14/11 → CN, 16/11 · 2 đêm"; giờ nhận / trả phòng theo homestay.
- Màu trạng thái đặt phòng (luôn kèm chữ): chờ cọc – vàng, đã xác nhận – xanh dương, đang ở – xanh lá, đã trả phòng – xám, huỷ / hết hạn / no-show – đỏ nhạt.
- Mobile-first: khu khách tối ưu điện thoại (360 px); lịch phòng trên điện thoại cuộn ngang trong khung, cột tên phòng dính trái.
- Tương phản chữ ≥ 4,5:1, focus thấy được, hộp thoại trả focus khi đóng (bài học từ Sổ Chi Tiêu).

## 6. Cấu hình `.env` (Vite)

| Biến | Mặc định | Ghi chú |
|---|---|---|
| `VITE_APP_NAME` | `Homestay Booking` | Tiêu đề trang |
| `VITE_API_BASE_URL` | `/api` | |
| `API_PROXY_TARGET` | `http://localhost:5100` | Chỉ `vite.config.ts` đọc, không vào bundle |

Biến `VITE_*` được nhúng vào bundle → không đặt bí mật.
