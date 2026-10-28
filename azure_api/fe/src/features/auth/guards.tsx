import { Navigate, Outlet, useLocation, useSearchParams } from 'react-router'
import { FullPageSpinner } from '../../components/Feedback'
import { useAuth } from './AuthProvider'

/** Chỉ nhận đường dẫn nội bộ ("/x"), chặn "//evil.com" và URL tuyệt đối → không bị lợi dụng chuyển hướng ra ngoài. */
export function safeNext(next: string | null): string {
  return next && next.startsWith('/') && !next.startsWith('//') ? next : '/'
}

/** Route cần đăng nhập: chưa đăng nhập → /login?next=<trang đang mở>. */
export function RequireAuth() {
  const auth = useAuth()
  const location = useLocation()

  if (auth.status === 'loading') return <FullPageSpinner label="Đang kiểm tra đăng nhập…" />
  if (auth.status === 'anonymous') {
    const next = location.pathname + location.search
    return <Navigate to={next === '/' ? '/login' : `/login?next=${encodeURIComponent(next)}`} replace />
  }
  return <Outlet />
}

/** /login, /register: đã đăng nhập thì chuyển về trang định vào. */
export function GuestOnly() {
  const auth = useAuth()
  const [params] = useSearchParams()

  if (auth.status === 'loading') return <FullPageSpinner label="Đang kiểm tra đăng nhập…" />
  if (auth.status === 'authenticated') return <Navigate to={safeNext(params.get('next'))} replace />
  return <Outlet />
}
