import { useQueryClient } from '@tanstack/react-query'
import { Check, Copy, Landmark, MapPin, TimerReset } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { Card } from '../../components/Feedback'
import { useNow } from '../../lib/useNow'
import { StatusBadge } from '../../components/StatusBadge'
import { countdown, dateTime, dayLabel, money, paymentStatusLabels, stayRange, statusLabels } from '../../lib/format'
import type { Booking, BookingEvent, TransferInfo } from '../../lib/types'

/** Chi tiết 1 đặt phòng cho khách – dùng chung trang của tôi và tra cứu; `actions` = nút huỷ… */
export function BookingView({ booking, actions }: { booking: Booking; actions?: ReactNode }) {
  const b = booking
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-sm text-slate-600 dark:text-slate-300">Mã đặt phòng</p>
          <h1 className="font-mono text-2xl font-semibold tracking-wide">{b.code}</h1>
        </div>
        <StatusBadge status={b.status} />
      </div>

      {b.status === 'pending' && <PendingBox booking={b} />}
      {b.status === 'confirmed' && (
        <p role="status" className="rounded-xl bg-sky-50 px-4 py-3 text-sm text-sky-900 dark:bg-sky-950/60 dark:text-sky-100">
          Đặt phòng đã được xác nhận. Hẹn gặp bạn {dayLabel(b.checkIn)} từ {b.checkInTime}.
        </p>
      )}
      {b.status === 'expired' && (
        <p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-800 dark:bg-red-950/60 dark:text-red-200">
          Quá hạn giữ chỗ mà chưa nhận được cọc nên đặt phòng đã tự huỷ. Bạn có thể{' '}
          <Link to={`/stays/${b.homestaySlug}`} className="font-medium underline">
            đặt lại
          </Link>
          .
        </p>
      )}
      {b.status === 'cancelled' && (
        <div className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-800 dark:bg-red-950/60 dark:text-red-200">
          <p>
            Đã huỷ{b.cancelledAt && ` lúc ${dateTime(b.cancelledAt)}`}
            {b.cancelReason && ` · Lý do: ${b.cancelReason}`}
          </p>
          {b.refundAmount > 0 && (
            <p className="mt-1">
              Số tiền được hoàn: <strong>{money(b.refundAmount)}</strong>
              {b.money.pendingRefund > 0 ? ` – chủ nhà sẽ chuyển trả ${money(b.money.pendingRefund)} còn lại` : ' – đã hoàn xong'}
            </p>
          )}
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        <Card className="flex flex-col gap-3 p-5">
          <div>
            <Link to={`/stays/${b.homestaySlug}`} className="font-semibold hover:underline">
              {b.homestayName}
            </Link>
            <p className="flex items-start gap-1 text-sm text-slate-600 dark:text-slate-300">
              <MapPin className="mt-0.5 size-4 shrink-0" aria-hidden />
              {b.homestayAddress}
            </p>
          </div>
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
            <dt className="text-slate-600 dark:text-slate-300">Phòng</dt>
            <dd>{b.roomName}</dd>
            <dt className="text-slate-600 dark:text-slate-300">Ngày ở</dt>
            <dd>{stayRange(b.checkIn, b.checkOut)}</dd>
            <dt className="text-slate-600 dark:text-slate-300">Giờ</dt>
            <dd>
              Nhận từ {b.checkInTime} · trả trước {b.checkOutTime}
            </dd>
            <dt className="text-slate-600 dark:text-slate-300">Khách</dt>
            <dd>
              {b.guestName} · {b.guestPhone} · {b.guests} người
            </dd>
            {b.guestNote && (
              <>
                <dt className="text-slate-600 dark:text-slate-300">Ghi chú</dt>
                <dd className="whitespace-pre-line">{b.guestNote}</dd>
              </>
            )}
          </dl>
        </Card>

        <Card className="flex flex-col gap-3 p-5">
          <h2 className="font-semibold">Tiền phòng</h2>
          <dl className="flex flex-col gap-1.5 text-sm">
            <Row label={`Tiền phòng (${b.nights} đêm)`} value={money(b.roomTotal)} />
            {b.extraGuestTotal > 0 && <Row label="Phụ thu khách thêm" value={money(b.extraGuestTotal)} />}
            {b.cleaningFee > 0 && <Row label="Phí dọn phòng" value={money(b.cleaningFee)} />}
            {b.money.extras > 0 && <Row label="Phát sinh" value={money(b.money.extras)} />}
            <Row label="Tổng cộng" value={money(b.total + b.money.extras)} strong />
            <Row label={`Cọc (${b.policy.depositPercent}%)`} value={money(b.depositAmount)} />
            <Row label="Đã thanh toán" value={money(b.money.paid)} />
            {b.money.refunded > 0 && <Row label="Đã hoàn" value={money(b.money.refunded)} />}
            {b.money.amountDue > 0 && <Row label="Còn phải trả" value={money(b.money.amountDue)} strong />}
          </dl>
          <p className="text-xs text-slate-500 dark:text-slate-400">{paymentStatusLabels[b.money.status]}</p>
        </Card>
      </div>

      {b.events.length > 0 && (
        <Card className="p-5">
          <h2 className="mb-3 font-semibold">Các mốc</h2>
          <ol className="flex flex-col gap-2 text-sm">
            {b.events.map((e) => (
              <li key={e.id} className="flex flex-wrap justify-between gap-x-3">
                <span>{eventLabel(e)}</span>
                <time dateTime={e.createdAt} className="text-slate-500 dark:text-slate-400">
                  {dateTime(e.createdAt)}
                </time>
              </li>
            ))}
          </ol>
        </Card>
      )}

      {actions}
    </div>
  )
}

function eventLabel(e: BookingEvent) {
  if (e.type === 'created') return 'Đặt phòng'
  if (e.type === 'rescheduled') return 'Đổi ngày / phòng'
  if (e.type === 'status' && e.toStatus) return statusLabels[e.toStatus]
  return e.message ?? e.type
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={`flex justify-between gap-3 ${strong ? 'font-semibold' : 'text-slate-700 dark:text-slate-200'}`}>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}

/** Đồng hồ hạn giữ chỗ (đếm từng giây); hết giờ → làm mới đặt phòng để thấy trạng thái "hết hạn" từ máy chủ. */
function PendingBox({ booking }: { booking: Booking }) {
  const qc = useQueryClient()
  const now = useNow()
  const left = booking.holdUntil ? countdown(booking.holdUntil, now) : null

  useEffect(() => {
    if (booking.holdUntil && !left) void qc.invalidateQueries({ queryKey: ['booking', booking.code] })
  }, [booking.holdUntil, booking.code, left, qc])

  return (
    <Card className="flex flex-col gap-4 border-amber-300 bg-amber-50/60 p-5 dark:border-amber-800 dark:bg-amber-950/30">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="flex items-center gap-2 font-semibold text-amber-900 dark:text-amber-100">
          <TimerReset className="size-5" aria-hidden />
          Đang giữ chỗ – chờ chuyển khoản cọc
        </p>
        {booking.holdUntil && (
          <p className="text-sm text-amber-900 dark:text-amber-100">
            {left ? (
              <>
                Còn <span className="font-mono text-lg font-semibold tabular-nums">{left}</span>
              </>
            ) : (
              'Đã hết hạn giữ chỗ'
            )}
            <span className="block text-xs">Hạn: {dateTime(booking.holdUntil)}</span>
          </p>
        )}
      </div>
      {booking.transfer ? (
        <TransferBox transfer={booking.transfer} />
      ) : (
        <p className="text-sm text-amber-900 dark:text-amber-100">Chủ nhà sẽ liên hệ qua số {booking.guestPhone} để hướng dẫn đặt cọc.</p>
      )}
      <p className="text-xs text-amber-900/80 dark:text-amber-100/80">Đặt phòng tự xác nhận khi chủ nhà ghi nhận đủ cọc. Quá hạn mà chưa cọc, phòng được nhả cho khách khác.</p>
    </Card>
  )
}

function TransferBox({ transfer: t }: { transfer: TransferInfo }) {
  return (
    <div className="rounded-xl bg-white p-4 dark:bg-slate-900">
      <p className="mb-3 flex items-center gap-2 text-sm font-medium">
        <Landmark className="size-4" aria-hidden />
        Hướng dẫn chuyển khoản
      </p>
      <dl className="grid gap-2 text-sm sm:grid-cols-[auto_1fr] sm:gap-x-4">
        <dt className="text-slate-600 dark:text-slate-300">Ngân hàng</dt>
        <dd className="font-medium">{t.bankName}</dd>
        <dt className="text-slate-600 dark:text-slate-300">Số tài khoản</dt>
        <dd>
          <Copyable value={t.accountNumber} label="số tài khoản" />
        </dd>
        <dt className="text-slate-600 dark:text-slate-300">Chủ tài khoản</dt>
        <dd className="font-medium">{t.accountName}</dd>
        <dt className="text-slate-600 dark:text-slate-300">Số tiền</dt>
        <dd>
          <Copyable value={String(t.amount)} display={money(t.amount)} label="số tiền" />
        </dd>
        <dt className="text-slate-600 dark:text-slate-300">Nội dung</dt>
        <dd>
          <Copyable value={t.content} label="nội dung chuyển khoản" />
        </dd>
      </dl>
    </div>
  )
}

function Copyable({ value, display, label }: { value: string; display?: string; label: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <span className="font-mono font-semibold break-all">{display ?? value}</span>
      <button
        type="button"
        className="inline-flex items-center gap-1 rounded-md border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
        aria-label={`Sao chép ${label}`}
        onClick={async () => {
          try {
            await navigator.clipboard.writeText(value)
            setCopied(true)
            setTimeout(() => setCopied(false), 2000)
          } catch {
            // trình duyệt chặn clipboard (http không bảo mật…) – khách vẫn tự chép được
          }
        }}
      >
        {copied ? <Check className="size-3.5" aria-hidden /> : <Copy className="size-3.5" aria-hidden />}
        {copied ? 'Đã chép' : 'Chép'}
      </button>
    </span>
  )
}
