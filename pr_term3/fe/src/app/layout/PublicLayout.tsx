import { CircleUserRound, Menu, X } from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink, Outlet } from 'react-router'
import { useAuth } from '../../features/auth/useAuth'
import { SkipLink } from '../../components/SkipLink'
import { env } from '../env'
import { HealthBadge } from './HealthBadge'
import { publicNav } from './nav'

// Năm cho chân trang – tính 1 lần khi tải trang (không gọi Date trong lúc render)
const year = new Date().getFullYear()

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
    isActive ? 'text-brand-700 dark:text-brand-100' : 'text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800'
  }`

/** Khu khách: thanh trên cùng (điện thoại: nút menu), nội dung, chân trang. */
export function PublicLayout() {
  const [open, setOpen] = useState(false)
  const auth = useAuth()
  const user = auth.status === 'authenticated' ? auth.user : null
  return (
    <div className="flex min-h-dvh flex-col">
      <SkipLink />
      <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/90 backdrop-blur dark:border-slate-800 dark:bg-slate-900/90">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4">
          <Link to="/" className="flex items-center gap-2 font-semibold">
            <img src="/favicon.svg" alt="" className="size-8" />
            <span className="text-lg">{env.appName}</span>
          </Link>
          <nav aria-label="Điều hướng khách" className="ml-auto hidden items-center gap-1 md:flex">
            {publicNav.map((item) => (
              <NavLink key={item.to} to={item.to} className={linkClass}>
                {item.label}
              </NavLink>
            ))}
            {user ? (
              <NavLink to="/account" className={(a) => `${linkClass(a)} ml-2 flex max-w-48 items-center gap-2`}>
                <CircleUserRound className="size-5 shrink-0" aria-hidden />
                <span className="truncate">{user.fullName}</span>
              </NavLink>
            ) : (
              <Link to="/login" className="ml-2 rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white hover:bg-brand-800">
                Đăng nhập
              </Link>
            )}
          </nav>
          <button
            type="button"
            className="ml-auto rounded-lg p-2 text-slate-700 hover:bg-slate-100 md:hidden dark:text-slate-200 dark:hover:bg-slate-800"
            aria-label={open ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={open}
            onClick={() => setOpen((v) => !v)}
          >
            {open ? <X className="size-6" aria-hidden /> : <Menu className="size-6" aria-hidden />}
          </button>
        </div>
        {open && (
          <nav aria-label="Điều hướng khách (điện thoại)" className="flex flex-col gap-1 border-t border-slate-200 px-4 py-3 md:hidden dark:border-slate-800">
            {publicNav.map((item) => (
              <NavLink key={item.to} to={item.to} className={linkClass} onClick={() => setOpen(false)}>
                {item.label}
              </NavLink>
            ))}
            {user ? (
              <>
                <NavLink to="/account" className={linkClass} onClick={() => setOpen(false)}>
                  Tài khoản ({user.fullName})
                </NavLink>
                <button
                  type="button"
                  className={`${linkClass({ isActive: false })} text-left`}
                  onClick={() => {
                    setOpen(false)
                    void auth.logout()
                  }}
                >
                  Đăng xuất
                </button>
              </>
            ) : (
              <NavLink to="/login" className={linkClass} onClick={() => setOpen(false)}>
                Đăng nhập
              </NavLink>
            )}
          </nav>
        )}
      </header>

      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 outline-none md:py-10">
        <Outlet />
      </main>

      <footer className="border-t border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="mx-auto flex max-w-6xl flex-col gap-3 px-4 py-6 text-sm text-slate-600 sm:flex-row sm:items-center sm:justify-between dark:text-slate-400">
          <p>© {year} {env.appName} · Nhận phòng 14:00 · Trả phòng 12:00</p>
          <div className="flex items-center gap-4">
            <Link to="/admin" className="hover:underline">
              Khu quản trị
            </Link>
            <HealthBadge />
          </div>
        </div>
      </footer>
    </div>
  )
}
