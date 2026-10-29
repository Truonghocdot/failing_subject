import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { TextAreaField, TextField } from '../../components/Field'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { ApiError, errorMessage, query } from '../../lib/api'
import { cancelPolicyLines, stayRange } from '../../lib/format'
import { applyServerErrors, emailOptional, fullName, normalizePhone, phoneRequired } from '../../lib/forms'
import type { Quote } from '../../lib/types'
import { useUser } from '../auth/useAuth'
import { readTrip, useQuote, useStay } from '../stays/api'
import { QuoteSummary } from '../stays/QuoteSummary'
import { useCreateBooking } from './api'

const schema = z.object({
  guestName: fullName,
  guestPhone: phoneRequired,
  guestEmail: emailOptional,
  note: z.string().trim().max(500, 'Ghi chú tối đa 500 ký tự'),
  agree: z.boolean().refine((v) => v, 'Vui lòng đồng ý chính sách đặt cọc và huỷ phòng'),
})
type Values = z.infer<typeof schema>

/** /book/:roomId?stay&checkIn&checkOut&guests – cần đăng nhập (RequireAuth giữ nguyên URL qua ?next=). */
export function BookPage() {
  const roomId = Number(useParams().roomId)
  const [params] = useSearchParams()
  const trip = readTrip(params)
  const slug = params.get('stay') ?? ''

  const valid = Number.isInteger(roomId) && roomId > 0 && trip.checkIn !== null && trip.checkOut !== null
  if (!valid)
    return (
      <div className="mx-auto flex max-w-xl flex-col gap-4">
        <h1 className="text-2xl font-semibold">Đặt phòng</h1>
        <Alert>Ngày nhận – trả phòng không hợp lệ hoặc đã qua. Vui lòng chọn lại.</Alert>
        <Link to={slug ? `/stays/${slug}` : '/stays'} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
          ← Chọn lại phòng và ngày
        </Link>
      </div>
    )
  return <BookForm roomId={roomId} slug={slug} checkIn={trip.checkIn!} checkOut={trip.checkOut!} guests={trip.guests} />
}

type FormProps = { roomId: number; slug: string; checkIn: string; checkOut: string; guests: number }

function BookForm({ roomId, slug, checkIn, checkOut, guests }: FormProps) {
  const user = useUser()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const stay = useStay(slug)
  const quoteInput = { roomId, checkIn, checkOut, guests }
  const quote = useQuote(quoteInput)
  const create = useCreateBooking()
  const [notice, setNotice] = useState<{ tone: 'error' | 'info'; text: string } | null>(null)

  const { register, handleSubmit, setError, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { guestName: user.fullName, guestPhone: user.phone ?? '', guestEmail: user.email, note: '', agree: false },
  })

  const room = stay.data?.rooms.find((r) => r.id === roomId)
  const policy = stay.data?.policy
  const backHref = slug ? `/stays/${slug}${query({ room: roomId, checkIn, checkOut, guests })}` : '/stays'

  const onSubmit = handleSubmit(async (v) => {
    if (!quote.data) return
    setNotice(null)
    try {
      const booking = await create.mutateAsync({
        ...quoteInput,
        guestName: v.guestName,
        guestPhone: normalizePhone(v.guestPhone),
        guestEmail: v.guestEmail || null,
        note: v.note || null,
        expectedTotal: quote.data.total,
      })
      navigate(`/bookings/${booking.code}`, { replace: true, state: { created: true } })
    } catch (e) {
      // 409: giá đổi (kèm báo giá mới) hoặc phòng vừa bị đặt – thông tin khách đã nhập vẫn giữ nguyên trong form
      if (e instanceof ApiError && e.status === 409) {
        const fresh = e.extensions.quote as Quote | undefined
        if (fresh) {
          qc.setQueryData(['quote', quoteInput], fresh)
          setNotice({ tone: 'info', text: `${e.message}. Bấm "Xác nhận đặt phòng" lần nữa để đặt với giá mới.` })
        } else {
          void qc.invalidateQueries({ queryKey: ['quote'] })
          setNotice({ tone: 'error', text: e.message })
        }
        return
      }
      const text = applyServerErrors(e, setError, ['guestName', 'guestPhone', 'guestEmail', 'note'])
      if (text) setNotice({ tone: 'error', text })
    }
  })

  const unavailable = quote.data?.available === false

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <div>
        <Link to={backHref} className="text-sm text-brand-700 hover:underline dark:text-brand-100">
          ← Đổi phòng / ngày
        </Link>
        <h1 className="mt-2 text-2xl font-semibold tracking-tight">Xác nhận đặt phòng</h1>
      </div>

      <div className="grid gap-6 md:grid-cols-[1fr_20rem]">
        <Card className="order-2 p-5 md:order-1">
          <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
            <h2 className="font-semibold">Thông tin người nhận phòng</h2>
            {notice && <Alert tone={notice.tone}>{notice.text}</Alert>}
            {unavailable && !notice && (
              <Alert>
                Phòng vừa có người đặt trong khoảng ngày này.{' '}
                <Link to={backHref} className="font-medium underline">
                  Chọn ngày khác
                </Link>
              </Alert>
            )}
            <TextField label="Họ tên" autoComplete="name" error={formState.errors.guestName?.message} {...register('guestName')} />
            <TextField label="Số điện thoại" type="tel" inputMode="tel" autoComplete="tel" hint="Dùng để liên hệ và tra cứu đặt phòng" error={formState.errors.guestPhone?.message} {...register('guestPhone')} />
            <TextField label="Email (không bắt buộc)" type="email" autoComplete="email" error={formState.errors.guestEmail?.message} {...register('guestEmail')} />
            <TextAreaField label="Ghi chú cho chủ nhà (không bắt buộc)" rows={3} maxLength={500} placeholder="VD: đến muộn khoảng 20:00" error={formState.errors.note?.message} {...register('note')} />

            {policy && (
              <div className="rounded-xl bg-slate-50 p-3 text-sm text-slate-700 dark:bg-slate-800/60 dark:text-slate-200">
                <p className="font-medium">Chính sách đặt cọc & huỷ</p>
                <ul className="mt-1 list-disc pl-5">
                  <li>
                    Giữ chỗ {policy.holdHours} giờ – chuyển khoản cọc {policy.depositPercent}% trong thời gian này để xác nhận, quá hạn đặt phòng tự huỷ
                  </li>
                  {cancelPolicyLines(policy).map((l) => (
                    <li key={l}>{l}</li>
                  ))}
                </ul>
              </div>
            )}
            <div className="flex flex-col gap-1">
              <label className="flex items-start gap-2 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5 size-4 accent-brand-700"
                  aria-invalid={!!formState.errors.agree}
                  aria-describedby={formState.errors.agree ? 'agree-error' : undefined}
                  {...register('agree')}
                />
                Tôi đồng ý chính sách đặt cọc và huỷ phòng
              </label>
              {formState.errors.agree && (
                <p id="agree-error" className="text-xs text-red-600 dark:text-red-400">
                  {formState.errors.agree.message}
                </p>
              )}
            </div>
            <Button type="submit" className="h-11" loading={formState.isSubmitting} disabled={!quote.data || unavailable || quote.isFetching}>
              Xác nhận đặt phòng
            </Button>
          </form>
        </Card>

        <Card className="order-1 flex flex-col gap-3 self-start p-5 md:order-2">
          <div>
            <p className="font-semibold">{stay.data?.name ?? 'Homestay'}</p>
            <p className="text-sm text-slate-600 dark:text-slate-300">{room?.name ?? `Phòng #${roomId}`}</p>
          </div>
          <p className="text-sm">
            {stayRange(checkIn, checkOut)} · {guests} khách
            {policy && (
              <span className="block text-slate-600 dark:text-slate-300">
                Nhận từ {policy.checkInTime} · trả trước {policy.checkOutTime}
              </span>
            )}
          </p>
          {quote.isPending ? <LoadingBlock label="Đang báo giá…" /> : quote.isError ? <Alert>{errorMessage(quote.error)}</Alert> : <QuoteSummary quote={quote.data} />}
        </Card>
      </div>
    </div>
  )
}
