import type { RouteObject } from 'react-router'
import { PlaceholderPage } from '../components/PlaceholderPage'
import { HomePage } from '../features/home/HomePage'
import { AdminLayout } from './layout/AdminLayout'
import { PublicLayout } from './layout/PublicLayout'
import { NotFoundPage } from './NotFoundPage'

// Phase 0: trang tạm cho mọi route ở docs/04-frontend-react.md §2 – thay dần ở Phase 4 (khách) và Phase 5 / 6 (quản trị)
const todo = (title: string, phase: number, description: string) => <PlaceholderPage title={title} phase={phase} description={description} />

export const routes: RouteObject[] = [
  {
    element: <PublicLayout />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'stays', element: todo('Tìm homestay', 4, 'Kết quả theo nơi đến, ngày, số khách; lọc tiện nghi, giá; "giá từ / đêm".') },
      { path: 'stays/:slug', element: todo('Chi tiết homestay', 4, 'Ảnh, tiện nghi, chính sách, phòng + lịch còn trống, báo giá từng đêm, đánh giá.') },
      { path: 'book/:roomId', element: todo('Đặt phòng', 4, 'Thông tin khách, tóm tắt giá, đồng ý chính sách → mã đặt phòng + hướng dẫn chuyển khoản.') },
      { path: 'bookings', element: todo('Đặt phòng của tôi', 4, 'Sắp tới · Đã ở · Đã huỷ.') },
      { path: 'bookings/:code', element: todo('Chi tiết đặt phòng', 4, 'Trạng thái, hạn giữ chỗ, hướng dẫn chuyển khoản, huỷ (xem trước số hoàn), đánh giá.') },
      { path: 'lookup', element: todo('Tra cứu đặt phòng', 4, 'Mã đặt phòng + số điện thoại.') },
      { path: 'login', element: todo('Đăng nhập', 4, 'Email + mật khẩu; khách, lễ tân, quản trị dùng chung.') },
      { path: 'register', element: todo('Đăng ký', 4, 'Tạo tài khoản khách.') },
      { path: 'account', element: todo('Tài khoản', 4, 'Họ tên, số điện thoại, đổi mật khẩu.') },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  {
    path: 'admin',
    element: <AdminLayout />,
    children: [
      { index: true, element: todo('Hôm nay', 5, 'Khách đến / đi / đang ở, đặt phòng chờ cọc sắp hết hạn, công suất 7 ngày tới.') },
      { path: 'calendar', element: todo('Lịch phòng', 5, 'Lưới phòng × ngày, thanh đặt phòng theo trạng thái, bấm ô trống để tạo đặt phòng / khoá phòng.') },
      { path: 'bookings', element: todo('Đặt phòng', 5, 'Lọc trạng thái, homestay, ngày, nguồn; tìm mã / tên / số điện thoại.') },
      { path: 'bookings/:id', element: todo('Chi tiết đặt phòng', 5, 'Xác nhận, check-in, check-out, huỷ, đổi ngày / phòng, khoản thu – hoàn, nhật ký.') },
      { path: 'guests', element: todo('Khách', 5, 'Tìm khách, lịch sử lưu trú.') },
      { path: 'homestays', element: todo('Homestay & phòng', 5, 'Thông tin, ảnh, chính sách cọc – huỷ, phòng, giá cơ bản / cuối tuần.') },
      { path: 'homestays/:id', element: todo('Sửa homestay', 5, 'Thông tin, ảnh (kéo thả sắp xếp), phòng.') },
      { path: 'rates', element: todo('Giá theo ngày', 5, 'Lịch giá từng phòng, đặt giá lễ / mùa cao điểm cho khoảng ngày.') },
      { path: 'reviews', element: todo('Đánh giá', 6, 'Ẩn / hiện đánh giá của khách.') },
      { path: 'reports', element: todo('Báo cáo', 6, 'Công suất phòng, ADR, RevPAR, doanh thu theo tháng / homestay / nguồn.') },
      { path: 'users', element: todo('Nhân viên', 5, 'Tạo tài khoản lễ tân / quản trị, khoá tài khoản.') },
      { path: '*', element: <NotFoundPage homeHref="/admin" homeLabel="Về trang Hôm nay" /> },
    ],
  },
]
