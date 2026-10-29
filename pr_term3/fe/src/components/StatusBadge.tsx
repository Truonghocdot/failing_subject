import { statusLabels } from '../lib/format'
import type { BookingStatus } from '../lib/types'

// Màu theo docs/04-frontend-react.md §5 – luôn kèm chữ, không chỉ dựa vào màu
const tones: Record<BookingStatus, string> = {
  pending: 'bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200',
  confirmed: 'bg-sky-100 text-sky-900 dark:bg-sky-950 dark:text-sky-200',
  checked_in: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-950 dark:text-emerald-200',
  checked_out: 'bg-slate-200 text-slate-800 dark:bg-slate-800 dark:text-slate-200',
  cancelled: 'bg-red-50 text-red-800 dark:bg-red-950 dark:text-red-200',
  expired: 'bg-red-50 text-red-800 dark:bg-red-950 dark:text-red-200',
  no_show: 'bg-red-50 text-red-800 dark:bg-red-950 dark:text-red-200',
}

export function StatusBadge({ status }: { status: BookingStatus }) {
  return <span className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${tones[status]}`}>{statusLabels[status]}</span>
}
