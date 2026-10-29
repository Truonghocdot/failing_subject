import dayjs from 'dayjs'
import { Download } from 'lucide-react'
import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { ColumnChart } from '../../components/ColumnChart'
import { inputClass } from '../../components/Field'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { errorMessage, query } from '../../lib/api'
import { downloadCsv } from '../../lib/csv'
import { compactMoney, fullDate, isIsoDate, isoDate, money, percent, sourceLabels, today } from '../../lib/format'
import type { Report, ReportRow } from '../../lib/types'
import { useReport } from './api'
import { HomestaySelect, PageHeader } from './shared'

type Preset = { label: string; range: () => [string, string] }

const t0 = () => dayjs(today())
const presets: Preset[] = [
  { label: 'Tháng này', range: () => [isoDate(t0().startOf('month')), isoDate(t0().endOf('month'))] },
  { label: 'Tháng trước', range: () => [isoDate(t0().subtract(1, 'month').startOf('month')), isoDate(t0().subtract(1, 'month').endOf('month'))] },
  { label: '3 tháng gần đây', range: () => [isoDate(t0().subtract(2, 'month').startOf('month')), isoDate(t0().endOf('month'))] },
  { label: 'Năm nay', range: () => [isoDate(t0().startOf('year')), isoDate(t0().endOf('year'))] },
]

/** /admin/reports?from&to&homestayId – công suất, ADR, RevPAR, doanh thu theo tháng / homestay / nguồn, tỉ lệ huỷ, xuất CSV. */
export function ReportsPage() {
  const [params, setParams] = useSearchParams()
  const [defFrom, defTo] = presets[0].range()
  const from = isIsoDate(params.get('from')) ? params.get('from')! : defFrom
  const to = isIsoDate(params.get('to')) ? params.get('to')! : defTo
  const homestayId = Number(params.get('homestayId')) || null
  const report = useReport(from, to, homestayId)
  const update = (patch: { from?: string; to?: string; homestayId?: number | null }) => {
    const next = { from, to, homestayId, ...patch }
    setParams(new URLSearchParams(query(next)), { replace: true })
  }
  const tooLong = dayjs(to).diff(dayjs(from), 'day') + 1 > 366

  return (
    <div>
      <PageHeader title="Báo cáo" description={`${fullDate(from)} → ${fullDate(to)}`} />

      <div className="mb-6 flex flex-wrap items-end gap-3">
        <div role="group" aria-label="Chọn nhanh kỳ báo cáo" className="flex flex-wrap gap-1">
          {presets.map((p) => {
            const [f, t] = p.range()
            const on = f === from && t === to
            return (
              <button
                key={p.label}
                type="button"
                aria-pressed={on}
                onClick={() => update({ from: f, to: t })}
                className={`rounded-lg border px-3 py-1.5 text-sm font-medium ${on ? 'border-brand-700 bg-brand-700 text-white' : 'border-slate-300 text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800'}`}
              >
                {p.label}
              </button>
            )
          })}
        </div>
        <label className="flex flex-col gap-1.5 text-sm font-medium">
          Từ ngày
          <input type="date" className={`${inputClass} h-9 w-auto`} value={from} max={to} onChange={(e) => isIsoDate(e.target.value) && update({ from: e.target.value })} />
        </label>
        <label className="flex flex-col gap-1.5 text-sm font-medium">
          Đến ngày
          <input type="date" className={`${inputClass} h-9 w-auto`} value={to} min={from} onChange={(e) => isIsoDate(e.target.value) && update({ to: e.target.value })} />
        </label>
        <div className="w-full sm:w-64">
          <HomestaySelect value={homestayId} onChange={(id) => update({ homestayId: id })} />
        </div>
      </div>

      {tooLong ? (
        <Alert>Tối đa 366 ngày mỗi báo cáo – thu hẹp khoảng ngày.</Alert>
      ) : report.isPending ? (
        <LoadingBlock label="Đang tính báo cáo…" />
      ) : report.isError ? (
        <Alert>{errorMessage(report.error)}</Alert>
      ) : (
        <ReportView r={report.data} busy={report.isPlaceholderData} />
      )}
    </div>
  )
}

function ReportView({ r, busy }: { r: Report; busy: boolean }) {
  const t = r.total
  const file = (name: string) => `bao-cao-${name}-${r.from}_${r.to}.csv`
  const rowCsv = (title: string, rows: ReportRow[]) => [
    [title, 'Đêm có thể bán', 'Đêm đã bán', 'Công suất (%)', 'Doanh thu phòng', 'ADR', 'RevPAR', 'Tiền thu thực tế', 'Đặt phòng tạo', 'Huỷ / không đến'],
    ...rows.map((x) => [x.label, x.metrics.sellableNights, x.metrics.soldNights, Math.round(x.metrics.occupancy * 1000) / 10, x.metrics.roomRevenue, x.metrics.adr, x.metrics.revPar, x.collected, x.bookings, x.cancelled]),
  ]

  return (
    <div className="flex flex-col gap-6" aria-busy={busy}>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 xl:grid-cols-6">
        <Tile label="Công suất phòng" value={percent(t.occupancy)} hint={`${t.soldNights}/${t.sellableNights} đêm`} />
        <Tile label="Doanh thu phòng" value={money(t.roomRevenue)} hint="theo đêm ở, gồm phụ thu" />
        <Tile label="ADR" value={money(t.adr)} hint="giá trung bình / đêm đã bán" />
        <Tile label="RevPAR" value={money(t.revPar)} hint="doanh thu / đêm có thể bán" />
        <Tile label="Tiền thu thực tế" value={money(r.collected)} hint={r.refunded > 0 ? `đã trừ hoàn ${money(r.refunded)}` : 'theo thời điểm thu'} />
        <Tile label="Tỉ lệ huỷ" value={percent(r.cancelRate)} hint={`${r.cancelled} huỷ + ${r.noShow} không đến / ${r.bookings} đặt phòng`} />
      </div>

      <Section
        title="Theo tháng"
        action={
          <CsvButton onClick={() => downloadCsv(file('theo-thang'), rowCsv('Tháng', r.months))} />
        }
      >
        {/* 2 đại lượng khác thang đo → 2 biểu đồ riêng, không dùng 2 trục Y */}
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="min-w-0">
            <h3 className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-200">Doanh thu phòng</h3>
            <ColumnChart title="Doanh thu phòng theo tháng" data={r.months.map((m) => ({ key: m.key, label: m.label, value: m.metrics.roomRevenue }))} format={money} axisFormat={compactMoney} />
          </div>
          <div className="min-w-0">
            <h3 className="mb-2 text-sm font-medium text-slate-700 dark:text-slate-200">Công suất phòng</h3>
            <ColumnChart title="Công suất phòng theo tháng" data={r.months.map((m) => ({ key: m.key, label: m.label, value: m.metrics.occupancy }))} format={(v) => percent(v)} axisFormat={(v) => percent(v, 0)} max={1} />
          </div>
        </div>
        <RowTable first="Tháng" rows={r.months} />
      </Section>

      <Section title="Theo homestay" action={<CsvButton onClick={() => downloadCsv(file('theo-homestay'), rowCsv('Homestay', r.homestays))} />}>
        {r.homestays.length === 0 ? <p className="text-sm text-slate-500">Không có dữ liệu.</p> : <RowTable first="Homestay" rows={r.homestays} />}
      </Section>

      <Section
        title="Theo nguồn đặt"
        action={
          <CsvButton
            onClick={() =>
              downloadCsv(file('theo-nguon'), [
                ['Nguồn', 'Đặt phòng tạo', 'Huỷ / không đến', 'Đêm đã bán', 'Doanh thu phòng'],
                ...r.sources.map((s) => [sourceLabels[s.source], s.bookings, s.cancelled, s.soldNights, s.roomRevenue]),
              ])
            }
          />
        }
      >
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-xs text-slate-500 dark:text-slate-400">
              <tr>
                <th className="py-2 pr-3 font-medium">Nguồn</th>
                <th className="py-2 pr-3 text-right font-medium">Đặt phòng tạo</th>
                <th className="py-2 pr-3 text-right font-medium">Huỷ / không đến</th>
                <th className="py-2 pr-3 text-right font-medium">Đêm đã bán</th>
                <th className="py-2 text-right font-medium">Doanh thu phòng</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 tabular-nums dark:divide-slate-800">
              {r.sources.map((s) => (
                <tr key={s.source}>
                  <th scope="row" className="py-2 pr-3 text-left font-normal">
                    {sourceLabels[s.source]}
                  </th>
                  <td className="py-2 pr-3 text-right">{s.bookings}</td>
                  <td className="py-2 pr-3 text-right">{s.cancelled}</td>
                  <td className="py-2 pr-3 text-right">{s.soldNights}</td>
                  <td className="py-2 text-right">{money(s.roomRevenue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>

      <p className="text-xs text-slate-500 dark:text-slate-400">
        Đêm có thể bán = phòng đang mở bán × số ngày − đêm khoá bảo trì · đêm đã bán = đêm của đặt phòng đã xác nhận / đang ở / đã trả (chờ cọc chưa tính) · doanh thu phòng không gồm phí dọn ·
        đặt phòng tạo / tỉ lệ huỷ tính theo ngày tạo trong kỳ{r.expired > 0 && ` (${r.expired} đặt phòng hết hạn giữ chỗ không tính là huỷ)`}.
      </p>
    </div>
  )
}

function Tile({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <Card className="p-4">
      <p className="text-sm text-slate-600 dark:text-slate-300">{label}</p>
      <p className="mt-1 text-xl font-semibold tabular-nums">{value}</p>
      <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">{hint}</p>
    </Card>
  )
}

function Section({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <Card className="flex flex-col gap-4 p-5">
      <div className="flex items-center justify-between gap-3">
        <h2 className="font-semibold">{title}</h2>
        {action}
      </div>
      {children}
    </Card>
  )
}

function CsvButton({ onClick }: { onClick: () => void }) {
  return (
    <Button variant="secondary" size="sm" onClick={onClick}>
      <Download className="size-4" aria-hidden />
      Xuất CSV
    </Button>
  )
}

function RowTable({ first, rows }: { first: string; rows: ReportRow[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[44rem] text-sm">
        <thead className="text-left text-xs text-slate-500 dark:text-slate-400">
          <tr>
            <th className="py-2 pr-3 font-medium">{first}</th>
            <th className="py-2 pr-3 text-right font-medium">Công suất</th>
            <th className="py-2 pr-3 text-right font-medium">Đêm bán / có thể bán</th>
            <th className="py-2 pr-3 text-right font-medium">Doanh thu phòng</th>
            <th className="py-2 pr-3 text-right font-medium">ADR</th>
            <th className="py-2 pr-3 text-right font-medium">RevPAR</th>
            <th className="py-2 pr-3 text-right font-medium">Tiền thu</th>
            <th className="py-2 text-right font-medium">Đặt / huỷ</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 tabular-nums dark:divide-slate-800">
          {rows.map((x) => (
            <tr key={x.key}>
              <th scope="row" className="py-2 pr-3 text-left font-normal">
                {x.label}
              </th>
              <td className="py-2 pr-3 text-right">{percent(x.metrics.occupancy)}</td>
              <td className="py-2 pr-3 text-right">
                {x.metrics.soldNights}/{x.metrics.sellableNights}
              </td>
              <td className="py-2 pr-3 text-right">{money(x.metrics.roomRevenue)}</td>
              <td className="py-2 pr-3 text-right">{money(x.metrics.adr)}</td>
              <td className="py-2 pr-3 text-right">{money(x.metrics.revPar)}</td>
              <td className="py-2 pr-3 text-right">{money(x.collected)}</td>
              <td className="py-2 text-right">
                {x.bookings}/{x.cancelled}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
