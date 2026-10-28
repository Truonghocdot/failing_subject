import { ChartPie, ReceiptText } from 'lucide-react'
import { useSearchParams } from 'react-router'
import { CategoryIcon } from '../../components/CategoryIcon'
import { inputClass } from '../../components/Field'
import { Alert, Card, EmptyState, LoadingBlock } from '../../components/Feedback'
import { Money } from '../../components/Money'
import { PageHeader } from '../../components/PageHeader'
import { Segmented } from '../../components/Segmented'
import { StatTile } from '../../components/StatTile'
import { errorMessage } from '../../lib/api'
import { formatDate } from '../../lib/format'
import type { CategoryKind, CategoryReportItem } from '../../lib/types'
import { categoryKindOptions } from '../categories/api'
import { useCategoryReport, useReportSummary, useTrend, type DateRange } from './api'
import { CategoryDonut, TrendChart } from './charts'
import { presetRange, rangePresets, trendGroupFor, type Preset } from './ranges'

function useReportParams() {
  const [params, setParams] = useSearchParams()
  const preset: Preset = rangePresets.find((p) => p.value === params.get('range'))?.value ?? 'this-month'
  const kind: CategoryKind = params.get('kind') === 'income' ? 'income' : 'expense'
  const fallback = presetRange('this-month')
  const range: DateRange =
    preset === 'custom' ? { from: params.get('from') || fallback.from, to: params.get('to') || fallback.to } : presetRange(preset)

  const update = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params)
    for (const [k, v] of Object.entries(changes)) {
      if (v === null || v === '') next.delete(k)
      else next.set(k, v)
    }
    setParams(next, { replace: true })
  }
  return { preset, kind, range, update }
}

export function ReportsPage() {
  const { preset, kind, range, update } = useReportParams()
  const summary = useReportSummary(range)
  const byCategory = useCategoryReport(range, kind)
  const trend = useTrend(range, trendGroupFor(range))
  const error = summary.error ?? byCategory.error ?? trend.error

  const choosePreset = (p: Preset) =>
    // Chuyển sang "Tuỳ chọn" thì giữ khoảng đang xem làm điểm bắt đầu
    update(p === 'custom' ? { range: p, from: range.from, to: range.to } : { range: p === 'this-month' ? null : p, from: null, to: null })

  return (
    <>
      <PageHeader title="Báo cáo" description="Cơ cấu và xu hướng thu chi – chuyển tiền giữa các ví không tính" />

      <Card className="mb-4 flex flex-col gap-3">
        <div className="flex flex-wrap gap-2" role="group" aria-label="Khoảng thời gian">
          {rangePresets.map((p) => (
            <button
              key={p.value}
              type="button"
              aria-pressed={preset === p.value}
              onClick={() => choosePreset(p.value)}
              className={`rounded-full border px-3 py-1 text-sm transition-colors ${
                preset === p.value
                  ? 'border-brand-600 bg-brand-50 font-medium text-brand-700 dark:bg-brand-700/20 dark:text-brand-100'
                  : 'border-slate-300 text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800'
              }`}
            >
              {p.label}
            </button>
          ))}
        </div>
        {preset === 'custom' ? (
          <div className="flex flex-wrap items-center gap-2">
            <label className="sr-only" htmlFor="report-from">Từ ngày</label>
            <input id="report-from" type="date" className={`${inputClass} w-auto`} value={range.from} onChange={(e) => update({ from: e.target.value })} />
            <span className="text-slate-400">–</span>
            <label className="sr-only" htmlFor="report-to">Đến ngày</label>
            <input id="report-to" type="date" className={`${inputClass} w-auto`} value={range.to} onChange={(e) => update({ to: e.target.value })} />
          </div>
        ) : (
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {formatDate(range.from)} – {formatDate(range.to)}
          </p>
        )}
      </Card>

      {error ? (
        <Alert>{errorMessage(error)}</Alert>
      ) : !summary.data ? (
        <LoadingBlock />
      ) : (
        <div className={`flex flex-col gap-4 transition-opacity ${summary.isFetching || byCategory.isFetching || trend.isFetching ? 'opacity-70' : ''}`}>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatTile label="Tổng thu">
              <Money amount={summary.data.income} kind="income" />
            </StatTile>
            <StatTile label="Tổng chi">
              <Money amount={summary.data.expense} kind="expense" />
            </StatTile>
            <StatTile label="Chênh lệch">
              <Money amount={summary.data.net} />
            </StatTile>
            <StatTile label="Chi trung bình / ngày">
              <Money amount={summary.data.dailyAverageExpense} />
            </StatTile>
          </div>

          {summary.data.transactionCount === 0 ? (
            <EmptyState icon={ReceiptText} title="Chưa có giao dịch thu chi trong khoảng này">
              Chọn khoảng khác hoặc thêm giao dịch bằng nút "Thêm giao dịch".
            </EmptyState>
          ) : (
            <>
              <Card>
                <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                  <h2 className="font-semibold">Theo danh mục</h2>
                  <Segmented label="Loại báo cáo" options={categoryKindOptions} value={kind} onChange={(k) => update({ kind: k === 'expense' ? null : k })} />
                </div>
                {byCategory.isPending ? (
                  <LoadingBlock />
                ) : byCategory.data?.length ? (
                  <>
                    <CategoryDonut items={byCategory.data} label={kind === 'expense' ? 'Tổng chi' : 'Tổng thu'} />
                    <CategoryTable items={byCategory.data} kind={kind} />
                  </>
                ) : (
                  <EmptyState icon={ChartPie} title={kind === 'expense' ? 'Không có khoản chi nào' : 'Không có khoản thu nào'} />
                )}
              </Card>

              <Card>
                <h2 className="mb-3 font-semibold">Thu chi {trendGroupFor(range) === 'day' ? 'theo ngày' : 'theo tháng'}</h2>
                {trend.data ? <TrendChart points={trend.data} showTable /> : <LoadingBlock />}
              </Card>
            </>
          )}
        </div>
      )}
    </>
  )
}

function CategoryTable({ items, kind }: { items: CategoryReportItem[]; kind: CategoryKind }) {
  return (
    <div className="mt-5 overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="sr-only">Chi tiết {kind === 'expense' ? 'chi' : 'thu'} theo danh mục</caption>
        <thead className="text-left text-xs text-slate-500 dark:text-slate-400">
          <tr>
            <th className="py-1.5 font-medium">Danh mục</th>
            <th className="hidden py-1.5 text-right font-medium sm:table-cell">Số giao dịch</th>
            <th className="py-1.5 text-right font-medium">Tỷ lệ</th>
            <th className="py-1.5 text-right font-medium">Số tiền</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {items.map((i) => (
            <tr key={i.category.id}>
              <td className="py-2">
                <span className="flex items-center gap-2">
                  <CategoryIcon icon={i.category.icon} color={i.category.color} size="sm" />
                  <span className="truncate">{i.category.name}</span>
                </span>
              </td>
              <td className="hidden py-2 text-right tabular-nums sm:table-cell">{i.count}</td>
              <td className="py-2 text-right tabular-nums">{i.percent.toLocaleString('vi-VN')}%</td>
              <td className="py-2 text-right font-medium">
                <Money amount={i.total} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
