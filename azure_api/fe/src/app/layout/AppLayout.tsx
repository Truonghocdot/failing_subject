import { LogOut, Plus } from 'lucide-react'
import { Suspense } from 'react'
import { NavLink, Outlet } from 'react-router'
import { Button, IconButton } from '../../components/Button'
import { LoadingBlock } from '../../components/Feedback'
import { useAuth, useUser } from '../../features/auth/AuthProvider'
import { TransactionDialogProvider, useTransactionDialog } from '../../features/transactions/TransactionDialogProvider'
import { env } from '../env'
import { HealthBadge } from './HealthBadge'
import { navItems } from './nav'

const sideLinkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-brand-50 text-brand-700 dark:bg-brand-700/20 dark:text-brand-100'
      : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
  }`

const bottomLinkClass = ({ isActive }: { isActive: boolean }) =>
  `flex min-w-0 flex-1 flex-col items-center gap-0.5 py-2 text-[10px] font-medium ${
    isActive ? 'text-brand-700 dark:text-brand-500' : 'text-slate-500 dark:text-slate-400'
  }`

/** Máy tính: sidebar trái. Điện thoại: thanh tab dưới + nút "+" nổi. */
export function AppLayout() {
  return (
    <TransactionDialogProvider>
      <Shell />
    </TransactionDialogProvider>
  )
}

function Shell() {
  const user = useUser()
  const { logout } = useAuth()
  const { openCreate } = useTransactionDialog()

  return (
    <div className="min-h-dvh md:flex">
      <aside className="sticky top-0 hidden h-dvh w-60 shrink-0 flex-col border-r border-slate-200 bg-white p-4 md:flex dark:border-slate-800 dark:bg-slate-900">
        <div className="mb-5 flex items-center gap-2 px-2">
          <img src="/favicon.svg" alt="" className="size-8" />
          <span className="text-lg font-semibold">{env.appName}</span>
        </div>
        <Button onClick={() => openCreate()} className="mb-5 w-full">
          <Plus className="size-4" aria-hidden />
          Thêm giao dịch
        </Button>
        <nav aria-label="Điều hướng chính" className="flex flex-1 flex-col gap-1">
          {navItems.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} end={to === '/'} className={sideLinkClass}>
              <Icon className="size-5" aria-hidden />
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="flex flex-col gap-3 border-t border-slate-200 px-2 pt-3 dark:border-slate-800">
          <div className="flex items-center gap-2">
            <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700" aria-hidden>
              {user.displayName.charAt(0).toUpperCase()}
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{user.displayName}</p>
              <p className="truncate text-xs text-slate-500 dark:text-slate-400">{user.email}</p>
            </div>
            <IconButton label="Đăng xuất" onClick={() => void logout()}>
              <LogOut className="size-4" />
            </IconButton>
          </div>
          <HealthBadge />
        </div>
      </aside>

      <main className="min-w-0 flex-1 px-4 pt-4 pb-28 md:px-8 md:pt-8 md:pb-8">
        <Suspense fallback={<LoadingBlock />}>
          <Outlet />
        </Suspense>
      </main>

      <button
        type="button"
        onClick={() => openCreate()}
        aria-label="Thêm giao dịch"
        className="fixed right-4 bottom-[calc(4.5rem+env(safe-area-inset-bottom))] z-10 flex size-14 items-center justify-center rounded-full bg-brand-700 text-white shadow-lg hover:bg-brand-800 md:hidden"
      >
        <Plus className="size-6" aria-hidden />
      </button>

      <nav
        aria-label="Điều hướng nhanh"
        className="fixed inset-x-0 bottom-0 z-10 flex border-t border-slate-200 bg-white pb-[env(safe-area-inset-bottom)] md:hidden dark:border-slate-800 dark:bg-slate-900"
      >
        {navItems.map(({ to, label, icon: Icon }) => (
          <NavLink key={to} to={to} end={to === '/'} className={bottomLinkClass}>
            <Icon className="size-5" aria-hidden />
            <span className="truncate">{label}</span>
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
