import { useCurrency } from '../features/auth/AuthProvider'
import { formatMoney, formatSignedMoney } from '../lib/format'
import type { TransactionKind } from '../lib/types'

/** Số tiền theo tiền tệ của người dùng; có kind → tô màu + dấu (chi đỏ "−", thu xanh "+"). */
export function Money({ amount, kind, className = '' }: { amount: number; kind?: TransactionKind; className?: string }) {
  const currency = useCurrency()
  const color =
    kind === 'expense'
      ? 'text-expense dark:text-red-400'
      : kind === 'income'
        ? 'text-income dark:text-emerald-400'
        : amount < 0
          ? 'text-expense dark:text-red-400'
          : ''
  return (
    <span className={`whitespace-nowrap tabular-nums ${color} ${className}`}>
      {kind ? formatSignedMoney(amount, kind, currency) : formatMoney(amount, currency)}
    </span>
  )
}
