import { Outlet, type RouteObject } from 'react-router'
import { AccountPage } from '../features/auth/AccountPage'
import { LoginPage, RegisterPage } from '../features/auth/AuthPages'
import { AuthProvider } from '../features/auth/AuthProvider'
import { BookingDetailPage as AdminBookingDetailPage } from '../features/admin/BookingDetailPage'
import { BookingsPage } from '../features/admin/BookingsPage'
import { CalendarPage } from '../features/admin/CalendarPage'
import { DashboardPage } from '../features/admin/DashboardPage'
import { GuestsPage } from '../features/admin/GuestsPage'
import { HomestayEditPage } from '../features/admin/HomestayEditPage'
import { HomestaysPage } from '../features/admin/HomestaysPage'
import { NewBookingPage } from '../features/admin/NewBookingPage'
import { RatesPage } from '../features/admin/RatesPage'
import { ReportsPage } from '../features/admin/ReportsPage'
import { ReviewsPage } from '../features/admin/ReviewsPage'
import { UsersPage } from '../features/admin/UsersPage'
import { GuestOnly, RequireAuth, RequireRole } from '../features/auth/guards'
import { BookingDetailPage } from '../features/booking/BookingDetailPage'
import { BookPage } from '../features/booking/BookPage'
import { LookupPage } from '../features/booking/LookupPage'
import { MyBookingsPage } from '../features/booking/MyBookingsPage'
import { HomePage } from '../features/home/HomePage'
import { StayDetailPage } from '../features/stays/StayDetailPage'
import { StaysPage } from '../features/stays/StaysPage'
import { AdminLayout } from './layout/AdminLayout'
import { PublicLayout } from './layout/PublicLayout'
import { NotFoundPage } from './NotFoundPage'

const staffRoles = ['staff', 'admin'] as const
const adminRoles = ['admin'] as const


export const routes: RouteObject[] = [
  {
    // AuthProvider trong cây route → dùng được useNavigate / useLocation, test render cả app bằng memory router
    element: (
      <AuthProvider>
        <Outlet />
      </AuthProvider>
    ),
    children: [
      {
        element: <PublicLayout />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'stays', element: <StaysPage /> },
          { path: 'stays/:slug', element: <StayDetailPage /> },
          { path: 'lookup', element: <LookupPage /> },
          {
            element: <GuestOnly />,
            children: [
              { path: 'login', element: <LoginPage /> },
              { path: 'register', element: <RegisterPage /> },
            ],
          },
          {
            element: <RequireAuth />,
            children: [
              { path: 'book/:roomId', element: <BookPage /> },
              { path: 'bookings', element: <MyBookingsPage /> },
              { path: 'bookings/:code', element: <BookingDetailPage /> },
              { path: 'account', element: <AccountPage /> },
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
            element: <AdminLayout />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: 'calendar', element: <CalendarPage /> },
              { path: 'bookings', element: <BookingsPage /> },
              { path: 'bookings/new', element: <NewBookingPage /> },
              { path: 'bookings/:id', element: <AdminBookingDetailPage /> },
              { path: 'guests', element: <GuestsPage /> },
              {
                // Chỉ admin – lễ tân không thấy menu, gõ thẳng URL → "Không có quyền" (API cũng trả 403)
                element: <RequireRole roles={adminRoles} />,
                children: [
                  { path: 'homestays', element: <HomestaysPage /> },
                  { path: 'homestays/:id', element: <HomestayEditPage /> },
                  { path: 'rates', element: <RatesPage /> },
                  { path: 'users', element: <UsersPage /> },
                  { path: 'reviews', element: <ReviewsPage /> },
                  { path: 'reports', element: <ReportsPage /> },
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
