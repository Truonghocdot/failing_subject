import type { ReactNode } from 'react'
import { env } from '../../app/env'

export function AuthLayout({ title, subtitle, children, footer }: { title: string; subtitle: string; children: ReactNode; footer: ReactNode }) {
  return (
    <main className="flex min-h-dvh items-center justify-center px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-3 text-center">
          <img src="/favicon.svg" alt="" className="size-12" />
          <p className="text-sm font-semibold tracking-wide text-brand-700 uppercase dark:text-brand-100">{env.appName}</p>
          <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">{subtitle}</p>
        </div>
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">{children}</div>
        <p className="mt-6 text-center text-sm text-slate-600 dark:text-slate-300">{footer}</p>
      </div>
    </main>
  )
}
