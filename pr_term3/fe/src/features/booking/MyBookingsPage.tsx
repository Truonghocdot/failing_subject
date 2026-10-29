import { CalendarX2 } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { Alert, EmptyState, LoadingBlock } from '../../components/Feedback'
import { StatusBadge } from '../../components/StatusBadge'
import { errorMessage } from '../../lib/api'
import { money, stayRange } from '../../lib/format'
import { bookingTabs, useMyBookings, type BookingTab } from './api'

/** /bookings?tab=upcoming|stayed|cancelled – tab nằm trên URL. */
export function MyBookingsPage() {
  const [params, setParams] = useSearchParams()
  const tab = bookingTabs.find((t) => t.key === params.get('tab')) ?? bookingTabs[0]
  const bookings = useMyBookings()

  const counts = Object.fromEntries(
    bookingTabs.map((t) => [t.key, bookings.data?.filter((b) => (t.statuses as readonly string[]).includes(b.status)).length ?? 0]),
  ) as Record<BookingTab, number>
  const rows = (bookings.data ?? [])
    .filter((b) => (tab.statuses as readonly string[]).includes(b.status))
    // Sắp tới: gần nhất lên đầu; các tab khác: mới nhất lên đầu
    .sort((a, b) => (tab.key === 'upcoming' ? a.checkIn.localeCompare(b.checkIn) : b.checkIn.localeCompare(a.checkIn)))

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <h1 className="text-2xl font-semibold tracking-tight">Đặt phòng của tôi</h1>

      <div role="tablist" aria-label="Lọc đặt phòng" className="flex gap-1 overflow-x-auto rounded-xl bg-slate-100 p-1 dark:bg-slate-800">
        {bookingTabs.map((t) => (
          <button
            key={t.key}
            role="tab"
            type="button"
            aria-selected={t.key === tab.key}
            onClick={() => setParams(t.key === 'upcoming' ? {} : { tab: t.key }, { replace: true })}
            className={`flex-1 rounded-lg px-3 py-2 text-sm font-medium whitespace-nowrap ${
              t.key === tab.key ? 'bg-white shadow-sm dark:bg-slate-950' : 'text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white'
            }`}
          >
            {t.label}
            {bookings.data && ` (${counts[t.key]})`}
          </button>
        ))}
      </div>

      <div role="tabpanel" aria-label={tab.label}>
        {bookings.isPending ? (
          <LoadingBlock />
        ) : bookings.isError ? (
          <Alert>{errorMessage(bookings.error)}</Alert>
        ) : rows.length === 0 ? (
          <EmptyState icon={CalendarX2} title={tab.key === 'upcoming' ? 'Chưa có chuyến đi sắp tới' : 'Chưa có đặt phòng nào'}>
            <Link to="/stays" className="font-medium text-brand-700 hover:underline dark:text-brand-100">
              Tìm homestay
            </Link>
          </EmptyState>
        ) : (
          <ul className="flex flex-col gap-3">
            {rows.map((b) => (
              <li key={b.id}>
                <Link
                  to={`/bookings/${b.code}`}
                  className="flex flex-col gap-2 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm hover:shadow-md focus-visible:outline-2 focus-visible:outline-brand-600 dark:border-slate-800 dark:bg-slate-900"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold">{b.homestayName}</p>
                      <p className="text-sm text-slate-600 dark:text-slate-300">
                        {b.roomName} · {b.guests} khách
                      </p>
                    </div>
                    <StatusBadge status={b.status} />
                  </div>
                  <p className="text-sm">{stayRange(b.checkIn, b.checkOut)}</p>
                  <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
                    <span className="font-mono text-slate-600 dark:text-slate-300">{b.code}</span>
                    <span>
                      <span className="font-semibold">{money(b.total)}</span>
                      {b.money.amountDue > 0 && <span className="text-slate-600 dark:text-slate-300"> · còn {money(b.money.amountDue)}</span>}
                    </span>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
