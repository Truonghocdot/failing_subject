import type { ReactNode } from 'react'
import { Card } from './Feedback'

/** Ô số liệu nhỏ (Tổng thu, Đã chi…) dùng chung cho các trang. */
export function StatTile({ label, children, className = '' }: { label: string; children: ReactNode; className?: string }) {
  return (
    <Card className={`px-3 py-3 ${className}`}>
      <p className="text-xs text-slate-500 dark:text-slate-400">{label}</p>
      <p className="mt-1 truncate text-base font-semibold sm:text-lg">{children}</p>
    </Card>
  )
}
