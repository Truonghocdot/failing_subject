import { CircleAlert, LoaderCircle, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

export function Spinner({ className = 'size-5' }: { className?: string }) {
  return <LoaderCircle className={`animate-spin ${className}`} aria-hidden />
}

export function FullPageSpinner({ label = 'Đang tải…' }: { label?: string }) {
  return (
    <div role="status" className="flex min-h-dvh items-center justify-center gap-3 text-slate-500 dark:text-slate-400">
      <Spinner />
      <span>{label}</span>
    </div>
  )
}

export function LoadingBlock({ label = 'Đang tải…' }: { label?: string }) {
  return (
    <div role="status" className="flex items-center justify-center gap-3 py-12 text-slate-500 dark:text-slate-400">
      <Spinner />
      <span>{label}</span>
    </div>
  )
}

export function Alert({ children, tone = 'error' }: { children: ReactNode; tone?: 'error' | 'info' }) {
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={`flex items-start gap-2 rounded-lg px-3 py-2 text-sm ${
        tone === 'error'
          ? 'bg-red-50 text-red-700 dark:bg-red-950/50 dark:text-red-300'
          : 'bg-sky-50 text-sky-800 dark:bg-sky-950/50 dark:text-sky-200'
      }`}
    >
      <CircleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
      <div>{children}</div>
    </div>
  )
}

export function EmptyState({ icon: Icon, title, children }: { icon: LucideIcon; title: string; children?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center dark:border-slate-700 dark:bg-slate-900">
      <Icon className="size-10 text-slate-400" aria-hidden />
      <p className="font-medium text-slate-700 dark:text-slate-200">{title}</p>
      {children && <div className="text-sm text-slate-500 dark:text-slate-400">{children}</div>}
    </div>
  )
}

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <section className={`rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900 ${className}`}>
      {children}
    </section>
  )
}
