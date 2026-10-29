import { CircleCheck } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { TextAreaField } from '../../components/Field'
import { Alert, LoadingBlock } from '../../components/Feedback'
import { NotFoundPage } from '../../app/NotFoundPage'
import { ApiError, errorMessage } from '../../lib/api'
import { money } from '../../lib/format'
import type { Booking } from '../../lib/types'
import { useBooking, useCancelBooking, useCancelPreview } from './api'
import { BookingView } from './BookingView'
import { ReviewSection } from './ReviewSection'

/** /bookings/:code – đặt phòng của khách đang đăng nhập (người khác mở → 404). */
export function BookingDetailPage() {
  const { code = '' } = useParams()
  const created = (useLocation().state as { created?: boolean } | null)?.created === true
  const booking = useBooking(code)

  if (booking.isPending) return <LoadingBlock />
  if (booking.isError)
    return booking.error instanceof ApiError && booking.error.status === 404 ? (
      <NotFoundPage homeHref="/bookings" homeLabel="Về danh sách đặt phòng" />
    ) : (
      <Alert>{errorMessage(booking.error)}</Alert>
    )

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-4">
      <Link to="/bookings" className="text-sm text-brand-700 hover:underline dark:text-brand-100">
        ← Đặt phòng của tôi
      </Link>
      {created && (
        <p role="status" className="flex items-center gap-2 rounded-xl bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-100">
          <CircleCheck className="size-5 shrink-0" aria-hidden />
          Đặt phòng thành công! Lưu lại mã {booking.data.code} để tra cứu.
        </p>
      )}
      <BookingView
        booking={booking.data}
        actions={
          <>
            <ReviewSection booking={booking.data} />
            <CancelAction booking={booking.data} />
          </>
        }
      />
    </div>
  )
}

/** Khách tự huỷ được khi đặt phòng còn chờ cọc / đã xác nhận và trước ngày nhận phòng – xem trước số hoàn trước khi bấm. */
function CancelAction({ booking }: { booking: Booking }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const preview = useCancelPreview(booking.code, open)
  const cancel = useCancelBooking(booking.code)

  if (booking.status !== 'pending' && booking.status !== 'confirmed') return null

  const close = () => {
    setOpen(false)
    cancel.reset()
  }

  const p = preview.data
  return (
    <div>
      <Button variant="secondary" onClick={() => setOpen(true)}>
        Huỷ đặt phòng
      </Button>
      <Dialog
        open={open}
        onClose={close}
        title={`Huỷ đặt phòng ${booking.code}`}
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={close}>
              Giữ đặt phòng
            </Button>
            <Button
              variant="danger"
              loading={cancel.isPending}
              disabled={!p?.canCancel}
              onClick={() => cancel.mutate(reason.trim() || null, { onSuccess: () => setOpen(false) })}
            >
              Xác nhận huỷ
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-4 text-sm">
          {preview.isPending ? (
            <LoadingBlock label="Đang tính số tiền hoàn…" />
          ) : preview.isError ? (
            <Alert>{errorMessage(preview.error)}</Alert>
          ) : !p!.canCancel ? (
            <Alert>{p!.reason ?? 'Không thể huỷ đặt phòng này'}</Alert>
          ) : (
            <dl className="flex flex-col gap-1.5 rounded-xl bg-slate-50 p-3 dark:bg-slate-800/60">
              <div className="flex justify-between gap-3">
                <dt>Còn đến ngày nhận phòng</dt>
                <dd>{p!.daysBefore} ngày</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt>Đã thanh toán</dt>
                <dd>{money(p!.paid)}</dd>
              </div>
              <div className="flex justify-between gap-3 font-semibold">
                <dt>Được hoàn ({p!.refundPercent}%)</dt>
                <dd>{money(p!.refundAmount)}</dd>
              </div>
            </dl>
          )}
          {p?.canCancel && (
            <TextAreaField label="Lý do (không bắt buộc)" rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
          )}
          {cancel.isError && <Alert>{errorMessage(cancel.error)}</Alert>}
        </div>
      </Dialog>
    </div>
  )
}
