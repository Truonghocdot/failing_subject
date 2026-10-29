import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { SelectField } from '../../components/Field'
import { StatusBadge } from '../../components/StatusBadge'
import { dayLabel, money, paymentStatusLabels } from '../../lib/format'
import type { BookingSummary, PaymentStatus } from '../../lib/types'
import { useAdminHomestays } from './api'

export function PageHeader({ title, description, actions }: { title: string; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
      <div className="min-w-0">
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        {description && <div className="mt-1 text-sm text-slate-600 dark:text-slate-300">{description}</div>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}

const paymentTones: Record<PaymentStatus, string> = {
  unpaid: 'text-amber-800 dark:text-amber-300',
  deposit_paid: 'text-sky-800 dark:text-sky-300',
  paid: 'text-emerald-800 dark:text-emerald-300',
  refunded: 'text-slate-600 dark:text-slate-400',
}

export function PaymentLabel({ status }: { status: PaymentStatus }) {
  return <span className={`text-xs font-medium ${paymentTones[status]}`}>{paymentStatusLabels[status]}</span>
}

/** Chọn homestay (lọc lịch / danh sách / tạo đặt phòng); allLabel = null → bắt buộc chọn. */
export function HomestaySelect({
  value,
  onChange,
  label = 'Homestay',
  allLabel = 'Tất cả homestay',
  error,
}: {
  value: number | null
  onChange: (id: number | null) => void
  label?: string
  allLabel?: string | null
  error?: string
}) {
  const homestays = useAdminHomestays()
  return (
    <SelectField label={label} value={value ?? ''} error={error} onChange={(e) => onChange(e.target.value ? Number(e.target.value) : null)}>
      {allLabel !== null ? <option value="">{allLabel}</option> : <option value="">Chọn homestay…</option>}
      {homestays.data?.map((h) => (
        <option key={h.id} value={h.id}>
          {h.name}
          {h.status === 'hidden' ? ' (đang ẩn)' : ''}
        </option>
      ))}
    </SelectField>
  )
}

/** 1 dòng đặt phòng gọn (bảng Hôm nay, danh sách trên điện thoại). */
export function BookingRow({ b, extra }: { b: BookingSummary; extra?: ReactNode }) {
  return (
    <li>
      <Link
        to={`/admin/bookings/${b.id}`}
        className="flex flex-col gap-1 rounded-xl px-3 py-2.5 hover:bg-slate-50 focus-visible:outline-2 focus-visible:outline-brand-600 dark:hover:bg-slate-800/60"
      >
        <div className="flex items-start justify-between gap-2">
          <span className="min-w-0 font-medium">
            {b.guestName} <span className="font-normal text-slate-500 dark:text-slate-400">· {b.guestPhone}</span>
          </span>
          <StatusBadge status={b.status} />
        </div>
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 text-sm text-slate-600 dark:text-slate-300">
          <span>
            {b.roomName} · {b.homestayName} · {dayLabel(b.checkIn)} → {dayLabel(b.checkOut)}
          </span>
          <span className="flex items-center gap-2">
            {b.money.amountDue > 0 && <span>còn {money(b.money.amountDue)}</span>}
            <PaymentLabel status={b.money.status} />
          </span>
        </div>
        {extra}
      </Link>
    </li>
  )
}

export const linkButton =
  'inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-brand-700 px-4 text-sm font-medium text-white hover:bg-brand-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600'

export const linkButtonSecondary =
  'inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800'
