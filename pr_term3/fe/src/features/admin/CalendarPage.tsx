import dayjs from 'dayjs'
import { CalendarPlus, ChevronLeft, ChevronRight, Lock } from 'lucide-react'
import { Fragment, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { inputClass, TextField } from '../../components/Field'
import { Alert, LoadingBlock } from '../../components/Feedback'
import { ApiError, errorMessage, query } from '../../lib/api'
import { addDays, dayLabel, isIsoDate, nightsBetween, shortMoney, statusLabels, today } from '../../lib/format'
import type { AdminCalendarRoom, BookingStatus, CalendarBlock, CalendarBooking } from '../../lib/types'
import { useAdminCalendar, useCreateBlock, useDeleteBlock } from './api'
import { HomestaySelect, PageHeader } from './shared'

const RANGES = [14, 30] as const

// Màu theo docs/04-frontend-react.md §5 – thanh luôn có chữ (tên khách) + nhãn trạng thái cho trình đọc màn hình
const barTones: Record<BookingStatus, string> = {
  pending: 'border-amber-400 bg-amber-100 text-amber-950 dark:border-amber-600 dark:bg-amber-900/70 dark:text-amber-50',
  confirmed: 'border-sky-400 bg-sky-100 text-sky-950 dark:border-sky-600 dark:bg-sky-900/70 dark:text-sky-50',
  checked_in: 'border-emerald-500 bg-emerald-100 text-emerald-950 dark:border-emerald-600 dark:bg-emerald-900/70 dark:text-emerald-50',
  checked_out: 'border-slate-300 bg-slate-200 text-slate-800 dark:border-slate-600 dark:bg-slate-700 dark:text-slate-100',
  cancelled: 'border-red-200 bg-red-50 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100',
  expired: 'border-red-200 bg-red-50 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100',
  no_show: 'border-red-200 bg-red-50 text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100',
}

const legend: BookingStatus[] = ['pending', 'confirmed', 'checked_in', 'checked_out', 'no_show']

type Selected = { room: AdminCalendarRoom; date: string }

/** /admin/calendar?homestayId&from&days=14|30 – lưới phòng × ngày; khoảng ngày + homestay nằm trên URL. */
export function CalendarPage() {
  const [params, setParams] = useSearchParams()
  const days = RANGES.find((r) => String(r) === params.get('days')) ?? 14
  const from = isIsoDate(params.get('from')) ? params.get('from')! : today()
  const homestayId = Number(params.get('homestayId')) || null
  const to = addDays(from, days - 1)
  const calendar = useAdminCalendar(homestayId, from, to)
  const [selected, setSelected] = useState<Selected | null>(null)
  const [block, setBlock] = useState<{ room: AdminCalendarRoom; block: CalendarBlock } | null>(null)

  const update = (patch: { from?: string; days?: number; homestayId?: number | null }) => {
    const next = { from, days, homestayId, ...patch }
    setParams(new URLSearchParams(query({ homestayId: next.homestayId, from: next.from === today() ? null : next.from, days: next.days === 14 ? null : next.days })), { replace: true })
  }

  const dates = Array.from({ length: days }, (_, i) => addDays(from, i))
  const rooms = calendar.data?.rooms ?? []
  const groups = [...new Map(rooms.map((r) => [r.homestayId, r.homestayName])).entries()]

  return (
    <div>
      <PageHeader
        title="Lịch phòng"
        description={`${dayLabel(from)} → ${dayLabel(to)} · bấm ô trống để tạo đặt phòng / khoá phòng, bấm thanh để xem chi tiết`}
        actions={
          <Link to="/admin/bookings/new" className="inline-flex h-10 items-center gap-2 rounded-lg bg-brand-700 px-4 text-sm font-medium text-white hover:bg-brand-800">
            <CalendarPlus className="size-4" aria-hidden />
            Tạo đặt phòng
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="w-full sm:w-64">
          <HomestaySelect value={homestayId} onChange={(id) => update({ homestayId: id })} />
        </div>
        <div className="flex items-center gap-1">
          <Button variant="secondary" size="sm" onClick={() => update({ from: addDays(from, -7) })} aria-label="Lùi 7 ngày">
            <ChevronLeft className="size-4" aria-hidden />7 ngày
          </Button>
          <Button variant="secondary" size="sm" onClick={() => update({ from: today() })} disabled={from === today()}>
            Hôm nay
          </Button>
          <Button variant="secondary" size="sm" onClick={() => update({ from: addDays(from, 7) })} aria-label="Tới 7 ngày">
            7 ngày
            <ChevronRight className="size-4" aria-hidden />
          </Button>
        </div>
        <label className="flex flex-col gap-1.5 text-sm font-medium">
          Từ ngày
          <input type="date" className={`${inputClass} h-8 w-auto`} value={from} onChange={(e) => isIsoDate(e.target.value) && update({ from: e.target.value })} />
        </label>
        <div role="group" aria-label="Số ngày hiển thị" className="flex rounded-lg border border-slate-300 p-0.5 dark:border-slate-700">
          {RANGES.map((r) => (
            <button
              key={r}
              type="button"
              aria-pressed={days === r}
              onClick={() => update({ days: r })}
              className={`rounded-md px-3 py-1 text-sm font-medium ${days === r ? 'bg-brand-700 text-white' : 'text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800'}`}
            >
              {r === 14 ? '2 tuần' : '30 ngày'}
            </button>
          ))}
        </div>
      </div>

      {calendar.isPending ? (
        <LoadingBlock />
      ) : calendar.isError ? (
        <Alert>{errorMessage(calendar.error)}</Alert>
      ) : rooms.length === 0 ? (
        <Alert tone="info">Chưa có phòng nào – thêm phòng ở mục Homestay & phòng.</Alert>
      ) : (
        <>
          {/* Cuộn ngang trong khung (không cuộn cả trang), cột tên phòng dính trái */}
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900" aria-busy={calendar.isFetching}>
            {/* Bề rộng tối thiểu cố định (không theo chữ dài trong thanh) → mọi hàng chia cột giống nhau, 14 ngày vừa màn hình máy tính */}
            <div role="grid" aria-label={`Lịch phòng ${dayLabel(from)} – ${dayLabel(to)}`} style={{ minWidth: `calc(8.5rem + ${days} * 3.25rem)` }}>
              <div role="row" className="grid border-b border-slate-200 dark:border-slate-800" style={{ gridTemplateColumns: columns(days) }}>
                <div role="columnheader" className="sticky left-0 z-20 bg-white px-3 py-2 text-xs font-medium text-slate-500 dark:bg-slate-900 dark:text-slate-400">
                  Phòng
                </div>
                {dates.map((d) => {
                  const dow = dayjs(d).day()
                  return (
                    <div
                      key={d}
                      role="columnheader"
                      className={`border-l border-slate-100 py-1.5 text-center text-xs leading-tight dark:border-slate-800 ${d === today() ? 'bg-brand-50 font-semibold text-brand-800 dark:bg-brand-700/20 dark:text-brand-100' : dow === 5 || dow === 6 ? 'bg-amber-50/60 dark:bg-amber-950/20' : ''}`}
                    >
                      <span className="block text-slate-500 dark:text-slate-400">{dayjs(d).format('dd')}</span>
                      {dayjs(d).format('DD/MM')}
                    </div>
                  )
                })}
              </div>

              {groups.map(([id, name]) => (
                <Fragment key={id}>
                  {groups.length > 1 && (
                    <div role="row" className="border-b border-slate-200 bg-slate-50 dark:border-slate-800 dark:bg-slate-800/50">
                      <div role="rowheader" className="sticky left-0 inline-block px-3 py-1.5 text-sm font-semibold">
                        {name}
                      </div>
                    </div>
                  )}
                  {rooms
                    .filter((r) => r.homestayId === id)
                    .map((room) => (
                      <RoomRow key={room.roomId} room={room} dates={dates} onEmpty={(date) => setSelected({ room, date })} onBlock={(b) => setBlock({ room, block: b })} />
                    ))}
                </Fragment>
              ))}
            </div>
          </div>
          <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-2 text-xs" aria-label="Chú thích màu">
            {legend.map((s) => (
              <li key={s} className="flex items-center gap-1.5">
                <span className={`inline-block h-3 w-5 rounded border ${barTones[s]}`} aria-hidden />
                {statusLabels[s]}
              </li>
            ))}
            <li className="flex items-center gap-1.5">
              <span className="inline-block h-3 w-5 rounded border border-slate-400 bg-stripes" aria-hidden />
              Khoá phòng
            </li>
          </ul>
        </>
      )}

      {selected && <EmptyCellDialog selected={selected} onClose={() => setSelected(null)} />}
      {block && <BlockDialog room={block.room} block={block.block} onClose={() => setBlock(null)} />}
    </div>
  )
}

const columns = (days: number) => `minmax(8.5rem, 11rem) repeat(${days}, minmax(3.25rem, 1fr))`

type Bar = { key: string; start: number; span: number; lane: number; cutLeft: boolean; cutRight: boolean } & (
  | { kind: 'booking'; booking: CalendarBooking }
  | { kind: 'block'; block: CalendarBlock }
)

/** Xếp thanh vào các làn: đặt phòng mới có thể nằm trên đêm của đặt phòng "không đến" / trả sớm đã nhả. */
function layout(room: AdminCalendarRoom, dates: string[]): { bars: Bar[]; lanes: number } {
  const first = dates[0]
  const end = addDays(dates[dates.length - 1], 1)
  const items = [
    ...room.bookings.map((b) => ({ kind: 'booking' as const, booking: b, from: b.checkIn, until: b.checkOut, key: `b${b.id}` })),
    ...room.blocks.map((b) => ({ kind: 'block' as const, block: b, from: b.from, until: addDays(b.to, 1), key: `k${b.id}` })),
  ].sort((a, b) => a.from.localeCompare(b.from))

  const laneEnds: string[] = []
  const bars = items.map((item) => {
    const from = item.from < first ? first : item.from
    const until = item.until > end ? end : item.until
    let lane = laneEnds.findIndex((e) => e <= from)
    if (lane === -1) lane = laneEnds.push(until) - 1
    else laneEnds[lane] = until
    const base = { key: item.key, start: nightsBetween(first, from), span: Math.max(1, nightsBetween(from, until)), lane, cutLeft: item.from < first, cutRight: item.until > end }
    return item.kind === 'booking' ? { ...base, kind: item.kind, booking: item.booking } : { ...base, kind: item.kind, block: item.block }
  })
  return { bars, lanes: Math.max(1, laneEnds.length) }
}

function RoomRow({ room, dates, onEmpty, onBlock }: { room: AdminCalendarRoom; dates: string[]; onEmpty: (date: string) => void; onBlock: (b: CalendarBlock) => void }) {
  const { bars, lanes } = layout(room, dates)
  // Đêm đang bị chiếm (đặt phòng còn hiệu lực / khoá) – ô không bấm được
  const busy = new Set(
    bars
      .filter((b) => b.kind === 'block' || ['pending', 'confirmed', 'checked_in'].includes(b.booking.status))
      .flatMap((b) => Array.from({ length: b.span }, (_, i) => dates[b.start + i])),
  )
  const inactive = room.status !== 'active'

  return (
    <div role="row" className="grid border-b border-slate-100 dark:border-slate-800" style={{ gridTemplateColumns: columns(dates.length), gridTemplateRows: `repeat(${lanes}, 2.75rem)` }}>
      <div role="rowheader" className="sticky left-0 z-20 flex flex-col justify-center bg-white px-3 text-sm dark:bg-slate-900" style={{ gridRow: `1 / span ${lanes}` }}>
        <span className="truncate font-medium">{room.name}</span>
        {inactive && <span className="text-xs text-slate-500 dark:text-slate-400">Ngưng nhận đặt</span>}
      </div>
      {dates.map((d, i) => {
        const disabled = inactive || d < today() || busy.has(d)
        return (
          <div key={d} role="gridcell" className="border-l border-slate-100 dark:border-slate-800" style={{ gridColumn: i + 2, gridRow: `1 / span ${lanes}` }}>
            <button
              type="button"
              disabled={disabled}
              onClick={() => onEmpty(d)}
              aria-label={`${room.name}, ${dayLabel(d)}, ${shortMoney(room.prices[i])} – tạo đặt phòng / khoá phòng`}
              className="flex h-full w-full items-end justify-center pb-1 text-[0.65rem] text-slate-500 hover:bg-brand-50 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-brand-600 disabled:cursor-default disabled:hover:bg-transparent dark:text-slate-400 dark:hover:bg-brand-700/20"
            >
              {!inactive && d >= today() && shortMoney(room.prices[i])}
            </button>
          </div>
        )
      })}
      {bars.map((bar) => {
        const b = bar.kind === 'booking' ? bar.booking : null
        const block = bar.kind === 'block' ? bar.block : null
        // Đã trả phòng / không đến: đêm đã nhả cho người khác đặt → thanh mỏng phía trên, ô bên dưới vẫn bấm được
        const released = b !== null && !['pending', 'confirmed', 'checked_in'].includes(b.status)
        const shape = `flex h-full w-full min-w-0 items-center overflow-hidden border px-2 font-medium whitespace-nowrap ${released ? 'text-[0.625rem] leading-none' : 'text-xs'} ${
          bar.cutLeft ? 'rounded-l-none border-l-0' : 'rounded-l-md'
        } ${bar.cutRight ? 'rounded-r-none border-r-0' : 'rounded-r-md'}`
        // Ô (gridcell) giữ vị trí trên lưới; nút / link bên trong – hàng của role="grid" chỉ được chứa ô
        return (
          <div
            key={bar.key}
            role="gridcell"
            style={{ gridColumn: `${bar.start + 2} / span ${bar.span}`, gridRow: bar.lane + 1 }}
            className={`z-10 mx-0.5 flex min-w-0 ${released ? 'mt-0.5 h-4 self-start' : 'my-1'}`}
          >
            {block ? (
              <button type="button" onClick={() => onBlock(block)} className={`${shape} bg-stripes border-slate-400 text-slate-800 dark:text-slate-100`} title={`Khoá: ${block.reason}`}>
                <Lock className="mr-1 size-3 shrink-0" aria-hidden />
                <span className="truncate">{block.reason}</span>
              </button>
            ) : b && (
              <Link
                to={`/admin/bookings/${b.id}`}
                className={`${shape} ${barTones[b.status]} hover:brightness-95 focus-visible:outline-2 focus-visible:outline-brand-600`}
                title={`${b.code} · ${b.guestName} · ${b.guests} khách · ${statusLabels[b.status]}`}
                aria-label={`${b.code} – ${b.guestName}, ${b.guests} khách, ${statusLabels[b.status]}, ${dayLabel(b.checkIn)} → ${dayLabel(b.checkOut)}`}
              >
                <span className="truncate">
                  {b.guestName}
                  {bar.span > 1 && <span className="font-normal opacity-80"> · {b.guests} khách</span>}
                </span>
              </Link>
            )}
          </div>
        )
      })}
    </div>
  )
}

/** Bấm ô trống: tạo đặt phòng (sang trang tạo, điền sẵn phòng + ngày) hoặc khoá phòng ngay trong hộp thoại. */
function EmptyCellDialog({ selected, onClose }: { selected: Selected; onClose: () => void }) {
  const { room, date } = selected
  const [mode, setMode] = useState<'menu' | 'block'>('menu')
  const [to, setTo] = useState(date)
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const create = useCreateBlock()

  const submit = async () => {
    setError(null)
    if (!reason.trim()) return setError('Vui lòng nhập lý do khoá')
    try {
      await create.mutateAsync({ roomId: room.roomId, from: date, to, reason: reason.trim() })
      onClose()
    } catch (e) {
      setError(e instanceof ApiError && e.extensions.bookings ? `${e.message}` : errorMessage(e))
    }
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      title={`${room.name} · ${dayLabel(date)}`}
      footer={
        mode === 'block' && (
          <>
            <Button variant="secondary" onClick={() => setMode('menu')}>
              Quay lại
            </Button>
            <Button loading={create.isPending} onClick={submit}>
              Khoá phòng
            </Button>
          </>
        )
      }
    >
      {mode === 'menu' ? (
        <div className="flex flex-col gap-2">
          <Link
            to={`/admin/bookings/new${query({ homestayId: room.homestayId, roomId: room.roomId, checkIn: date, checkOut: addDays(date, 1) })}`}
            className="flex items-center gap-3 rounded-xl border border-slate-200 p-3 hover:border-brand-600 dark:border-slate-700"
          >
            <CalendarPlus className="size-5 text-brand-700 dark:text-brand-100" aria-hidden />
            <span>
              <span className="block font-medium">Tạo đặt phòng</span>
              <span className="text-sm text-slate-600 dark:text-slate-300">Khách gọi điện / vãng lai – nhận phòng {dayLabel(date)}</span>
            </span>
          </Link>
          <button type="button" onClick={() => setMode('block')} className="flex items-center gap-3 rounded-xl border border-slate-200 p-3 text-left hover:border-brand-600 dark:border-slate-700">
            <Lock className="size-5 text-slate-600 dark:text-slate-300" aria-hidden />
            <span>
              <span className="block font-medium">Khoá phòng</span>
              <span className="text-sm text-slate-600 dark:text-slate-300">Bảo trì, chủ nhà dùng – không nhận đặt</span>
            </span>
          </button>
        </div>
      ) : (
        <div className="flex flex-col gap-4">
          {error && <Alert>{error}</Alert>}
          <p className="text-sm">Khoá từ đêm {dayLabel(date)}</p>
          <TextField label="Đến hết đêm" type="date" min={date} value={to} onChange={(e) => setTo(e.target.value || date)} />
          <TextField label="Lý do" maxLength={200} placeholder="VD: Sửa điều hoà" value={reason} onChange={(e) => setReason(e.target.value)} autoFocus />
        </div>
      )}
    </Dialog>
  )
}

function BlockDialog({ room, block, onClose }: { room: AdminCalendarRoom; block: CalendarBlock; onClose: () => void }) {
  const remove = useDeleteBlock()
  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      title={`Khoá phòng · ${room.name}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Quay lại
          </Button>
          <Button variant="danger" loading={remove.isPending} onClick={() => remove.mutate(block.id, { onSuccess: onClose })}>
            Bỏ khoá
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2 text-sm">
        <p>
          {dayLabel(block.from)} → hết đêm {dayLabel(block.to)}
        </p>
        <p className="text-slate-600 dark:text-slate-300">Lý do: {block.reason}</p>
        {remove.isError && <Alert>{errorMessage(remove.error)}</Alert>}
      </div>
    </Dialog>
  )
}
