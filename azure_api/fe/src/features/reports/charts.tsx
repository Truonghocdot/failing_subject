import { useMemo, type ReactNode } from 'react'
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Money } from '../../components/Money'
import { formatCompact, formatPeriod, formatPeriodLong } from '../../lib/format'
import type { CategoryReportItem, TrendPoint } from '../../lib/types'

// Quy ước biểu đồ (skill dataviz): nét mảnh, 2px khe nền giữa các mảng, lưới mờ, chữ dùng màu chữ (không dùng màu series),
// tooltip khi rê chuột + luôn có bảng / danh sách số liệu để không phải đọc bằng màu hay tooltip.

const MAX_SLICES = 5
const OTHER_COLOR = '#94a3b8'

/** Phần props tooltip của Recharts mà các tooltip tự vẽ cần dùng. */
type TooltipProps = { active?: boolean; payload?: ReadonlyArray<{ payload?: unknown }> }

type Slice = { key: string; name: string; color: string; total: number; percent: number }

/** Top 5 danh mục + gộp phần còn lại thành "Khác" – biểu đồ tròn quá 6 mảng không đọc được. */
function toSlices(items: CategoryReportItem[]): Slice[] {
  const slices: Slice[] = items.slice(0, MAX_SLICES).map((i) => ({
    key: String(i.category.id),
    name: i.category.name,
    color: i.category.color,
    total: i.total,
    percent: i.percent,
  }))
  const rest = items.slice(MAX_SLICES)
  if (rest.length) {
    const total = rest.reduce((s, i) => s + i.total, 0)
    const percent = Math.round(rest.reduce((s, i) => s + i.percent, 0) * 10) / 10
    slices.push({ key: 'other', name: `Khác (${rest.length} danh mục)`, color: OTHER_COLOR, total, percent })
  }
  return slices
}

/** Tròn cơ cấu theo danh mục + chú thích có số tiền / % (chú thích chính là bảng số liệu). */
export function CategoryDonut({ items, label }: { items: CategoryReportItem[]; label: string }) {
  const slices = useMemo(() => toSlices(items), [items])
  const total = items.reduce((s, i) => s + i.total, 0)
  return (
    <div className="flex flex-col items-center gap-4 sm:flex-row sm:items-center">
      <div className="relative size-44 shrink-0" role="img" aria-label={`Biểu đồ tròn ${label.toLowerCase()} theo danh mục`}>
        <ResponsiveContainer width="100%" height="100%" initialDimension={{ width: 176, height: 176 }}>
          <PieChart>
            <Pie
              data={slices}
              dataKey="total"
              nameKey="name"
              innerRadius="62%"
              outerRadius="100%"
              stroke="var(--chart-surface)"
              strokeWidth={2}
              startAngle={90}
              endAngle={-270}
              isAnimationActive={false}
            >
              {slices.map((s) => (
                <Cell key={s.key} fill={s.color} />
              ))}
            </Pie>
            <Tooltip content={SliceTooltip} />
          </PieChart>
        </ResponsiveContainer>
        <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center text-center">
          <span className="text-xs text-slate-500 dark:text-slate-400">{label}</span>
          <Money amount={total} className="text-sm font-semibold" />
        </div>
      </div>
      <ul className="w-full min-w-0 flex-1 space-y-2 text-sm sm:max-w-md">
        {slices.map((s) => (
          <li key={s.key} className="flex items-center gap-2">
            <span className="size-2.5 shrink-0 rounded-full" style={{ backgroundColor: s.color }} aria-hidden />
            <span className="min-w-0 flex-1 truncate">{s.name}</span>
            <Money amount={s.total} className="font-medium" />
            <span className="w-12 text-right text-xs text-slate-500 tabular-nums dark:text-slate-400">{s.percent.toLocaleString('vi-VN')}%</span>
          </li>
        ))}
      </ul>
    </div>
  )
}

function SliceTooltip({ active, payload }: TooltipProps) {
  const slice = payload?.[0]?.payload as Slice | undefined
  if (!active || !slice) return null
  return (
    <TooltipBox>
      <p className="flex items-center gap-2 font-medium">
        <span className="size-2.5 rounded-full" style={{ backgroundColor: slice.color }} aria-hidden />
        {slice.name}
      </p>
      <p className="mt-1">
        <Money amount={slice.total} /> · {slice.percent.toLocaleString('vi-VN')}%
      </p>
    </TooltipBox>
  )
}

/** Cột thu – chi theo kỳ (1 trục, 2 series cạnh nhau). */
export function TrendChart({ points, height = 240, showTable = false }: { points: TrendPoint[]; height?: number; showTable?: boolean }) {
  const data = useMemo(() => points.map((p) => ({ ...p, label: formatPeriod(p.period) })), [points])
  return (
    <div>
      <ul className="mb-2 flex gap-4 text-xs text-slate-600 dark:text-slate-300" aria-label="Chú thích">
        <li className="flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-[var(--chart-income)]" aria-hidden />
          Thu
        </li>
        <li className="flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-[var(--chart-expense)]" aria-hidden />
          Chi
        </li>
      </ul>
      <div className="text-slate-500 dark:text-slate-400" role="img" aria-label="Biểu đồ cột thu chi theo kỳ">
        <ResponsiveContainer width="100%" height={height} initialDimension={{ width: 320, height }}>
          <BarChart data={data} barGap={2} barCategoryGap="20%" margin={{ top: 4, right: 4, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="currentColor" strokeOpacity={0.15} />
            <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: 'currentColor', fontSize: 12 }} minTickGap={8} />
            <YAxis
              tickFormatter={formatCompact}
              tickLine={false}
              axisLine={false}
              tick={{ fill: 'currentColor', fontSize: 12 }}
              width={48}
              allowDecimals={false}
            />
            <Tooltip content={TrendTooltip} cursor={{ fill: 'currentColor', fillOpacity: 0.08 }} />
            <Bar dataKey="income" name="Thu" fill="var(--chart-income)" radius={[4, 4, 0, 0]} maxBarSize={28} isAnimationActive={false} />
            <Bar dataKey="expense" name="Chi" fill="var(--chart-expense)" radius={[4, 4, 0, 0]} maxBarSize={28} isAnimationActive={false} />
          </BarChart>
        </ResponsiveContainer>
      </div>
      {showTable && (
        <details className="mt-3 text-sm">
          <summary className="cursor-pointer text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white">Xem dạng bảng</summary>
          <TrendTable points={points} />
        </details>
      )}
    </div>
  )
}

function TrendTooltip({ active, payload }: TooltipProps) {
  const point = payload?.[0]?.payload as TrendPoint | undefined
  if (!active || !point) return null
  return (
    <TooltipBox>
      <p className="mb-1 font-medium">{formatPeriodLong(point.period)}</p>
      <p className="flex justify-between gap-4">
        <span>Thu</span>
        <Money amount={point.income} kind="income" />
      </p>
      <p className="flex justify-between gap-4">
        <span>Chi</span>
        <Money amount={point.expense} kind="expense" />
      </p>
      <p className="mt-1 flex justify-between gap-4 border-t border-slate-200 pt-1 dark:border-slate-700">
        <span>Chênh lệch</span>
        <Money amount={point.income - point.expense} />
      </p>
    </TooltipBox>
  )
}

export function TrendTable({ points }: { points: TrendPoint[] }) {
  return (
    <div className="mt-2 overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="text-left text-xs text-slate-500 dark:text-slate-400">
          <tr>
            <th className="py-1.5 font-medium">Kỳ</th>
            <th className="py-1.5 text-right font-medium">Thu</th>
            <th className="py-1.5 text-right font-medium">Chi</th>
            <th className="py-1.5 text-right font-medium">Chênh lệch</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {points.map((p) => (
            <tr key={p.period}>
              <td className="py-1.5">{formatPeriodLong(p.period)}</td>
              <td className="py-1.5 text-right"><Money amount={p.income} /></td>
              <td className="py-1.5 text-right"><Money amount={p.expense} /></td>
              <td className="py-1.5 text-right"><Money amount={p.income - p.expense} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function TooltipBox({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs text-slate-700 shadow-lg dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
      {children}
    </div>
  )
}
