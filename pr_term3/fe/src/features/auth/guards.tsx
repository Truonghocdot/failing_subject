import { ShieldOff } from 'lucide-react'
import { Link, Navigate, Outlet, useLocation, useSearchParams } from 'react-router'
import { LoadingBlock } from '../../components/Feedback'
import type { UserRole } from '../../lib/types'
import { loginHref, safeNext } from './next'
import { isStaff, useAuth } from './useAuth'

/** Route cần đăng nhập: chưa đăng nhập → /login?next=<trang đang mở> (giữ cả ngày / số khách khi đang đặt phòng). */
export function RequireAuth() {
  const auth = useAuth()
  const location = useLocation()

  if (auth.status === 'loading') return <LoadingBlock label="Đang kiểm tra đăng nhập…" />
  if (auth.status === 'anonymous') return <Navigate to={loginHref(location.pathname + location.search)} replace />
  return <Outlet />
}

/** /login, /register: đã đăng nhập thì chuyển về trang định vào. */
export function GuestOnly() {
  const auth = useAuth()
  const [params] = useSearchParams()

  if (auth.status === 'loading') return <LoadingBlock label="Đang kiểm tra đăng nhập…" />
  if (auth.status === 'authenticated') return <Navigate to={safeNext(params.get('next'))} replace />
  return <Outlet />
}

/**
 * Route theo vai trò: chưa đăng nhập → /login?next; sai vai trò → trang "Không có quyền" (backend vẫn trả 403 nếu gọi thẳng API).
 * Khu quản trị: <RequireRole roles={['staff', 'admin']} />, mục chỉ admin: <RequireRole roles={['admin']} />.
 */
export function RequireRole({ roles }: { roles: readonly UserRole[] }) {
  const auth = useAuth()
  const location = useLocation()

  if (auth.status === 'loading') return <LoadingBlock label="Đang kiểm tra đăng nhập…" />
  if (auth.status === 'anonymous') return <Navigate to={loginHref(location.pathname + location.search)} replace />
  if (!roles.includes(auth.user.role)) return <Forbidden staffArea={isStaff(auth.user.role)} />
  return <Outlet />
}

function Forbidden({ staffArea }: { staffArea: boolean }) {
  return (
    <div className="flex min-h-[50dvh] flex-col items-center justify-center gap-3 px-4 text-center">
      <ShieldOff className="size-12 text-slate-400" aria-hidden />
      <h1 className="text-xl font-semibold">Không có quyền</h1>
      <p className="max-w-sm text-slate-600 dark:text-slate-300">
        {staffArea ? 'Mục này chỉ dành cho quản trị viên.' : 'Khu quản trị chỉ dành cho lễ tân và quản trị viên.'}
      </p>
      <Link to={staffArea ? '/admin' : '/'} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
        {staffArea ? 'Về trang Hôm nay' : 'Về trang chủ'}
      </Link>
    </div>
  )
}
