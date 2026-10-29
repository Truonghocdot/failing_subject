import { ExternalLink, LogOut, Menu, X } from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useAuth, useUser } from '../../features/auth/useAuth'
import { env } from '../env'
import { HealthBadge } from './HealthBadge'
import { adminNav } from './nav'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-brand-50 text-brand-700 dark:bg-brand-700/20 dark:text-brand-100'
      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
  }`

const roleLabels = { guest: 'Khách', staff: 'Lễ tân', admin: 'Quản trị' } as const

function SideNav({ onNavigate }: { onNavigate?: () => void }) {
  const user = useUser()
  const { logout } = useAuth()
  const navigate = useNavigate()
  // Lễ tân không thấy mục chỉ admin (backend vẫn chặn 403 nếu gõ thẳng URL / gọi API)
  const items = adminNav.filter((item) => !item.adminOnly || user.role === 'admin')
  return (
    <>
      <div className="mb-6 flex items-center gap-2 px-2">
        <img src="/favicon.svg" alt="" className="size-8" />
        <div className="leading-tight">
          <p className="font-semibold">{env.appName}</p>
          <p className="text-xs text-slate-600 dark:text-slate-400">Quản trị</p>
        </div>
      </div>
      <nav aria-label="Điều hướng quản trị" className="flex flex-1 flex-col gap-1">
        {items.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end={to === '/admin'} className={linkClass} onClick={onNavigate}>
            <Icon className="size-5" aria-hidden />
            {label}
          </NavLink>
        ))}
      </nav>
      <div className="flex flex-col gap-3 border-t border-slate-200 px-2 pt-3 dark:border-slate-800">
        <div className="flex items-center justify-between gap-2">
          <div className="min-w-0 text-sm leading-tight">
            <p className="truncate font-medium">{user.fullName}</p>
            <p className="text-xs text-slate-600 dark:text-slate-400">{roleLabels[user.role]}</p>
          </div>
          <button
            type="button"
            aria-label="Đăng xuất"
            title="Đăng xuất"
            className="rounded-lg p-2 text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
            onClick={async () => {
              await logout()
              navigate('/login')
            }}
          >
            <LogOut className="size-4" aria-hidden />
          </button>
        </div>
        <Link to="/" className="flex items-center gap-2 text-sm text-slate-600 hover:underline dark:text-slate-300">
          <ExternalLink className="size-4" aria-hidden />
          Xem trang khách
        </Link>
        <HealthBadge />
      </div>
    </>
  )
}

/** Khu quản trị: sidebar trái (máy tính), ngăn kéo menu (điện thoại). Nằm trong <RequireRole roles={['staff','admin']} />. */
export function AdminLayout() {
  const [open, setOpen] = useState(false)
  return (
    <div className="min-h-dvh md:flex">
      <aside className="sticky top-0 hidden h-dvh w-64 shrink-0 flex-col border-r border-slate-200 bg-white p-4 md:flex dark:border-slate-800 dark:bg-slate-900">
        <SideNav />
      </aside>

      <header className="sticky top-0 z-20 flex h-14 items-center gap-3 border-b border-slate-200 bg-white px-4 md:hidden dark:border-slate-800 dark:bg-slate-900">
        <button
          type="button"
          className="rounded-lg p-2 text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800"
          aria-label="Mở menu quản trị"
          aria-expanded={open}
          onClick={() => setOpen(true)}
        >
          <Menu className="size-6" aria-hidden />
        </button>
        <span className="font-semibold">{env.appName} · Quản trị</span>
      </header>

      {open && (
        <div className="fixed inset-0 z-30 md:hidden">
          <div className="absolute inset-0 bg-slate-950/50" onClick={() => setOpen(false)} aria-hidden />
          <aside className="relative flex h-full w-72 max-w-[85vw] flex-col bg-white p-4 dark:bg-slate-900">
            <button
              type="button"
              className="absolute top-3 right-3 rounded-lg p-2 text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
              aria-label="Đóng menu quản trị"
              onClick={() => setOpen(false)}
            >
              <X className="size-5" aria-hidden />
            </button>
            <SideNav onNavigate={() => setOpen(false)} />
          </aside>
        </div>
      )}

      <main className="min-w-0 flex-1 px-4 py-4 md:px-8 md:py-8">
        <Outlet />
      </main>
    </div>
  )
}
