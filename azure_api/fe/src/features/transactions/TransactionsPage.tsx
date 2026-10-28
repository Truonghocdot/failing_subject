import { ChevronLeft, ChevronRight, Download, Plus, ReceiptText, Search, X } from 'lucide-react'
import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Button, IconButton } from '../../components/Button'
import { inputClass } from '../../components/Field'
import { Alert, Card, EmptyState, LoadingBlock } from '../../components/Feedback'
import { Money } from '../../components/Money'
import { PageHeader } from '../../components/PageHeader'
import { StatTile } from '../../components/StatTile'
import { errorMessage } from '../../lib/api'
import { currentMonth, formatDayHeading, formatMonth, monthRange, shiftMonth } from '../../lib/format'
import type { Transaction, TransactionKind } from '../../lib/types'
import { useCategories } from '../categories/api'
import { useWallets } from '../wallets/api'
import { downloadTransactionsCsv, transactionKindLabels, useTransactions, type TransactionFilters } from './api'
import { useTransactionDialog } from './TransactionDialogProvider'
import { TransactionRow } from './TransactionRow'

const PAGE_SIZE = 30
const kinds: TransactionKind[] = ['expense', 'income', 'transfer']

/** Bộ lọc nằm trên URL → F5 / chia sẻ link / Back đều giữ nguyên. */
function useFiltersFromUrl() {
  const [params, setParams] = useSearchParams()
  const custom = params.has('from') || params.has('to')
  const month = /^\d{4}-\d{2}$/.test(params.get('month') ?? '') ? params.get('month')! : currentMonth()
  const range = custom ? { from: params.get('from') ?? monthRange(month).from, to: params.get('to') ?? monthRange(month).to } : monthRange(month)
  const num = (key: string) => (Number(params.get(key)) > 0 ? Number(params.get(key)) : undefined)
  const kind = kinds.find((k) => k === params.get('kind'))

  const filters: TransactionFilters = {
    ...range,
    walletId: num('walletId'),
    categoryId: num('categoryId'),
    kind,
    q: params.get('q') ?? undefined,
    page: num('page') ?? 1,
    pageSize: PAGE_SIZE,
  }

  // `prev` của setSearchParams là URL lúc render tạo ra hàm (không phải URL mới nhất) → hàm gọi trễ
  // (VD ô tìm kiếm chờ 300ms) sẽ ghi đè lại bộ lọc vừa bỏ. Luôn dựa trên URL mới nhất qua ref.
  const latest = useRef(params)
  useLayoutEffect(() => {
    latest.current = params
  })

  /** Đổi 1 hay nhiều tham số; đổi bộ lọc thì về trang 1. */
  const update = useCallback(
    (changes: Record<string, string | number | undefined | null>, keepPage = false) => {
      const next = new URLSearchParams(latest.current)
      for (const [k, v] of Object.entries(changes)) {
        if (v === undefined || v === null || v === '') next.delete(k)
        else next.set(k, String(v))
      }
      if (!keepPage) next.delete('page')
      latest.current = next
      setParams(next, { replace: true })
    },
    [setParams],
  )
  return { filters, month, custom, update }
}

export function TransactionsPage() {
  const { filters, month, custom, update } = useFiltersFromUrl()
  const { data, isPending, isFetching, error } = useTransactions(filters)
  const { openCreate, openEdit } = useTransactionDialog()
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)

  const groups = useMemo(() => groupByDay(data?.items ?? []), [data])
  const hasFilter = !!(filters.walletId || filters.categoryId || filters.kind || filters.q)

  const exportCsv = async () => {
    setExporting(true)
    setExportError(null)
    try {
      await downloadTransactionsCsv(filters)
    } catch (e) {
      setExportError(errorMessage(e))
    } finally {
      setExporting(false)
    }
  }

  return (
    <>
      <PageHeader
        title="Giao dịch"
        description="Lịch sử thu chi – bấm vào một dòng để sửa"
        actions={
          <div className="flex gap-2">
            <Button variant="secondary" loading={exporting} onClick={() => void exportCsv()} disabled={!data?.totalItems}>
              <Download className="size-4" aria-hidden />
              Xuất CSV
            </Button>
            <Button onClick={() => openCreate(filters.kind)}>
              <Plus className="size-4" aria-hidden />
              Thêm giao dịch
            </Button>
          </div>
        }
      />

      <FilterBar filters={filters} month={month} custom={custom} update={update} />
      {exportError && (
        <div className="mb-4">
          <Alert>{exportError}</Alert>
        </div>
      )}

      {isPending ? (
        <LoadingBlock />
      ) : error ? (
        <Alert>{errorMessage(error)}</Alert>
      ) : (
        <div className={`flex flex-col gap-4 transition-opacity ${isFetching ? 'opacity-70' : ''}`}>
          {/* Điện thoại: Thu | Chi một hàng, Chênh lệch cả hàng dưới – 3 cột ở 360px sẽ cắt mất số */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <SummaryTile label="Tổng thu" amount={data.totals.income} kind="income" />
            <SummaryTile label="Tổng chi" amount={data.totals.expense} kind="expense" />
            <SummaryTile label="Chênh lệch" amount={data.totals.income - data.totals.expense} className="col-span-2 sm:col-span-1" />
          </div>

          {groups.length === 0 ? (
            <EmptyState icon={ReceiptText} title={hasFilter ? 'Không có giao dịch nào khớp bộ lọc' : 'Chưa có giao dịch trong khoảng này'}>
              {hasFilter ? (
                <Button variant="secondary" size="sm" onClick={() => update({ walletId: null, categoryId: null, kind: null, q: null })}>
                  Bỏ lọc
                </Button>
              ) : (
                <Button size="sm" onClick={() => openCreate()}>
                  Thêm khoản chi đầu tiên
                </Button>
              )}
            </EmptyState>
          ) : (
            groups.map((g) => (
              <Card key={g.date} className="p-0">
                <h2 className="flex items-center justify-between border-b border-slate-100 px-4 py-2 text-sm dark:border-slate-800">
                  <span className="font-medium text-slate-700 dark:text-slate-200">{formatDayHeading(g.date)}</span>
                  <Money amount={g.net} className="text-xs text-slate-500 dark:text-slate-400" />
                </h2>
                <ul className="divide-y divide-slate-100 dark:divide-slate-800">
                  {g.items.map((t) => (
                    <li key={t.id}>
                      <TransactionRow tx={t} onClick={() => openEdit(t)} />
                    </li>
                  ))}
                </ul>
              </Card>
            ))
          )}

          {data.totalPages > 1 && (
            <nav aria-label="Phân trang" className="flex items-center justify-center gap-3 text-sm">
              <IconButton label="Trang trước" disabled={data.page <= 1} onClick={() => update({ page: data.page - 1 }, true)} className="disabled:opacity-40">
                <ChevronLeft className="size-5" />
              </IconButton>
              <span>
                Trang {data.page}/{data.totalPages} · {data.totalItems} giao dịch
              </span>
              <IconButton label="Trang sau" disabled={data.page >= data.totalPages} onClick={() => update({ page: data.page + 1 }, true)} className="disabled:opacity-40">
                <ChevronRight className="size-5" />
              </IconButton>
            </nav>
          )}
        </div>
      )}
    </>
  )
}

function SummaryTile({ label, amount, kind, className = '' }: { label: string; amount: number; kind?: TransactionKind; className?: string }) {
  return (
    <StatTile label={label} className={className}>
      <Money amount={amount} kind={kind} />
    </StatTile>
  )
}

function groupByDay(items: Transaction[]) {
  const groups: { date: string; net: number; items: Transaction[] }[] = []
  for (const t of items) {
    let g = groups.at(-1)
    if (g?.date !== t.occurredOn) groups.push((g = { date: t.occurredOn, net: 0, items: [] }))
    g.items.push(t)
    g.net += t.kind === 'income' ? t.amount : t.kind === 'expense' ? -t.amount : 0
  }
  return groups
}

type FilterBarProps = {
  filters: TransactionFilters
  month: string
  custom: boolean
  update: (changes: Record<string, string | number | undefined | null>) => void
}

function FilterBar({ filters, month, custom, update }: FilterBarProps) {
  const wallets = useWallets(true)
  const categories = useCategories(true)
  const urlQ = filters.q ?? ''
  const [search, setSearch] = useState(urlQ)

  // URL đổi từ ngoài (Bỏ lọc, Back) → ô tìm kiếm hiện theo URL
  const [syncedQ, setSyncedQ] = useState(urlQ)
  if (urlQ !== syncedQ) {
    setSyncedQ(urlQ)
    setSearch(urlQ)
  }

  // Gõ tìm kiếm: chờ 300ms mới cập nhật URL / gọi API
  useEffect(() => {
    if (search.trim() === urlQ) return
    const id = setTimeout(() => update({ q: search.trim() || null }), 300)
    return () => clearTimeout(id)
  }, [search, urlQ, update])

  const selectClass = `${inputClass} sm:w-auto`
  return (
    <Card className="mb-4 flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        {custom ? (
          <div className="flex flex-wrap items-center gap-2">
            <label className="sr-only" htmlFor="filter-from">Từ ngày</label>
            <input id="filter-from" type="date" className={`${inputClass} w-auto`} value={filters.from} onChange={(e) => update({ from: e.target.value })} />
            <span className="text-slate-400">–</span>
            <label className="sr-only" htmlFor="filter-to">Đến ngày</label>
            <input id="filter-to" type="date" className={`${inputClass} w-auto`} value={filters.to} onChange={(e) => update({ to: e.target.value })} />
            <Button variant="ghost" size="sm" onClick={() => update({ from: null, to: null })}>
              Theo tháng
            </Button>
          </div>
        ) : (
          <div className="flex items-center gap-1">
            <IconButton label="Tháng trước" onClick={() => update({ month: shiftMonth(month, -1) })}>
              <ChevronLeft className="size-5" />
            </IconButton>
            <span className="min-w-32 text-center font-semibold">{formatMonth(month)}</span>
            <IconButton label="Tháng sau" onClick={() => update({ month: shiftMonth(month, 1) })}>
              <ChevronRight className="size-5" />
            </IconButton>
            {month !== currentMonth() && (
              <Button variant="ghost" size="sm" onClick={() => update({ month: null })}>
                Tháng này
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={() => update({ from: filters.from, to: filters.to })}>
              Tuỳ chọn ngày
            </Button>
          </div>
        )}
      </div>

      <div className="grid gap-2 sm:flex sm:flex-wrap">
        <div className="relative sm:min-w-56 sm:flex-1">
          <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" aria-hidden />
          <input type="search" aria-label="Tìm trong ghi chú" placeholder="Tìm trong ghi chú…" className={`${inputClass} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <select aria-label="Lọc theo loại" className={selectClass} value={filters.kind ?? ''} onChange={(e) => update({ kind: e.target.value, categoryId: null })}>
          <option value="">Tất cả loại</option>
          {kinds.map((k) => (
            <option key={k} value={k}>
              {transactionKindLabels[k]}
            </option>
          ))}
        </select>
        <select aria-label="Lọc theo ví" className={selectClass} value={filters.walletId ?? ''} onChange={(e) => update({ walletId: e.target.value })}>
          <option value="">Tất cả ví</option>
          {wallets.data?.items.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
              {w.isArchived ? ' (lưu trữ)' : ''}
            </option>
          ))}
        </select>
        <select
          aria-label="Lọc theo danh mục"
          className={selectClass}
          value={filters.categoryId ?? ''}
          disabled={filters.kind === 'transfer'}
          onChange={(e) => update({ categoryId: e.target.value })}
        >
          <option value="">Tất cả danh mục</option>
          {(['expense', 'income'] as const)
            .filter((k) => !filters.kind || filters.kind === k)
            .map((k) => (
              <optgroup key={k} label={k === 'expense' ? 'Khoản chi' : 'Khoản thu'}>
                {categories.data
                  ?.filter((c) => c.kind === k)
                  .map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
              </optgroup>
            ))}
        </select>
        {(filters.walletId || filters.categoryId || filters.kind || filters.q) && (
          <Button
            variant="ghost"
            onClick={() => update({ walletId: null, categoryId: null, kind: null, q: null })}
          >
            <X className="size-4" aria-hidden />
            Bỏ lọc
          </Button>
        )}
      </div>
    </Card>
  )
}
