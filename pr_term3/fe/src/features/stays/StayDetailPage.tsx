import { BedDouble, CalendarCheck, Check, Clock, MapPin, Maximize2, Users } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { SelectField } from '../../components/Field'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { DateRangeFields, GuestStepper } from '../../components/TripFields'
import { ApiError, errorMessage, query } from '../../lib/api'
import { amenityLabel, cancelPolicyLines, money, stayRange } from '../../lib/format'
import type { Photo, StayDetail, StayRoom } from '../../lib/types'
import { NotFoundPage } from '../../app/NotFoundPage'
import { readTrip, tripError, useQuote, useStay, type Trip } from './api'
import { AvailabilityCalendar } from './AvailabilityCalendar'
import { QuoteSummary } from './QuoteSummary'
import { Reviews } from './Reviews'
import { Cover, Rating } from './StayCard'

/** /stays/:slug?room&checkIn&checkOut&guests – phòng, ngày, số khách nằm trên URL (F5 giữ nguyên báo giá). */
export function StayDetailPage() {
  const { slug = '' } = useParams()
  const stay = useStay(slug)

  if (stay.isPending) return <LoadingBlock />
  if (stay.isError)
    return stay.error instanceof ApiError && stay.error.status === 404 ? <NotFoundPage homeHref="/stays" homeLabel="Xem homestay khác" /> : <Alert>{errorMessage(stay.error)}</Alert>
  return <StayView stay={stay.data} />
}

function StayView({ stay }: { stay: StayDetail }) {
  const [params, setParams] = useSearchParams()
  const trip = readTrip(params)
  const roomParam = Number(params.get('room'))
  const room = stay.rooms.find((r) => r.id === roomParam) ?? null

  const setTrip = (patch: Partial<Trip> & { room?: number }) => {
    const next = { room: room?.id ?? null, ...trip, ...patch }
    setParams(new URLSearchParams(query({ room: next.room, checkIn: next.checkIn, checkOut: next.checkOut, guests: next.guests })), { replace: true, preventScrollReset: true })
  }

  const chooseRoom = (r: StayRoom) => {
    setTrip({ room: r.id, guests: Math.min(trip.guests, r.maxGuests) })
    document.getElementById('booking-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  return (
    <div className="flex flex-col gap-6 pb-24 lg:pb-0">
      <div>
        <Link to={`/stays${query({ province: stay.province, checkIn: trip.checkIn, checkOut: trip.checkOut, guests: trip.guests })}`} className="text-sm text-brand-700 hover:underline dark:text-brand-100">
          ← Homestay ở {stay.province}
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight md:text-3xl">{stay.name}</h1>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-600 dark:text-slate-300">
          <span className="flex items-center gap-1">
            <MapPin className="size-4 shrink-0" aria-hidden />
            {stay.address}, {stay.province}
          </span>
          <Rating rating={stay.rating} count={stay.reviewCount} />
        </div>
      </div>

      <Gallery photos={stay.photos} name={stay.name} />

      <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
        <div className="flex min-w-0 flex-col gap-6">
          {stay.description && <p className="whitespace-pre-line text-slate-700 dark:text-slate-200">{stay.description}</p>}

          {stay.amenities.length > 0 && (
            <section aria-labelledby="amenities-title">
              <h2 id="amenities-title" className="mb-3 text-lg font-semibold">
                Tiện nghi
              </h2>
              <ul className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                {stay.amenities.map((a) => (
                  <li key={a} className="flex items-center gap-2">
                    <Check className="size-4 text-brand-700 dark:text-brand-100" aria-hidden />
                    {amenityLabel(a)}
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section aria-labelledby="rooms-title" className="flex flex-col gap-3">
            <h2 id="rooms-title" className="text-lg font-semibold">
              Phòng ({stay.rooms.length})
            </h2>
            {stay.rooms.length === 0 && <p className="text-sm text-slate-600 dark:text-slate-300">Homestay chưa mở bán phòng nào.</p>}
            <ul className="flex flex-col gap-3">
              {stay.rooms.map((r) => (
                <RoomItem key={r.id} room={r} selected={r.id === room?.id} tooSmall={r.maxGuests < trip.guests} onChoose={() => chooseRoom(r)} />
              ))}
            </ul>
          </section>

          {room && (
            <Card>
              <h2 className="mb-3 text-lg font-semibold">Lịch trống · {room.name}</h2>
              <AvailabilityCalendar
                key={room.id}
                slug={stay.slug}
                roomId={room.id}
                roomName={room.name}
                checkIn={trip.checkIn}
                checkOut={trip.checkOut}
                onSelect={(checkIn, checkOut) => setTrip({ checkIn, checkOut })}
              />
            </Card>
          )}

          <Policy stay={stay} />
          <Reviews stay={stay} />
        </div>

        <aside id="booking-panel" aria-label="Đặt phòng" className="scroll-mt-20 lg:sticky lg:top-20 lg:self-start">
          <BookingPanel stay={stay} room={room} trip={trip} onTrip={setTrip} />
        </aside>
      </div>
    </div>
  )
}

function Gallery({ photos, name }: { photos: Photo[]; name: string }) {
  const [index, setIndex] = useState(0)
  if (photos.length === 0) return <Cover url={null} alt={name} className="aspect-[16/9] w-full rounded-2xl md:aspect-[21/9]" />
  const current = photos[Math.min(index, photos.length - 1)]
  return (
    <div className="flex flex-col gap-2">
      <img
        src={current.url}
        width={current.width}
        height={current.height}
        alt={`${name} – ảnh ${index + 1}/${photos.length}`}
        className="aspect-[16/9] w-full rounded-2xl bg-slate-100 object-cover md:aspect-[21/9] dark:bg-slate-800"
      />
      {photos.length > 1 && (
        <ul className="flex gap-2 overflow-x-auto pb-1" aria-label="Chọn ảnh">
          {photos.map((p, i) => (
            <li key={p.id} className="shrink-0">
              <button
                type="button"
                onClick={() => setIndex(i)}
                aria-label={`Xem ảnh ${i + 1}`}
                aria-current={i === index || undefined}
                className={`block overflow-hidden rounded-lg border-2 focus-visible:outline-2 focus-visible:outline-brand-600 ${i === index ? 'border-brand-700' : 'border-transparent opacity-80 hover:opacity-100'}`}
              >
                <img src={p.url} alt="" loading="lazy" className="h-14 w-20 object-cover" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function RoomItem({ room, selected, tooSmall, onChoose }: { room: StayRoom; selected: boolean; tooSmall: boolean; onChoose: () => void }) {
  return (
    <li className={`rounded-2xl border bg-white p-4 dark:bg-slate-900 ${selected ? 'border-brand-700 ring-1 ring-brand-700' : 'border-slate-200 dark:border-slate-800'}`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-semibold">{room.name}</h3>
          <p className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-sm text-slate-600 dark:text-slate-300">
            <span className="flex items-center gap-1">
              <Users className="size-4" aria-hidden />
              {room.baseGuests === room.maxGuests ? `${room.maxGuests} khách` : `${room.baseGuests}–${room.maxGuests} khách`}
            </span>
            {room.bedInfo && (
              <span className="flex items-center gap-1">
                <BedDouble className="size-4" aria-hidden />
                {room.bedInfo}
              </span>
            )}
            {room.areaM2 && (
              <span className="flex items-center gap-1">
                <Maximize2 className="size-4" aria-hidden />
                {room.areaM2} m²
              </span>
            )}
            {room.minNights > 1 && <span>Tối thiểu {room.minNights} đêm</span>}
          </p>
        </div>
        <p className="text-right">
          <span className="font-semibold">{money(room.basePrice)}</span>
          <span className="text-xs text-slate-500 dark:text-slate-400"> / đêm</span>
          {room.weekendPrice !== null && room.weekendPrice !== room.basePrice && (
            <span className="block text-xs text-slate-500 dark:text-slate-400">T6, T7: {money(room.weekendPrice)}</span>
          )}
        </p>
      </div>
      {room.description && <p className="mt-2 text-sm whitespace-pre-line text-slate-700 dark:text-slate-200">{room.description}</p>}
      <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
        {room.extraGuestFee > 0 && room.maxGuests > room.baseGuests && `Phụ thu ${money(room.extraGuestFee)} / khách / đêm từ khách thứ ${room.baseGuests + 1}. `}
        {room.cleaningFee > 0 && `Phí dọn ${money(room.cleaningFee)} / lần.`}
      </p>
      <div className="mt-3 flex items-center gap-3">
        <Button variant={selected ? 'secondary' : 'primary'} size="sm" onClick={onChoose} aria-pressed={selected}>
          {selected ? 'Đang chọn' : 'Chọn phòng này'}
        </Button>
        {tooSmall && <span className="text-xs text-amber-700 dark:text-amber-300">Tối đa {room.maxGuests} khách</span>}
      </div>
    </li>
  )
}

function Policy({ stay }: { stay: StayDetail }) {
  const p = stay.policy
  return (
    <section aria-labelledby="policy-title" className="flex flex-col gap-3">
      <h2 id="policy-title" className="text-lg font-semibold">
        Chính sách
      </h2>
      <ul className="flex flex-col gap-2 text-sm text-slate-700 dark:text-slate-200">
        <li className="flex gap-2">
          <Clock className="mt-0.5 size-4 shrink-0" aria-hidden />
          Nhận phòng từ {p.checkInTime} · trả phòng trước {p.checkOutTime}
        </li>
        <li className="flex gap-2">
          <CalendarCheck className="mt-0.5 size-4 shrink-0" aria-hidden />
          Đặt online được giữ chỗ {p.holdHours} giờ – chuyển khoản cọc {p.depositPercent}% trong thời gian này để xác nhận
        </li>
        {cancelPolicyLines(p).map((line) => (
          <li key={line} className="flex gap-2">
            <Check className="mt-0.5 size-4 shrink-0" aria-hidden />
            {line}
          </li>
        ))}
      </ul>
    </section>
  )
}

type PanelProps = { stay: StayDetail; room: StayRoom | null; trip: Trip; onTrip: (patch: Partial<Trip> & { room?: number }) => void }

/** Khung đặt phòng dính bên phải (máy tính); điện thoại: nằm dưới danh sách phòng + thanh tổng tiền dính đáy. */
function BookingPanel({ stay, room, trip, onTrip }: PanelProps) {
  const [checkIn, setCheckIn] = useState(trip.checkIn ?? '')
  const [checkOut, setCheckOut] = useState(trip.checkOut ?? '')
  const [dateError, setDateError] = useState<string | null>(null)
  // Ngày trên URL đổi (bấm lịch) → đồng bộ lại 2 ô
  const [synced, setSynced] = useState(`${trip.checkIn}|${trip.checkOut}`)
  if (synced !== `${trip.checkIn}|${trip.checkOut}`) {
    setSynced(`${trip.checkIn}|${trip.checkOut}`)
    setCheckIn(trip.checkIn ?? '')
    setCheckOut(trip.checkOut ?? '')
  }

  const fits = room !== null && trip.guests <= room.maxGuests
  const quote = useQuote(fits && trip.checkIn && trip.checkOut ? { roomId: room.id, checkIn: trip.checkIn, checkOut: trip.checkOut, guests: trip.guests } : null)
  const canBook = quote.data?.available === true && !quote.isFetching

  const bookHref =
    room && trip.checkIn && trip.checkOut ? `/book/${room.id}${query({ stay: stay.slug, checkIn: trip.checkIn, checkOut: trip.checkOut, guests: trip.guests })}` : ''

  return (
    <>
      <Card className="flex flex-col gap-4 p-5">
        <h2 className="text-lg font-semibold">Đặt phòng</h2>
        <SelectField label="Phòng" value={room?.id ?? ''} onChange={(e) => e.target.value && onTrip({ room: Number(e.target.value) })}>
          <option value="">Chọn phòng…</option>
          {stay.rooms.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name} · tối đa {r.maxGuests} khách
            </option>
          ))}
        </SelectField>
        <DateRangeFields
          checkIn={checkIn}
          checkOut={checkOut}
          error={dateError}
          onChange={(ci, co) => {
            setCheckIn(ci)
            setCheckOut(co)
            const problem = ci && co ? tripError(ci, co) : null
            setDateError(problem)
            if (ci && co && !problem) onTrip({ checkIn: ci, checkOut: co })
          }}
        />
        <GuestStepper value={trip.guests} max={room?.maxGuests ?? 20} onChange={(guests) => onTrip({ guests })} />

        {!room ? (
          <p className="text-sm text-slate-600 dark:text-slate-300">Chọn phòng để xem giá.</p>
        ) : !fits ? (
          <Alert>
            {room.name} tối đa {room.maxGuests} khách
          </Alert>
        ) : !trip.checkIn || !trip.checkOut ? (
          <p className="text-sm text-slate-600 dark:text-slate-300">Chọn ngày nhận – trả phòng để xem giá từng đêm.</p>
        ) : quote.isPending ? (
          <LoadingBlock label="Đang báo giá…" />
        ) : quote.isError ? (
          <Alert>{errorMessage(quote.error)}</Alert>
        ) : (
          <div className="flex flex-col gap-3" aria-busy={quote.isFetching}>
            <p className="text-sm font-medium">{stayRange(quote.data.checkIn, quote.data.checkOut)}</p>
            <QuoteSummary quote={quote.data} />
            {!quote.data.available && <Alert>Phòng đã có người đặt trong khoảng ngày này – chọn ngày khác trên lịch.</Alert>}
          </div>
        )}

        {canBook ? (
          <Link to={bookHref} className="inline-flex h-11 items-center justify-center rounded-lg bg-brand-700 px-4 font-medium text-white hover:bg-brand-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600">
            Đặt phòng
          </Link>
        ) : (
          <Button disabled className="h-11">
            Đặt phòng
          </Button>
        )}
        <p className="text-xs text-slate-500 dark:text-slate-400">Chưa trừ tiền – bạn chuyển khoản cọc sau khi nhận mã đặt phòng.</p>
      </Card>

      {canBook && quote.data && (
        <div className="fixed inset-x-0 bottom-0 z-10 border-t border-slate-200 bg-white px-4 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] lg:hidden dark:border-slate-800 dark:bg-slate-900">
          <div className="mx-auto flex max-w-6xl items-center justify-between gap-3">
            <p className="min-w-0 text-sm">
              <span className="block font-semibold">{money(quote.data.total)}</span>
              <span className="text-slate-600 dark:text-slate-300">
                {quote.data.nights} đêm · cọc {money(quote.data.depositAmount)}
              </span>
            </p>
            <Link to={bookHref} className="inline-flex h-11 shrink-0 items-center rounded-lg bg-brand-700 px-5 font-medium text-white hover:bg-brand-800">
              Đặt ngay
            </Link>
          </div>
        </div>
      )}
    </>
  )
}
