import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { SelectField, TextAreaField, TextField } from '../../components/Field'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { DateRangeFields, GuestStepper } from '../../components/TripFields'
import { ApiError, errorMessage } from '../../lib/api'
import { isIsoDate, paymentKindLabels, paymentMethodLabels, sourceLabels, stayRange } from '../../lib/format'
import { applyServerErrors, emailOptional, fullName, normalizePhone, phoneRequired } from '../../lib/forms'
import type { BookingSource, PaymentKind, PaymentMethod } from '../../lib/types'
import { tripError, useQuote } from '../stays/api'
import { QuoteSummary } from '../stays/QuoteSummary'
import { useAdminCreateBooking, useAdminHomestay } from './api'
import { HomestaySelect, PageHeader } from './shared'

const schema = z.object({
  guestName: fullName,
  guestPhone: phoneRequired,
  guestEmail: emailOptional,
  note: z.string().trim().max(500, 'Ghi chú tối đa 500 ký tự'),
  internalNote: z.string().trim().max(500, 'Ghi chú nội bộ tối đa 500 ký tự'),
})
type Values = z.infer<typeof schema>

const SOURCES: BookingSource[] = ['phone', 'walk_in', 'ota', 'web']
const METHODS = Object.keys(paymentMethodLabels) as PaymentMethod[]

/** /admin/bookings/new?homestayId&roomId&checkIn&checkOut – lễ tân tạo hộ khách (từ lịch phòng: điền sẵn phòng + ngày). */
export function NewBookingPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const [homestayId, setHomestayId] = useState<number | null>(Number(params.get('homestayId')) || null)
  const [roomId, setRoomId] = useState<number | null>(Number(params.get('roomId')) || null)
  const [checkIn, setCheckIn] = useState(isIsoDate(params.get('checkIn')) ? params.get('checkIn')! : '')
  const [checkOut, setCheckOut] = useState(isIsoDate(params.get('checkOut')) ? params.get('checkOut')! : '')
  const [guests, setGuests] = useState(2)
  const [source, setSource] = useState<BookingSource>('phone')
  const [collect, setCollect] = useState(false)
  const [kind, setKind] = useState<PaymentKind>('deposit')
  const [method, setMethod] = useState<PaymentMethod>('cash')
  const [amount, setAmount] = useState<number | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [stayError, setStayError] = useState<string | null>(null)

  const homestay = useAdminHomestay(homestayId)
  const rooms = homestay.data?.rooms ?? []
  const room = rooms.find((r) => r.id === roomId) ?? null
  const datesError = checkIn && checkOut ? tripError(checkIn, checkOut) : null
  const ready = room !== null && checkIn && checkOut && !datesError && guests <= room.maxGuests
  const quote = useQuote(ready ? { roomId: room.id, checkIn, checkOut, guests } : null)
  const create = useAdminCreateBooking()

  const { register, handleSubmit, setError, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { guestName: '', guestPhone: '', guestEmail: '', note: '', internalNote: '' },
  })

  const onSubmit = handleSubmit(async (v) => {
    setNotice(null)
    setStayError(null)
    if (!room) return setStayError('Vui lòng chọn homestay và phòng')
    if (!checkIn || !checkOut) return setStayError('Vui lòng chọn ngày nhận – trả phòng')
    if (datesError) return setStayError(datesError)
    const payAmount = amount ?? (kind === 'deposit' ? quote.data?.depositAmount : quote.data?.total) ?? null
    if (collect && !payAmount) return setNotice('Vui lòng nhập số tiền thu')
    try {
      const booking = await create.mutateAsync({
        roomId: room.id,
        checkIn,
        checkOut,
        guests,
        guestName: v.guestName,
        guestPhone: normalizePhone(v.guestPhone),
        guestEmail: v.guestEmail || null,
        note: v.note || null,
        internalNote: v.internalNote || null,
        source,
        payment: collect && payAmount ? { kind, method, amount: payAmount } : null,
      })
      navigate(`/admin/bookings/${booking.id}`, { replace: true })
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) void qc.invalidateQueries({ queryKey: ['quote'] })
      const text = applyServerErrors(e, setError, ['guestName', 'guestPhone', 'guestEmail', 'note', 'internalNote'])
      if (text) setNotice(text)
    }
  })

  return (
    <div className="mx-auto max-w-5xl">
      <PageHeader title="Tạo đặt phòng" description="Khách gọi điện / đến trực tiếp / từ OTA – thu cọc ngay thì đặt phòng được xác nhận luôn." />
      <form onSubmit={onSubmit} noValidate className="grid gap-4 lg:grid-cols-[1fr_22rem]">
        <div className="flex flex-col gap-4">
          <Card className="flex flex-col gap-4 p-5">
            <h2 className="font-semibold">Phòng & ngày</h2>
            {stayError && <Alert>{stayError}</Alert>}
            <div className="grid gap-4 sm:grid-cols-2">
              <HomestaySelect
                value={homestayId}
                allLabel={null}
                onChange={(id) => {
                  setHomestayId(id)
                  setRoomId(null)
                }}
              />
              <SelectField label="Phòng" value={roomId ?? ''} disabled={!homestay.data} onChange={(e) => setRoomId(e.target.value ? Number(e.target.value) : null)}>
                <option value="">{homestay.isFetching ? 'Đang tải…' : 'Chọn phòng…'}</option>
                {rooms.map((r) => (
                  <option key={r.id} value={r.id} disabled={r.status !== 'active'}>
                    {r.name} · tối đa {r.maxGuests} khách{r.status !== 'active' ? ' (ngưng)' : ''}
                  </option>
                ))}
              </SelectField>
            </div>
            <DateRangeFields
              checkIn={checkIn}
              checkOut={checkOut}
              error={datesError}
              onChange={(ci, co) => {
                setCheckIn(ci)
                setCheckOut(co)
              }}
            />
            <GuestStepper value={guests} onChange={setGuests} max={room?.maxGuests ?? 20} />
            {room && guests > room.maxGuests && <Alert>{room.name} tối đa {room.maxGuests} khách</Alert>}
          </Card>

          <Card className="flex flex-col gap-4 p-5">
            <h2 className="font-semibold">Khách</h2>
            {notice && <Alert>{notice}</Alert>}
            <div className="grid gap-4 sm:grid-cols-2">
              <TextField label="Họ tên khách" autoComplete="off" error={formState.errors.guestName?.message} {...register('guestName')} />
              <TextField label="Số điện thoại" type="tel" inputMode="tel" autoComplete="off" error={formState.errors.guestPhone?.message} {...register('guestPhone')} />
              <TextField label="Email (không bắt buộc)" type="email" autoComplete="off" error={formState.errors.guestEmail?.message} {...register('guestEmail')} />
              <SelectField label="Nguồn" value={source} onChange={(e) => setSource(e.target.value as BookingSource)}>
                {SOURCES.map((s) => (
                  <option key={s} value={s}>
                    {sourceLabels[s]}
                  </option>
                ))}
              </SelectField>
            </div>
            <TextAreaField label="Ghi chú của khách" maxLength={500} error={formState.errors.note?.message} {...register('note')} />
            <TextAreaField label="Ghi chú nội bộ (khách không thấy)" maxLength={500} error={formState.errors.internalNote?.message} {...register('internalNote')} />
          </Card>

          <Card className="flex flex-col gap-4 p-5">
            <label className="flex items-center gap-2 font-semibold">
              <input type="checkbox" className="size-4 accent-brand-700" checked={collect} onChange={(e) => setCollect(e.target.checked)} />
              Thu tiền ngay
            </label>
            {collect && (
              <div className="grid gap-4 sm:grid-cols-3">
                <SelectField label="Loại" value={kind} onChange={(e) => setKind(e.target.value as PaymentKind)}>
                  {(['deposit', 'balance'] as const).map((k) => (
                    <option key={k} value={k}>
                      {k === 'balance' ? 'Thanh toán đủ' : paymentKindLabels[k]}
                    </option>
                  ))}
                </SelectField>
                <SelectField label="Hình thức" value={method} onChange={(e) => setMethod(e.target.value as PaymentMethod)}>
                  {METHODS.map((m) => (
                    <option key={m} value={m}>
                      {paymentMethodLabels[m]}
                    </option>
                  ))}
                </SelectField>
                <MoneyInput
                  label="Số tiền"
                  value={amount ?? (kind === 'deposit' ? quote.data?.depositAmount : quote.data?.total) ?? null}
                  onChange={setAmount}
                  hint={kind === 'deposit' ? 'Mặc định = tiền cọc' : 'Mặc định = tổng tiền'}
                />
              </div>
            )}
          </Card>
        </div>

        <Card className="flex flex-col gap-3 self-start p-5 lg:sticky lg:top-6">
          <h2 className="font-semibold">Báo giá</h2>
          {!ready ? (
            <p className="text-sm text-slate-600 dark:text-slate-300">Chọn phòng, ngày và số khách để xem giá.</p>
          ) : quote.isPending ? (
            <LoadingBlock label="Đang báo giá…" />
          ) : quote.isError ? (
            <Alert>{errorMessage(quote.error)}</Alert>
          ) : (
            <>
              <p className="text-sm font-medium">
                {room.name} · {stayRange(checkIn, checkOut)}
              </p>
              <QuoteSummary quote={quote.data} />
              {!quote.data.available && <Alert>Phòng đã có người đặt / đang khoá trong khoảng ngày này.</Alert>}
            </>
          )}
          <Button type="submit" className="h-11" loading={formState.isSubmitting} disabled={!quote.data?.available}>
            Tạo đặt phòng
          </Button>
          <Link to="/admin/calendar" className="text-center text-sm text-slate-600 hover:underline dark:text-slate-300">
            Huỷ, về lịch phòng
          </Link>
        </Card>
      </form>
    </div>
  )
}
