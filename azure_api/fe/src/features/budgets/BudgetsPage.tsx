import { ChevronLeft, ChevronRight, Copy, PiggyBank, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { Button, IconButton } from '../../components/Button'
import { ConfirmDialog } from '../../components/Dialog'
import { Alert, Card, EmptyState, LoadingBlock } from '../../components/Feedback'
import { Money } from '../../components/Money'
import { PageHeader } from '../../components/PageHeader'
import { StatTile } from '../../components/StatTile'
import { errorMessage } from '../../lib/api'
import { currentMonth, formatMonth, shiftMonth } from '../../lib/format'
import type { Budget } from '../../lib/types'
import { useBudgets, useCopyBudgets, useDeleteBudget } from './api'
import { BudgetFormDialog } from './BudgetFormDialog'
import { BudgetMeter } from './BudgetMeter'

export function BudgetsPage() {
  const [params, setParams] = useSearchParams()
  const month = /^\d{4}-\d{2}$/.test(params.get('month') ?? '') ? params.get('month')! : currentMonth()
  const setMonth = (m: string) => setParams(m === currentMonth() ? {} : { month: m }, { replace: true })

  const { data, isPending, isFetching, error } = useBudgets(month)
  const copy = useCopyBudgets()
  const remove = useDeleteBudget()
  const [editing, setEditing] = useState<{ budget?: Budget } | null>(null)
  const [deleting, setDeleting] = useState<Budget | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [copyResult, setCopyResult] = useState<{ month: string; text: string; error?: boolean } | null>(null)

  const previous = shiftMonth(month, -1)
  const copyPrevious = async () => {
    try {
      const { created } = await copy.mutateAsync({ from: previous, to: month })
      setCopyResult({
        month,
        text: created
          ? `Đã thêm ${created} ngân sách từ ${formatMonth(previous).toLowerCase()}`
          : `${formatMonth(previous)} không có ngân sách nào mới để sao chép`,
      })
    } catch (e) {
      setCopyResult({ month, text: errorMessage(e), error: true })
    }
  }

  const confirmDelete = async () => {
    if (!deleting) return
    setDeleteError(null)
    try {
      await remove.mutateAsync(deleting.id)
      setDeleting(null)
    } catch (e) {
      setDeleteError(errorMessage(e))
    }
  }

  const budgets = data ?? []
  const total = budgets.reduce((s, b) => s + b.amount, 0)
  const spent = budgets.reduce((s, b) => s + b.spent, 0)

  return (
    <>
      <PageHeader
        title="Ngân sách"
        description="Đặt hạn mức chi cho từng danh mục mỗi tháng"
        actions={
          <div className="flex gap-2">
            <Button variant="secondary" loading={copy.isPending} onClick={() => void copyPrevious()}>
              <Copy className="size-4" aria-hidden />
              Sao chép tháng trước
            </Button>
            <Button onClick={() => setEditing({})}>
              <Plus className="size-4" aria-hidden />
              Thêm ngân sách
            </Button>
          </div>
        }
      />

      <div className="mb-4 flex items-center gap-1">
        <IconButton label="Tháng trước" onClick={() => setMonth(shiftMonth(month, -1))}>
          <ChevronLeft className="size-5" />
        </IconButton>
        <span className="min-w-32 text-center font-semibold">{formatMonth(month)}</span>
        <IconButton label="Tháng sau" onClick={() => setMonth(shiftMonth(month, 1))}>
          <ChevronRight className="size-5" />
        </IconButton>
        {month !== currentMonth() && (
          <Button variant="ghost" size="sm" onClick={() => setMonth(currentMonth())}>
            Tháng này
          </Button>
        )}
      </div>

      {copyResult?.month === month && (
        <div className="mb-4">
          <Alert tone={copyResult.error ? 'error' : 'info'}>{copyResult.text}</Alert>
        </div>
      )}

      {isPending ? (
        <LoadingBlock />
      ) : error ? (
        <Alert>{errorMessage(error)}</Alert>
      ) : budgets.length === 0 ? (
        <EmptyState icon={PiggyBank} title={`Chưa có ngân sách cho ${formatMonth(month).toLowerCase()}`}>
          <p>Đặt hạn mức cho các danh mục hay chi (Ăn uống, Mua sắm…) để biết khi nào sắp tiêu quá tay.</p>
          <div className="mt-3 flex flex-wrap justify-center gap-2">
            <Button size="sm" onClick={() => setEditing({})}>
              Đặt ngân sách đầu tiên
            </Button>
            <Button size="sm" variant="secondary" loading={copy.isPending} onClick={() => void copyPrevious()}>
              Chép từ {formatMonth(previous).toLowerCase()}
            </Button>
          </div>
        </EmptyState>
      ) : (
        <div className={`flex flex-col gap-4 transition-opacity ${isFetching ? 'opacity-70' : ''}`}>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <StatTile label="Tổng ngân sách">
              <Money amount={total} />
            </StatTile>
            <StatTile label="Đã chi">
              <Money amount={spent} />
            </StatTile>
            <StatTile label={total - spent >= 0 ? 'Còn lại' : 'Vượt'} className="col-span-2 sm:col-span-1">
              <Money amount={Math.abs(total - spent)} className={total - spent < 0 ? 'text-expense dark:text-red-400' : ''} />
            </StatTile>
          </div>
          <Card className="p-0">
            <ul className="divide-y divide-slate-100 dark:divide-slate-800">
              {budgets.map((b) => (
                <li key={b.id} className="flex items-center gap-2 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <BudgetMeter budget={b} />
                  </div>
                  <IconButton label={`Sửa ngân sách ${b.category.name}`} onClick={() => setEditing({ budget: b })}>
                    <Pencil className="size-4" />
                  </IconButton>
                  <IconButton label={`Xoá ngân sách ${b.category.name}`} onClick={() => setDeleting(b)} className="hover:text-red-600">
                    <Trash2 className="size-4" />
                  </IconButton>
                </li>
              ))}
            </ul>
          </Card>
        </div>
      )}

      <BudgetFormDialog
        open={editing !== null}
        month={month}
        budget={editing?.budget}
        budgeted={budgets.map((b) => b.category.id)}
        onClose={() => setEditing(null)}
      />
      <ConfirmDialog
        open={deleting !== null}
        title="Xoá ngân sách?"
        message={`Bỏ hạn mức "${deleting?.category.name}" của ${formatMonth(month).toLowerCase()}. Giao dịch không bị ảnh hưởng.`}
        confirmLabel="Xoá"
        danger
        loading={remove.isPending}
        error={deleteError ?? undefined}
        onConfirm={() => void confirmDelete()}
        onCancel={() => {
          setDeleting(null)
          setDeleteError(null)
        }}
      />
    </>
  )
}
