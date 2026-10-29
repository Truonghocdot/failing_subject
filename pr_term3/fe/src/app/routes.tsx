import type { ComponentType } from 'react'
import { Outlet, type RouteObject } from 'react-router'
import { FullPageSpinner } from '../components/Feedback'
import { AuthProvider } from '../features/auth/AuthProvider'
import { GuestOnly, RequireAuth, RequireRole } from '../features/auth/guards'
import { HomePage } from '../features/home/HomePage'
import { PublicLayout } from './layout/PublicLayout'
import { NotFoundPage } from './NotFoundPage'
import { RouteError } from './RouteError'

const staffRoles = ['staff', 'admin'] as const
const adminRoles = ['admin'] as const

/**
 * Tách code theo route: trang chủ + khung khu khách tải ngay, các trang khác tải khi vào lần đầu
 * (khu quản trị không nằm trong bundle của khách). React Router tải chunk trước khi chuyển trang → không nháy trang trắng.
 */
function page<M>(load: () => Promise<M>, pick: (m: M) => ComponentType) {
  return async () => ({ Component: pick(await load()) })
}

export const routes: RouteObject[] = [
  {
    // AuthProvider trong cây route → dùng được useNavigate / useLocation, test render cả app bằng memory router
    element: (
      <AuthProvider>
        <Outlet />
      </AuthProvider>
    ),
    // Lần tải đầu vào thẳng 1 trang tách chunk (VD F5 ở /admin/calendar)
    hydrateFallbackElement: <FullPageSpinner />,
    // Sau khi cập nhật bản mới, tab cũ xin chunk đã bị xoá → "Ứng dụng vừa được cập nhật – Tải lại trang"
    errorElement: <RouteError />,
    children: [
      {
        element: <PublicLayout />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'stays', lazy: page(() => import('../features/stays/StaysPage'), (m) => m.StaysPage) },
          { path: 'stays/:slug', lazy: page(() => import('../features/stays/StayDetailPage'), (m) => m.StayDetailPage) },
          { path: 'lookup', lazy: page(() => import('../features/booking/LookupPage'), (m) => m.LookupPage) },
          {
            element: <GuestOnly />,
            children: [
              { path: 'login', lazy: page(() => import('../features/auth/AuthPages'), (m) => m.LoginPage) },
              { path: 'register', lazy: page(() => import('../features/auth/AuthPages'), (m) => m.RegisterPage) },
            ],
          },
          {
            element: <RequireAuth />,
            children: [
              { path: 'book/:roomId', lazy: page(() => import('../features/booking/BookPage'), (m) => m.BookPage) },
              { path: 'bookings', lazy: page(() => import('../features/booking/MyBookingsPage'), (m) => m.MyBookingsPage) },
              { path: 'bookings/:code', lazy: page(() => import('../features/booking/BookingDetailPage'), (m) => m.BookingDetailPage) },
              { path: 'account', lazy: page(() => import('../features/auth/AccountPage'), (m) => m.AccountPage) },
            ],
          },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
      {
        path: 'admin',
        element: <RequireRole roles={staffRoles} />,
        children: [
          {
            lazy: page(() => import('./layout/AdminLayout'), (m) => m.AdminLayout),
            children: [
              { index: true, lazy: page(() => import('../features/admin/DashboardPage'), (m) => m.DashboardPage) },
              { path: 'calendar', lazy: page(() => import('../features/admin/CalendarPage'), (m) => m.CalendarPage) },
              { path: 'bookings', lazy: page(() => import('../features/admin/BookingsPage'), (m) => m.BookingsPage) },
              { path: 'bookings/new', lazy: page(() => import('../features/admin/NewBookingPage'), (m) => m.NewBookingPage) },
              { path: 'bookings/:id', lazy: page(() => import('../features/admin/BookingDetailPage'), (m) => m.BookingDetailPage) },
              { path: 'guests', lazy: page(() => import('../features/admin/GuestsPage'), (m) => m.GuestsPage) },
              {
                // Chỉ admin – lễ tân không thấy menu, gõ thẳng URL → "Không có quyền" (API cũng trả 403)
                element: <RequireRole roles={adminRoles} />,
                children: [
                  { path: 'homestays', lazy: page(() => import('../features/admin/HomestaysPage'), (m) => m.HomestaysPage) },
                  { path: 'homestays/:id', lazy: page(() => import('../features/admin/HomestayEditPage'), (m) => m.HomestayEditPage) },
                  { path: 'rates', lazy: page(() => import('../features/admin/RatesPage'), (m) => m.RatesPage) },
                  { path: 'users', lazy: page(() => import('../features/admin/UsersPage'), (m) => m.UsersPage) },
                  { path: 'reviews', lazy: page(() => import('../features/admin/ReviewsPage'), (m) => m.ReviewsPage) },
                  { path: 'reports', lazy: page(() => import('../features/admin/ReportsPage'), (m) => m.ReportsPage) },
                ],
              },
              { path: '*', element: <NotFoundPage homeHref="/admin" homeLabel="Về trang Hôm nay" /> },
            ],
          },
        ],
      },
    ],
  },
]
