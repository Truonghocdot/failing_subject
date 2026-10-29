import dayjs from 'dayjs'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { IconButton } from '../../components/Button'
import { Alert, Spinner } from '../../components/Feedback'
import { errorMessage } from '../../lib/api'
import { addDays, dayLabel, isoDate, shortMoney, today } from '../../lib/format'
import type { CalendarDay } from '../../lib/types'
import { MAX_DAYS_AHEAD, MAX_NIGHTS, useStayCalendar } from './api'

const weekdays = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN']

type Props = {
  slug: string
  roomId: number
  roomName: string
  checkIn: string | null
  checkOut: string | null
  onSelect: (checkIn: string, checkOut: string) => void
}

/**
 * Lịch tháng của 1 phòng: giá từng đêm, đêm đã có người đặt gạch đi.
 * Bấm lần 1 = ngày nhận phòng, lần 2 = ngày trả phòng (ngày trả được trùng đêm đã bán – khách đi buổi sáng).
 */
export function AvailabilityCalendar({ slug, roomId, roomName, checkIn, checkOut, onSelect }: Props) {
  const first = dayjs(today()).startOf('month')
  const last = dayjs(addDays(today(), MAX_DAYS_AHEAD)).startOf('month')
  const [month, setMonth] = useState(() => (checkIn ? dayjs(checkIn).startOf('month') : first))
  const [anchor, setAnchor] = useState<string | null>(null)
  const [hint, setHint] = useState<string | null>(null)

  const from = month.isBefore(dayjs(today())) ? today() : isoDate(month)
  const to = isoDate(month.endOf('month'))
  const calendar = useStayCalendar(slug, from, to)
  const days = new Map<string, CalendarDay>((calendar.data?.rooms.find((r) => r.roomId === roomId)?.days ?? []).map((d) => [d.date, d]))

  const lead = (month.day() + 6) % 7 // thứ Hai đầu tuần
  const cells = Array.from({ length: month.daysInMonth() }, (_, i) => isoDate(month.add(i, 'day')))

  const pick = (date: string) => {
    setHint(null)
    if (anchor && date > anchor) {
      const blocked = Array.from({ length: dayjs(date).diff(anchor, 'day') }, (_, i) => addDays(anchor, i)).find((d) => days.get(d)?.available === false)
      if (blocked) return setHint(`Đêm ${dayLabel(blocked)} đã có người đặt – chọn ngày trả phòng sớm hơn`)
      if (dayjs(date).diff(anchor, 'day') > MAX_NIGHTS) return setHint(`Mỗi lần đặt tối đa ${MAX_NIGHTS} đêm`)
      setAnchor(null)
      return onSelect(anchor, date)
    }
    if (days.get(date)?.available === false) return setHint('Đêm này đã có người đặt')
    setAnchor(date)
    setHint('Chọn tiếp ngày trả phòng')
  }

  const selectedFrom = anchor ?? checkIn
  const selectedTo = anchor ? null : checkOut

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <IconButton label="Tháng trước" disabled={!month.isAfter(first)} onClick={() => setMonth(month.subtract(1, 'month'))} className="disabled:opacity-30">
          <ChevronLeft className="size-5" aria-hidden />
        </IconButton>
        <p className="flex items-center gap-2 font-medium capitalize" aria-live="polite">
          {month.format('MMMM YYYY')}
          {calendar.isFetching && <Spinner className="size-4" />}
        </p>
        <IconButton label="Tháng sau" disabled={!month.isBefore(last)} onClick={() => setMonth(month.add(1, 'month'))} className="disabled:opacity-30">
          <ChevronRight className="size-5" aria-hidden />
        </IconButton>
      </div>

      {calendar.isError && <Alert>{errorMessage(calendar.error)}</Alert>}

      <div role="grid" aria-label={`Lịch trống ${roomName} – ${month.format('MMMM YYYY')}`} className="grid grid-cols-7 gap-1 text-center">
        <div role="row" className="contents">
          {weekdays.map((w) => (
            <div key={w} role="columnheader" className="py-1 text-xs font-medium text-slate-500 dark:text-slate-400">
              {w}
            </div>
          ))}
        </div>
        <div role="row" className="contents">
          {Array.from({ length: lead }, (_, i) => (
            <div key={`lead-${i}`} role="presentation" />
          ))}
          {cells.map((date) => {
            const info = days.get(date)
            const past = date < today()
            const asCheckout = !!anchor && date > anchor
            const taken = info?.available === false
            const disabled = past || !info || (taken && !asCheckout)
            const inRange = !!selectedFrom && !!selectedTo && date > selectedFrom && date < selectedTo
            const isEdge = date === selectedFrom || date === selectedTo
            return (
              <div key={date} role="gridcell" aria-selected={isEdge || inRange}>
                <button
                  type="button"
                  disabled={disabled}
                  onClick={() => pick(date)}
                  aria-label={`${dayLabel(date)}${past ? '' : taken ? ', đã có người đặt' : info ? `, ${shortMoney(info.price)}` : ''}${date === selectedFrom ? ', nhận phòng' : ''}${date === selectedTo ? ', trả phòng' : ''}`}
                  className={`flex h-12 w-full flex-col items-center justify-center rounded-lg text-sm leading-tight transition-colors focus-visible:outline-2 focus-visible:outline-brand-600 disabled:cursor-not-allowed ${
                    isEdge
                      ? 'bg-brand-700 text-white'
                      : inRange
                        ? 'bg-brand-100 text-brand-800 dark:bg-brand-700/30 dark:text-brand-100'
                        : past || !info
                          ? 'text-slate-300 dark:text-slate-600'
                          : taken
                            ? asCheckout
                              ? 'text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
                              : 'text-slate-400 line-through dark:text-slate-500'
                            : 'hover:bg-slate-100 dark:hover:bg-slate-800'
                  }`}
                >
                  <span className="font-medium">{dayjs(date).date()}</span>
                  {info && !past && (
                    <span className={`text-[0.65rem] ${isEdge ? 'text-white' : info.source !== 'base' && !taken ? 'text-amber-700 dark:text-amber-300' : ''}`}>
                      {taken ? 'Hết' : shortMoney(info.price)}
                    </span>
                  )}
                </button>
              </div>
            )
          })}
        </div>
      </div>
      <p className="min-h-5 text-sm text-slate-600 dark:text-slate-300" aria-live="polite">
        {hint ?? 'Giá màu cam: cuối tuần / ngày lễ. Bấm ngày nhận rồi ngày trả phòng.'}
      </p>
    </div>
  )
}
