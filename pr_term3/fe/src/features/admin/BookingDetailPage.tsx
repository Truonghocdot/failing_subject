import { CalendarClock, LogIn, LogOut, Pencil, Plus, Trash2, UserX } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router'
import { NotFoundPage } from '../../app/NotFoundPage'
import { Button, IconButton } from '../../components/Button'
import { ConfirmDialog, Dialog } from '../../components/Dialog'
import { SelectField, TextAreaField, TextField } from '../../components/Field'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { StatusBadge } from '../../components/StatusBadge'
import { DateRangeFields, GuestStepper } from '../../components/TripFields'
import { ApiError, errorMessage } from '../../lib/api'
import { dateTime, dayLabel, money, paymentKindLabels, paymentMethodLabels, sourceLabels, stayRange, statusLabels } from '../../lib/format'
import { isEmailish, isPhone, normalizePhone } from '../../lib/forms'
import type { Booking, PaymentKind, PaymentMethod } from '../../lib/types'
import { tripError, useQuote } from '../stays/api'
import { QuoteSummary } from '../stays/QuoteSummary'
import { useAdminCancelPreview, useAdminHomestay, useBookingAction, useAdminBooking } from './api'
import { PageHeader, PaymentLabel } from './shared'

type DialogName = 'confirm' | 'checkout' | 'noshow' | 'cancel' | 'payment' | 'reschedule' | 'edit' | null

/** /admin/bookings/:id – nút theo trạng thái, khoản thu – hoàn, đổi ngày / phòng, nhật ký. */
export function BookingDetailPage() {
  const id = Number(useParams().id)
  const booking = useAdminBooking(id)
  if (booking.isPending) return <LoadingBlock />
  if (booking.isError)
    return booking.error instanceof ApiError && booking.error.status === 404 ? <NotFoundPage homeHref="/admin/bookings" homeLabel="Về danh sách đặt phòng" /> : <Alert>{errorMessage(booking.error)}</Alert>
  return <Detail b={booking.data} />
}

function Detail({ b }: { b: Booking }) {
  const [open, setOpen] = useState<DialogName>(null)
  const [deleting, setDeleting] = useState<number | null>(null)
  const action = useBookingAction(b.id)
  const close = () => {
    setOpen(null)
    action.reset()
  }
  const run = (path: string, body?: unknown, method?: 'POST' | 'PUT' | 'DELETE') => action.mutate({ path, body, method }, { onSuccess: () => setOpen(null) })

  const active = b.status === 'pending' || b.status === 'confirmed'
  const payments = b.payments ?? []
  const canPay = !['cancelled', 'expired', 'no_show'].includes(b.status) || b.money.paid > 0

  return (
    <div className="mx-auto max-w-5xl">
      <Link to="/admin/bookings" className="text-sm text-brand-700 hover:underline dark:text-brand-100">
        ← Danh sách đặt phòng
      </Link>
      <PageHeader
        title={b.code}
        description={
          <span className="flex flex-wrap items-center gap-2">
            <StatusBadge status={b.status} />
            <PaymentLabel status={b.money.status} />
            <span>
              · {sourceLabels[b.source]} · tạo {dateTime(b.createdAt)}
            </span>
          </span>
        }
        actions={
          <>
            {b.status === 'pending' && <Button onClick={() => setOpen('confirm')}>Xác nhận</Button>}
            {b.status === 'confirmed' && (
              <Button loading={action.isPending && open === null} onClick={() => run('/check-in')}>
                <LogIn className="size-4" aria-hidden />
                Check-in
              </Button>
            )}
            {b.status === 'checked_in' && (
              <Button onClick={() => setOpen('checkout')}>
                <LogOut className="size-4" aria-hidden />
                Check-out
              </Button>
            )}
            {canPay && (
              <Button variant="secondary" onClick={() => setOpen('payment')}>
                <Plus className="size-4" aria-hidden />
                {['cancelled', 'expired', 'no_show'].includes(b.status) ? 'Ghi hoàn tiền' : 'Ghi khoản thu'}
              </Button>
            )}
            {active && (
              <Button variant="secondary" onClick={() => setOpen('reschedule')}>
                <CalendarClock className="size-4" aria-hidden />
                Đổi ngày / phòng
              </Button>
            )}
            {b.status === 'confirmed' && (
              <Button variant="ghost" onClick={() => setOpen('noshow')}>
                <UserX className="size-4" aria-hidden />
                Không đến
              </Button>
            )}
            {active && (
              <Button variant="ghost" className="text-red-700 dark:text-red-300" onClick={() => setOpen('cancel')}>
                Huỷ đặt phòng
              </Button>
            )}
          </>
        }
      />
      {action.isError && open === null && (
        <div className="mb-4">
          <Alert>{errorMessage(action.error)}</Alert>
        </div>
      )}
      {b.status === 'pending' && b.holdUntil && (
        <div className="mb-4">
          <Alert tone="info">
            Giữ chỗ đến {dateTime(b.holdUntil)} – ghi đủ cọc {money(b.depositAmount)} thì tự xác nhận.
          </Alert>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="p-5">
          <SectionTitle title="Khách" action={<IconButton label="Sửa thông tin khách" onClick={() => setOpen('edit')}><Pencil className="size-4" aria-hidden /></IconButton>} />
          <Dl
            rows={[
              ['Họ tên', b.guestName],
              ['Điện thoại', <a key="p" href={`tel:${b.guestPhone}`} className="text-brand-700 hover:underline dark:text-brand-100">{b.guestPhone}</a>],
              ['Email', b.guestEmail ?? '—'],
              ['Tài khoản', b.userId ? 'Khách tự đặt online' : 'Lễ tân đặt hộ'],
              ['Ghi chú khách', b.guestNote ?? '—'],
              ['Ghi chú nội bộ', b.internalNote ?? '—'],
            ]}
          />
          <Link to={`/admin/bookings?q=${encodeURIComponent(b.guestPhone)}`} className="mt-3 inline-block text-sm text-brand-700 hover:underline dark:text-brand-100">
            Lịch sử lưu trú của SĐT này
          </Link>
        </Card>

        <Card className="p-5">
          <SectionTitle title="Ngày ở" />
          <Dl
            rows={[
              ['Homestay', b.homestayName],
              ['Phòng', b.roomName],
              ['Ngày', stayRange(b.checkIn, b.checkOut)],
              ['Giờ', `Nhận ${b.checkInTime} · trả ${b.checkOutTime}`],
              ['Số khách', `${b.guests}`],
              ...(b.checkedInAt ? [['Đã nhận', dateTime(b.checkedInAt)] as [string, string]] : []),
              ...(b.checkedOutAt ? [['Đã trả', dateTime(b.checkedOutAt)] as [string, string]] : []),
              ...(b.cancelledAt ? [['Huỷ lúc', `${dateTime(b.cancelledAt)}${b.cancelReason ? ` – ${b.cancelReason}` : ''}`] as [string, string]] : []),
            ]}
          />
          {b.nightPrices.length > 0 && (
            <details className="mt-3 text-sm">
              <summary className="cursor-pointer text-slate-600 dark:text-slate-300">Giá đã chốt từng đêm</summary>
              <ul className="mt-2 flex flex-col gap-1">
                {b.nightPrices.map((n) => (
                  <li key={n.night} className="flex justify-between">
                    <span>{dayLabel(n.night)}</span>
                    <span className="tabular-nums">{money(n.price)}</span>
                  </li>
                ))}
              </ul>
            </details>
          )}
        </Card>

        <Card className="p-5 lg:col-span-2">
          <SectionTitle title="Tiền" />
          <div className="grid gap-x-8 gap-y-2 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <MoneyCell label="Tổng" value={b.total + b.money.extras} hint={b.money.extras > 0 ? `gồm phát sinh ${money(b.money.extras)}` : `phòng ${money(b.roomTotal)} · phụ thu ${money(b.extraGuestTotal)} · dọn ${money(b.cleaningFee)}`} />
            <MoneyCell label={`Cọc (${b.policy.depositPercent}%)`} value={b.depositAmount} />
            <MoneyCell label="Đã thu (trừ hoàn)" value={b.money.paid} hint={b.money.refunded > 0 ? `đã hoàn ${money(b.money.refunded)}` : undefined} />
            {b.money.pendingRefund > 0 ? (
              <MoneyCell label="Còn phải hoàn" value={b.money.pendingRefund} strong hint={`chốt hoàn ${money(b.refundAmount)} khi huỷ`} />
            ) : (
              <MoneyCell label="Còn phải thu" value={b.money.amountDue} strong={b.money.amountDue > 0} />
            )}
          </div>
          <div className="mt-4 overflow-x-auto">
            {payments.length === 0 ? (
              <p className="text-sm text-slate-500 dark:text-slate-400">Chưa có khoản thu nào.</p>
            ) : (
              <table className="w-full text-sm">
                <thead className="text-left text-xs text-slate-500 dark:text-slate-400">
                  <tr>
                    <th className="py-1.5 pr-3 font-medium">Thời điểm</th>
                    <th className="py-1.5 pr-3 font-medium">Loại</th>
                    <th className="py-1.5 pr-3 font-medium">Hình thức</th>
                    <th className="py-1.5 pr-3 text-right font-medium">Số tiền</th>
                    <th className="py-1.5 pr-3 font-medium">Ghi chú</th>
                    <th className="py-1.5 font-medium">
                      <span className="sr-only">Xoá</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {payments.map((p) => (
                    <tr key={p.id}>
                      <td className="py-1.5 pr-3 whitespace-nowrap">{dateTime(p.paidAt)}</td>
                      <td className="py-1.5 pr-3">{paymentKindLabels[p.kind]}</td>
                      <td className="py-1.5 pr-3">{paymentMethodLabels[p.method]}</td>
                      <td className={`py-1.5 pr-3 text-right tabular-nums ${p.kind === 'refund' ? 'text-red-700 dark:text-red-300' : ''}`}>
                        {p.kind === 'refund' ? '−' : ''}
                        {money(p.amount)}
                      </td>
                      <td className="py-1.5 pr-3 text-slate-600 dark:text-slate-300">
                        {p.note}
                        {p.recordedBy && <span className="block text-xs text-slate-500">ghi bởi {p.recordedBy}</span>}
                      </td>
                      <td className="py-1.5 text-right">
                        <IconButton label={`Xoá khoản ${paymentKindLabels[p.kind]} ${money(p.amount)}`} onClick={() => setDeleting(p.id)}>
                          <Trash2 className="size-4" aria-hidden />
                        </IconButton>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </Card>

        <Card className="p-5 lg:col-span-2">
          <SectionTitle title="Nhật ký" />
          <ol className="flex flex-col gap-2 text-sm">
            {b.events.map((e) => (
              <li key={e.id} className="flex flex-col gap-0.5 border-l-2 border-slate-200 pl-3 sm:flex-row sm:justify-between sm:gap-4 dark:border-slate-700">
                <span>
                  {e.type === 'created' ? 'Tạo đặt phòng' : e.type === 'status' && e.toStatus ? `→ ${statusLabels[e.toStatus]}` : ''}
                  {e.message && <span className="text-slate-600 dark:text-slate-300">{e.type === 'status' || e.type === 'created' ? ' – ' : ''}{e.message}</span>}
                </span>
                <span className="shrink-0 text-xs text-slate-500 dark:text-slate-400">
                  {dateTime(e.createdAt)} · {e.actor ?? 'Hệ thống'}
                </span>
              </li>
            ))}
          </ol>
        </Card>
      </div>

      {open === 'confirm' && (
        <ReasonDialog
          title="Xác nhận đặt phòng"
          message={b.money.paid >= b.depositAmount ? 'Đã nhận đủ cọc.' : `Chưa nhận đủ cọc (${money(b.money.paid)} / ${money(b.depositAmount)}) – ghi lý do xác nhận (VD khách quen, đã hẹn chuyển sau).`}
          required={b.money.paid < b.depositAmount}
          confirmLabel="Xác nhận"
          pending={action.isPending}
          error={action.error}
          onClose={close}
          onSubmit={(reason) => run('/confirm', { reason })}
        />
      )}
      {open === 'checkout' && (
        <ReasonDialog
          title="Check-out"
          message={b.money.amountDue > 0 ? `Khách còn phải thu ${money(b.money.amountDue)}. Thu đủ trước (Ghi khoản thu) hoặc ghi lý do bỏ qua.` : 'Khách đã thanh toán đủ.'}
          required={b.money.amountDue > 0}
          confirmLabel="Trả phòng"
          pending={action.isPending}
          error={action.error}
          onClose={close}
          onSubmit={(reason) => run('/check-out', { reason })}
        />
      )}
      {open === 'noshow' && (
        <ReasonDialog
          title="Khách không đến"
          message="Đánh dấu không đến sẽ nhả các đêm còn lại cho khách khác đặt. Tiền cọc không tự hoàn."
          required={false}
          confirmLabel="Đánh dấu không đến"
          danger
          pending={action.isPending}
          error={action.error}
          onClose={close}
          onSubmit={(reason) => run('/no-show', { reason })}
        />
      )}
      {open === 'cancel' && <CancelDialog b={b} pending={action.isPending} error={action.error} onClose={close} onSubmit={(body) => run('/cancel', body)} />}
      {open === 'payment' && <PaymentDialog b={b} pending={action.isPending} error={action.error} onClose={close} onSubmit={(body) => run('/payments', body)} />}
      {open === 'reschedule' && <RescheduleDialog b={b} pending={action.isPending} error={action.error} onClose={close} onSubmit={(body) => run('/reschedule', body)} />}
      {open === 'edit' && <EditGuestDialog b={b} pending={action.isPending} error={action.error} onClose={close} onSubmit={(body) => run('', body, 'PUT')} />}
      <ConfirmDialog
        open={deleting !== null}
        title="Xoá khoản ghi nhầm"
        message="Chỉ xoá khi ghi nhầm (lễ tân: trong 24 giờ). Thao tác được ghi vào nhật ký."
        confirmLabel="Xoá khoản"
        danger
        loading={action.isPending}
        error={action.isError && deleting !== null ? errorMessage(action.error) : undefined}
        onCancel={() => {
          setDeleting(null)
          action.reset()
        }}
        onConfirm={() => action.mutate({ path: `/payments/${deleting}`, method: 'DELETE' }, { onSuccess: () => setDeleting(null) })}
      />
    </div>
  )
}

function SectionTitle({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <div className="mb-3 flex items-center justify-between gap-2">
      <h2 className="font-semibold">{title}</h2>
      {action}
    </div>
  )
}

function Dl({ rows }: { rows: [string, ReactNode][] }) {
  return (
    <dl className="grid grid-cols-[7.5rem_1fr] gap-x-3 gap-y-1.5 text-sm">
      {rows.map(([k, v]) => (
        <div key={k} className="contents">
          <dt className="text-slate-600 dark:text-slate-300">{k}</dt>
          <dd className="min-w-0 break-words whitespace-pre-line">{v}</dd>
        </div>
      ))}
    </dl>
  )
}

function MoneyCell({ label, value, hint, strong }: { label: string; value: number; hint?: string; strong?: boolean }) {
  return (
    <div>
      <p className="text-slate-600 dark:text-slate-300">{label}</p>
      <p className={`text-lg tabular-nums ${strong ? 'font-semibold text-amber-800 dark:text-amber-300' : 'font-medium'}`}>{money(value)}</p>
      {hint && <p className="text-xs text-slate-500 dark:text-slate-400">{hint}</p>}
    </div>
  )
}

type DialogProps<T> = { pending: boolean; error: unknown; onClose: () => void; onSubmit: (body: T) => void }

function DialogFooter({ onClose, pending, label, danger, disabled }: { onClose: () => void; pending: boolean; label: string; danger?: boolean; disabled?: boolean }) {
  return (
    <>
      <Button variant="secondary" onClick={onClose}>
        Quay lại
      </Button>
      <Button type="submit" form="admin-dialog-form" variant={danger ? 'danger' : 'primary'} loading={pending} disabled={disabled}>
        {label}
      </Button>
    </>
  )
}

function ReasonDialog({
  title,
  message,
  required,
  confirmLabel,
  danger,
  ...p
}: DialogProps<string | null> & { title: string; message: string; required: boolean; confirmLabel: string; danger?: boolean }) {
  const [reason, setReason] = useState('')
  const [local, setLocal] = useState<string | null>(null)
  return (
    <Dialog open onClose={p.onClose} title={title} size="sm" footer={<DialogFooter onClose={p.onClose} pending={p.pending} label={confirmLabel} danger={danger} />}>
      <form
        id="admin-dialog-form"
        noValidate
        className="flex flex-col gap-4 text-sm"
        onSubmit={(e) => {
          e.preventDefault()
          if (required && !reason.trim()) return setLocal('Vui lòng nhập lý do')
          p.onSubmit(reason.trim() || null)
        }}
      >
        <p>{message}</p>
        <TextAreaField label={required ? 'Lý do' : 'Lý do (không bắt buộc)'} maxLength={300} value={reason} error={local ?? undefined} onChange={(e) => setReason(e.target.value)} />
        {!!p.error && <Alert>{errorMessage(p.error)}</Alert>}
      </form>
    </Dialog>
  )
}

function CancelDialog({ b, ...p }: DialogProps<{ reason: string; refundAmount: number | null }> & { b: Booking }) {
  const preview = useAdminCancelPreview(b.id, true)
  const [reason, setReason] = useState('')
  const [refund, setRefund] = useState<number | null>(null)
  const [local, setLocal] = useState<string | null>(null)
  const suggested = preview.data?.refundAmount ?? 0
  return (
    <Dialog open onClose={p.onClose} title={`Huỷ ${b.code}`} size="sm" footer={<DialogFooter onClose={p.onClose} pending={p.pending} label="Huỷ đặt phòng" danger disabled={!preview.data?.canCancel} />}>
      <form
        id="admin-dialog-form"
        noValidate
        className="flex flex-col gap-4 text-sm"
        onSubmit={(e) => {
          e.preventDefault()
          if (!reason.trim()) return setLocal('Vui lòng nhập lý do huỷ')
          p.onSubmit({ reason: reason.trim(), refundAmount: refund })
        }}
      >
        {preview.isPending ? (
          <LoadingBlock label="Đang tính số hoàn…" />
        ) : preview.isError ? (
          <Alert>{errorMessage(preview.error)}</Alert>
        ) : !preview.data.canCancel ? (
          <Alert>{preview.data.reason}</Alert>
        ) : (
          <>
            <p>
              Còn {preview.data.daysBefore} ngày tới ngày nhận phòng · đã thu {money(preview.data.paid)} · theo chính sách hoàn {preview.data.refundPercent}% = <strong>{money(suggested)}</strong>
            </p>
            <MoneyInput label="Số hoàn chốt cho khách" value={refund ?? suggested} onChange={setRefund} hint="Sửa được (VD huỷ do phía homestay → hoàn đủ). Ghi khoản hoàn sau khi đã chuyển trả." />
            <TextAreaField label="Lý do huỷ" maxLength={500} value={reason} error={local ?? undefined} onChange={(e) => setReason(e.target.value)} />
          </>
        )}
        {!!p.error && <Alert>{errorMessage(p.error)}</Alert>}
      </form>
    </Dialog>
  )
}

const METHODS = Object.keys(paymentMethodLabels) as PaymentMethod[]

function PaymentDialog({ b, ...p }: DialogProps<{ kind: PaymentKind; method: PaymentMethod; amount: number; paidAt: string | null; note: string | null }> & { b: Booking }) {
  const closed = ['cancelled', 'expired', 'no_show'].includes(b.status)
  const kinds: PaymentKind[] = closed ? ['refund'] : b.status === 'pending' ? ['deposit', 'balance', 'refund'] : ['balance', 'extra', 'deposit', 'refund']
  const [kind, setKind] = useState<PaymentKind>(kinds[0])
  const [method, setMethod] = useState<PaymentMethod>('bank_transfer')
  const [amount, setAmount] = useState<number | null>(null)
  const [paidAt, setPaidAt] = useState('')
  const [note, setNote] = useState('')
  const [local, setLocal] = useState<string | null>(null)

  const suggested =
    kind === 'refund' ? b.money.pendingRefund || null : kind === 'deposit' ? Math.max(0, b.depositAmount - b.money.paid) || null : kind === 'balance' ? b.money.amountDue || null : null
  const value = amount ?? suggested
  return (
    <Dialog open onClose={p.onClose} title="Ghi khoản thu / hoàn" footer={<DialogFooter onClose={p.onClose} pending={p.pending} label="Ghi khoản" />}>
      <form
        id="admin-dialog-form"
        noValidate
        className="grid gap-4 sm:grid-cols-2"
        onSubmit={(e) => {
          e.preventDefault()
          if (!value) return setLocal('Vui lòng nhập số tiền')
          p.onSubmit({ kind, method, amount: value, paidAt: paidAt ? new Date(paidAt).toISOString() : null, note: note.trim() || null })
        }}
      >
        <SelectField
          label="Loại"
          value={kind}
          onChange={(e) => {
            setKind(e.target.value as PaymentKind)
            setAmount(null)
          }}
        >
          {kinds.map((k) => (
            <option key={k} value={k}>
              {paymentKindLabels[k]}
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
        <MoneyInput label="Số tiền" value={value} onChange={setAmount} error={local ?? undefined} hint={kind === 'extra' ? 'Minibar, thuê xe…' : undefined} />
        <TextField label="Thời điểm thu (bỏ trống = bây giờ)" type="datetime-local" value={paidAt} onChange={(e) => setPaidAt(e.target.value)} />
        <div className="sm:col-span-2">
          <TextField label="Ghi chú" maxLength={200} value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: mã giao dịch VCB 123456" />
        </div>
        {b.status === 'pending' && kind === 'deposit' && <p className="text-sm text-slate-600 sm:col-span-2 dark:text-slate-300">Ghi đủ cọc {money(b.depositAmount)} → đặt phòng tự chuyển "Đã xác nhận".</p>}
        {!!p.error && (
          <div className="sm:col-span-2">
            <Alert>{errorMessage(p.error)}</Alert>
          </div>
        )}
      </form>
    </Dialog>
  )
}

function RescheduleDialog({ b, ...p }: DialogProps<{ roomId: number; checkIn: string; checkOut: string; guests: number; keepPrice: boolean; reason: string | null }> & { b: Booking }) {
  const homestay = useAdminHomestay(b.homestayId)
  const [roomId, setRoomId] = useState(b.roomId)
  const [checkIn, setCheckIn] = useState(b.checkIn)
  const [checkOut, setCheckOut] = useState(b.checkOut)
  const [guests, setGuests] = useState(b.guests)
  const [keepPrice, setKeepPrice] = useState(false)
  const [reason, setReason] = useState('')
  const [local, setLocal] = useState<string | null>(null)
  const datesError = tripError(checkIn, checkOut)
  const room = homestay.data?.rooms.find((r) => r.id === roomId)
  const quote = useQuote(!keepPrice && !datesError && room && guests <= room.maxGuests ? { roomId, checkIn, checkOut, guests } : null)

  return (
    <Dialog open onClose={p.onClose} title={`Đổi ngày / phòng ${b.code}`} footer={<DialogFooter onClose={p.onClose} pending={p.pending} label="Lưu thay đổi" />}>
      <form
        id="admin-dialog-form"
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(e) => {
          e.preventDefault()
          if (datesError) return setLocal(datesError)
          if (keepPrice && !reason.trim()) return setLocal('Giữ giá cũ cần ghi lý do')
          p.onSubmit({ roomId, checkIn, checkOut, guests, keepPrice, reason: reason.trim() || null })
        }}
      >
        <SelectField label="Phòng" value={roomId} onChange={(e) => setRoomId(Number(e.target.value))}>
          {(homestay.data?.rooms ?? [{ id: b.roomId, name: b.roomName, maxGuests: b.guests, status: 'active' as const }]).map((r) => (
            <option key={r.id} value={r.id} disabled={r.status !== 'active'}>
              {r.name} · tối đa {r.maxGuests} khách
            </option>
          ))}
        </SelectField>
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
        <label className="flex items-start gap-2 text-sm">
          <input type="checkbox" className="mt-0.5 size-4 accent-brand-700" checked={keepPrice} onChange={(e) => setKeepPrice(e.target.checked)} />
          Giữ nguyên giá đã chốt ({money(b.total)}) – chỉ khi cùng số đêm ({b.nights}), cần lý do
        </label>
        <TextAreaField label={keepPrice ? 'Lý do' : 'Lý do (không bắt buộc)'} maxLength={300} value={reason} onChange={(e) => setReason(e.target.value)} />
        {!keepPrice && quote.data && (
          <div className="rounded-xl bg-slate-50 p-3 dark:bg-slate-800/60">
            <p className="mb-2 text-sm font-medium">
              Giá mới · {stayRange(checkIn, checkOut)}
            </p>
            <QuoteSummary quote={quote.data} />
          </div>
        )}
        {local && <Alert>{local}</Alert>}
        {!!p.error && <Alert>{errorMessage(p.error)}</Alert>}
      </form>
    </Dialog>
  )
}

function EditGuestDialog({ b, ...p }: DialogProps<{ guestName: string; guestPhone: string; guestEmail: string | null; guestNote: string | null; internalNote: string | null }> & { b: Booking }) {
  const [v, setV] = useState({ guestName: b.guestName, guestPhone: b.guestPhone, guestEmail: b.guestEmail ?? '', guestNote: b.guestNote ?? '', internalNote: b.internalNote ?? '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const set = (k: keyof typeof v) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value })
  return (
    <Dialog open onClose={p.onClose} title="Sửa thông tin khách" footer={<DialogFooter onClose={p.onClose} pending={p.pending} label="Lưu" />}>
      <form
        id="admin-dialog-form"
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(e) => {
          e.preventDefault()
          const next: Record<string, string> = {}
          if (!v.guestName.trim()) next.guestName = 'Vui lòng nhập họ tên khách'
          if (!isPhone(v.guestPhone)) next.guestPhone = 'Số điện thoại không hợp lệ (VD: 0912 345 678)'
          if (v.guestEmail.trim() && !isEmailish(v.guestEmail)) next.guestEmail = 'Email không hợp lệ'
          setErrors(next)
          if (Object.keys(next).length > 0) return
          p.onSubmit({
            guestName: v.guestName.trim(),
            guestPhone: normalizePhone(v.guestPhone),
            guestEmail: v.guestEmail.trim() || null,
            guestNote: v.guestNote.trim() || null,
            internalNote: v.internalNote.trim() || null,
          })
        }}
      >
        <TextField label="Họ tên khách" value={v.guestName} maxLength={100} error={errors.guestName} onChange={set('guestName')} />
        <TextField label="Số điện thoại" type="tel" value={v.guestPhone} error={errors.guestPhone} onChange={set('guestPhone')} />
        <TextField label="Email" type="email" value={v.guestEmail} error={errors.guestEmail} onChange={set('guestEmail')} />
        <TextAreaField label="Ghi chú của khách" maxLength={500} value={v.guestNote} onChange={set('guestNote')} />
        <TextAreaField label="Ghi chú nội bộ" maxLength={500} value={v.internalNote} onChange={set('internalNote')} />
        {!!p.error && <Alert>{errorMessage(p.error)}</Alert>}
      </form>
    </Dialog>
  )
}
