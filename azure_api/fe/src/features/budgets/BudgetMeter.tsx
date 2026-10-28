import { CircleAlert, CircleCheck, TriangleAlert, type LucideIcon } from 'lucide-react'
import { CategoryIcon } from '../../components/CategoryIcon'
import { Money } from '../../components/Money'
import type { Budget, BudgetStatus } from '../../lib/types'
import { budgetStatusLabels } from './api'

// Màu trạng thái chỉ dùng cho trạng thái, luôn đi kèm icon + chữ (không bao giờ chỉ dựa vào màu)
const statusStyles: Record<BudgetStatus, { bar: string; text: string; icon: LucideIcon }> = {
  ok: { bar: 'bg-emerald-500', text: 'text-emerald-700 dark:text-emerald-400', icon: CircleCheck },
  warning: { bar: 'bg-amber-500', text: 'text-amber-700 dark:text-amber-400', icon: TriangleAlert },
  over: { bar: 'bg-red-600 dark:bg-red-500', text: 'text-red-700 dark:text-red-400', icon: CircleAlert },
}

/** Thanh tiến độ 1 ngân sách: đã chi / hạn mức, % và trạng thái. */
export function BudgetMeter({ budget, compact = false }: { budget: Budget; compact?: boolean }) {
  const style = statusStyles[budget.status]
  const Icon = style.icon
  const width = Math.min(budget.percent, 100)
  return (
    <div className="flex items-start gap-3">
      <CategoryIcon icon={budget.category.icon} color={budget.category.color} size="sm" />
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline justify-between gap-2">
          <span className="truncate text-sm font-medium">{budget.category.name}</span>
          <span className="shrink-0 text-xs text-slate-500 dark:text-slate-400">
            <Money amount={budget.spent} className="font-medium text-slate-700 dark:text-slate-200" /> / <Money amount={budget.amount} />
          </span>
        </div>
        <div
          className="mt-1.5 h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800"
          role="progressbar"
          aria-label={`Đã dùng ngân sách ${budget.category.name}`}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={width}
          aria-valuetext={`${budget.percent.toLocaleString('vi-VN')}%`}
        >
          <div className={`h-full rounded-full ${style.bar}`} style={{ width: `${width}%` }} />
        </div>
        <div className="mt-1 flex items-center justify-between gap-2 text-xs">
          <span className={`flex items-center gap-1 font-medium ${style.text}`}>
            <Icon className="size-3.5" aria-hidden />
            {budget.percent.toLocaleString('vi-VN')}% · {budgetStatusLabels[budget.status]}
          </span>
          {!compact && (
            <span className="text-slate-500 dark:text-slate-400">
              {budget.remaining >= 0 ? 'Còn ' : 'Vượt '}
              <Money amount={Math.abs(budget.remaining)} className={budget.remaining < 0 ? 'text-expense dark:text-red-400' : ''} />
            </span>
          )}
        </div>
      </div>
    </div>
  )
}
