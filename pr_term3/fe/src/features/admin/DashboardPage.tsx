import dayjs from 'dayjs'
import { CalendarPlus, ChevronLeft, ChevronRight } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { IconButton } from '../../components/Button'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { errorMessage } from '../../lib/api'
import { useNow } from '../../lib/useNow'
import { addDays, countdown, dayLabel, fullDate, isIsoDate, today } from '../../lib/format'
import type { BookingSummary, OccupancyDay } from '../../lib/types'
import { useDashboard } from './api'
import { BookingRow, linkButton, PageHeader } from './shared'

/** /admin?date= – khách đến / đi / đang ở, chờ cọc sắp hết hạn, công suất 7 ngày. */
export function DashboardPage() {
  const [params, setParams] = useSearchParams()
  const date = isIsoDate(params.get('date')) ? params.get('date')! : today()
  const dashboard = useDashboard(date)
  const go = (d: string) => setParams(d === today() ? {} : { date: d }, { replace: true })

  return (
    <div>
      <PageHeader
        title="Hôm nay"
        description={
          <span className="flex items-center gap-1">
            <IconButton label="Ngày trước" onClick={() => go(addDays(date, -1))}>
              <ChevronLeft className="size-4" aria-hidden />
            </IconButton>
            <span className="font-medium text-slate-800 capitalize dark:text-slate-100">{fullDate(date)}</span>
            <IconButton label="Ngày sau" onClick={() => go(addDays(date, 1))}>
              <ChevronRight className="size-4" aria-hidden />
            </IconButton>
            {date !== today() && (
              <button type="button" className="ml-1 text-sm font-medium text-brand-700 hover:underline dark:text-brand-100" onClick={() => go(today())}>
                Về hôm nay
              </button>
            )}
          </span>
        }
        actions={
          <Link to="/admin/bookings/new" className={linkButton}>
            <CalendarPlus className="size-4" aria-hidden />
            Tạo đặt phòng
          </Link>
        }
      />

      {dashboard.isPending ? (
        <LoadingBlock />
      ) : dashboard.isError ? (
        <Alert>{errorMessage(dashboard.error)}</Alert>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <Stat label="Khách đến" value={dashboard.data.arrivals.length} />
            <Stat label="Khách đi" value={dashboard.data.departures.length} />
            <Stat label="Đang ở" value={dashboard.data.inHouse.length} />
            <Stat label="Chờ cọc" value={dashboard.data.pendingHolds.length} />
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            <Section title="Khách đến" empty="Không có khách nhận phòng" items={dashboard.data.arrivals} />
            <Section title="Khách đi" empty="Không có khách trả phòng" items={dashboard.data.departures} />
            <Section title="Đang ở" empty="Chưa có khách đang ở" items={dashboard.data.inHouse} />
            <Section
              title="Chờ cọc (sắp hết hạn giữ chỗ trước)"
              empty="Không có đặt phòng chờ cọc"
              items={dashboard.data.pendingHolds}
              extra={(b) => b.holdUntil && <HoldLeft until={b.holdUntil} />}
            />
          </div>

          <Occupancy days={dashboard.data.next7Days} />
        </div>
      )}
    </div>
  )
}

function HoldLeft({ until }: { until: string }) {
  const left = countdown(until, useNow(30_000))
  return <span className="text-xs text-amber-800 dark:text-amber-300">{left ? `Còn ${left} giữ chỗ` : 'Đã quá hạn giữ chỗ'}</span>
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <Card className="p-4">
      <p className="text-sm text-slate-600 dark:text-slate-300">{label}</p>
      <p className="mt-1 text-3xl font-semibold tabular-nums">{value}</p>
    </Card>
  )
}

function Section({ title, empty, items, extra }: { title: string; empty: string; items: BookingSummary[]; extra?: (b: BookingSummary) => ReactNode }) {
  return (
    <Card className="p-3">
      <h2 className="px-3 pt-1 pb-2 font-semibold">
        {title} <span className="font-normal text-slate-500 dark:text-slate-400">({items.length})</span>
      </h2>
      {items.length === 0 ? (
        <p className="px-3 pb-3 text-sm text-slate-500 dark:text-slate-400">{empty}</p>
      ) : (
        <ul className="flex flex-col divide-y divide-slate-100 dark:divide-slate-800">
          {items.map((b) => (
            <BookingRow key={b.id} b={b} extra={extra?.(b)} />
          ))}
        </ul>
      )}
    </Card>
  )
}

/** Công suất 7 ngày: bảng (số liệu là chữ), thanh ngang chỉ minh hoạ. */
function Occupancy({ days }: { days: OccupancyDay[] }) {
  return (
    <Card className="p-4">
      <h2 className="mb-3 font-semibold">Công suất 7 ngày tới</h2>
      <table className="w-full text-sm">
        <thead className="sr-only">
          <tr>
            <th>Ngày</th>
            <th>Tỉ lệ</th>
            <th>Đã bán / có thể bán</th>
          </tr>
        </thead>
        <tbody>
          {days.map((d) => (
            <tr key={d.date}>
              <th scope="row" className="w-20 py-1.5 pr-3 text-left font-normal text-slate-600 capitalize dark:text-slate-300">
                {dayjs(d.date).isSame(dayjs(), 'day') ? 'Hôm nay' : dayLabel(d.date)}
              </th>
              <td className="py-1.5">
                <div className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800" aria-hidden>
                  <div className="h-full rounded-full bg-brand-600" style={{ width: `${Math.round(d.rate * 100)}%` }} />
                </div>
              </td>
              <td className="w-28 py-1.5 pl-3 text-right tabular-nums">
                <span className="font-medium">{Math.round(d.rate * 100)}%</span>
                <span className="text-slate-500 dark:text-slate-400">
                  {' '}
                  · {d.sold}/{d.sellable}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </Card>
  )
}
