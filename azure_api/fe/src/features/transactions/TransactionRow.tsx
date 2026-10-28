import { ArrowLeftRight } from 'lucide-react'
import { CategoryIcon } from '../../components/CategoryIcon'
import { Money } from '../../components/Money'
import type { Transaction } from '../../lib/types'
import { transactionKindLabels } from './api'

/** 1 dòng giao dịch (bấm để sửa) – dùng ở trang Giao dịch và Tổng quan. */
export function TransactionRow({ tx, onClick }: { tx: Transaction; onClick: () => void }) {
  const title = tx.kind === 'transfer' ? 'Chuyển tiền' : (tx.category?.name ?? transactionKindLabels[tx.kind])
  const walletText = tx.kind === 'transfer' ? `${tx.wallet.name} → ${tx.toWallet?.name}` : tx.wallet.name
  return (
    <button type="button" onClick={onClick} className="flex w-full items-center gap-3 px-4 py-2.5 text-left hover:bg-slate-50 dark:hover:bg-slate-800/50">
      {tx.kind === 'transfer' || !tx.category ? (
        <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-sky-100 text-sky-600 dark:bg-sky-950 dark:text-sky-300" aria-hidden>
          <ArrowLeftRight className="size-4" />
        </span>
      ) : (
        <CategoryIcon icon={tx.category.icon} color={tx.category.color} size="sm" />
      )}
      <span className="min-w-0 flex-1">
        <span className="block truncate text-sm font-medium">{title}</span>
        <span className="block truncate text-xs text-slate-500 dark:text-slate-400">
          {walletText}
          {tx.note && ` · ${tx.note}`}
        </span>
      </span>
      <Money amount={tx.amount} kind={tx.kind} className="shrink-0 text-sm font-semibold" />
    </button>
  )
}
