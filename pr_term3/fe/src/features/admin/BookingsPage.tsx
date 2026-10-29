import { CalendarPlus, ChevronLeft, ChevronRight, SearchX } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { inputClass, SelectField, TextField } from '../../components/Field'
import { Alert, EmptyState, LoadingBlock } from '../../components/Feedback'
import { StatusBadge } from '../../components/StatusBadge'
import { errorMessage } from '../../lib/api'
import { dayLabel, isIsoDate, money, sourceLabels } from '../../lib/format'
import type { BookingSource, BookingStatus } from '../../lib/types'
import { adminBookingsQuery, useAdminBookings, type AdminBookingFilters } from './api'
import { BookingRow, HomestaySelect, linkButton, PageHeader, PaymentLabel } from './shared'

const statusGroups: { value: string; label: string; statuses: BookingStatus[] }[] = [
  { value: '', label: 'Mọi trạng thái', statuses: [] },
  { value: 'active', label: 'Còn hiệu lực (chờ cọc, đã xác nhận, đang ở)', statuses: ['pending', 'confirmed', 'checked_in'] },
  { value: 'pending', label: 'Chờ cọc', statuses: ['pending'] },
  { value: 'confirmed', label: 'Đã xác nhận', statuses: ['confirmed'] },
  { value: 'checked_in', label: 'Đang ở', statuses: ['checked_in'] },
  { value: 'checked_out', label: 'Đã trả phòng', statuses: ['checked_out'] },
  { value: 'closed', label: 'Huỷ / hết hạn / không đến', statuses: ['cancelled', 'expired', 'no_show'] },
]

const SOURCES = Object.keys(sourceLabels) as BookingSource[]

function readFilters(p: URLSearchParams): AdminBookingFilters {
  const status = (p.get('status') ?? '').split(',').filter(Boolean) as BookingStatus[]
  const source = p.get('source') as BookingSource | null
  return {
    status,
    homestayId: Number(p.get('homestayId')) || null,
    from: isIsoDate(p.get('from')) ? p.get('from') : null,
    to: isIsoDate(p.get('to')) ? p.get('to') : null,
    source: source && SOURCES.includes(source) ? source : null,
    q: p.get('q')?.trim() || null,
    page: Math.max(1, Number.parseInt(p.get('page') ?? '', 10) || 1),
  }
}

/** /admin/bookings – bộ lọc trên URL (trang Khách dẫn sang ?q=<SĐT> để xem lịch sử lưu trú). */
export function BookingsPage() {
  const [params, setParams] = useSearchParams()
  const filters = readFilters(params)
  const list = useAdminBookings(filters)
  const [q, setQ] = useState(filters.q ?? '')

  const update = (patch: Partial<AdminBookingFilters>) => setParams(new URLSearchParams(adminBookingsQuery({ ...filters, page: 1, ...patch })))
  const group = statusGroups.find((g) => g.statuses.join(',') === filters.status.join(','))?.value ?? ''
  const totalPages = list.data ? Math.max(1, Math.ceil(list.data.total / list.data.pageSize)) : 1

  return (
    <div>
      <PageHeader
        title="Đặt phòng"
        description={list.data && `${list.data.total} đặt phòng`}
        actions={
          <Link to="/admin/bookings/new" className={linkButton}>
            <CalendarPlus className="size-4" aria-hidden />
            Tạo đặt phòng
          </Link>
        }
      />

      <div className="mb-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr_0.8fr_0.8fr_0.8fr]">
        <form
          role="search"
          onSubmit={(e) => {
            e.preventDefault()
            update({ q: q.trim() || null })
          }}
        >
          <TextField label="Tìm mã / tên / SĐT" type="search" value={q} maxLength={100} onChange={(e) => setQ(e.target.value)} placeholder="HS-… hoặc 0912…" />
        </form>
        <SelectField label="Trạng thái" value={group} onChange={(e) => update({ status: statusGroups.find((g) => g.value === e.target.value)!.statuses })}>
          {statusGroups.map((g) => (
            <option key={g.value} value={g.value}>
              {g.label}
            </option>
          ))}
        </SelectField>
        <HomestaySelect value={filters.homestayId} onChange={(homestayId) => update({ homestayId })} />
        <SelectField label="Nguồn" value={filters.source ?? ''} onChange={(e) => update({ source: (e.target.value || null) as BookingSource | null })}>
          <option value="">Mọi nguồn</option>
          {SOURCES.map((s) => (
            <option key={s} value={s}>
              {sourceLabels[s]}
            </option>
          ))}
        </SelectField>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">
          Ở từ ngày
          <input type="date" className={inputClass} value={filters.from ?? ''} onChange={(e) => update({ from: e.target.value || null })} />
        </label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">
          đến ngày
          <input type="date" className={inputClass} value={filters.to ?? ''} onChange={(e) => update({ to: e.target.value || null })} />
        </label>
      </div>

      {list.isPending ? (
        <LoadingBlock />
      ) : list.isError ? (
        <Alert>{errorMessage(list.error)}</Alert>
      ) : list.data.items.length === 0 ? (
        <EmptyState icon={SearchX} title="Không có đặt phòng phù hợp" />
      ) : (
        <div aria-busy={list.isPlaceholderData}>
          {/* Điện thoại: thẻ; máy tính: bảng */}
          <ul className="flex flex-col divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white md:hidden dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
            {list.data.items.map((b) => (
              <BookingRow key={b.id} b={b} extra={<span className="font-mono text-xs text-slate-500 dark:text-slate-400">{b.code}</span>} />
            ))}
          </ul>
          <div className="hidden overflow-x-auto rounded-2xl border border-slate-200 bg-white md:block dark:border-slate-800 dark:bg-slate-900">
            <table className="w-full text-sm">
              <thead className="border-b border-slate-200 text-left text-xs text-slate-500 dark:border-slate-800 dark:text-slate-400">
                <tr>
                  <th className="px-3 py-2 font-medium">Mã</th>
                  <th className="px-3 py-2 font-medium">Khách</th>
                  <th className="px-3 py-2 font-medium">Phòng</th>
                  <th className="px-3 py-2 font-medium">Ngày ở</th>
                  <th className="px-3 py-2 font-medium">Trạng thái</th>
                  <th className="px-3 py-2 text-right font-medium">Tổng / còn thu</th>
                  <th className="px-3 py-2 font-medium">Nguồn</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {list.data.items.map((b) => (
                  <tr key={b.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/50">
                    <td className="px-3 py-2">
                      <Link to={`/admin/bookings/${b.id}`} className="font-mono font-medium text-brand-700 hover:underline dark:text-brand-100">
                        {b.code}
                      </Link>
                    </td>
                    <td className="px-3 py-2">
                      {b.guestName}
                      <span className="block text-xs text-slate-500 dark:text-slate-400">{b.guestPhone}</span>
                    </td>
                    <td className="px-3 py-2">
                      {b.roomName}
                      <span className="block text-xs text-slate-500 dark:text-slate-400">{b.homestayName}</span>
                    </td>
                    <td className="px-3 py-2 whitespace-nowrap">
                      {dayLabel(b.checkIn)} → {dayLabel(b.checkOut)}
                      <span className="block text-xs text-slate-500 dark:text-slate-400">
                        {b.nights} đêm · {b.guests} khách
                      </span>
                    </td>
                    <td className="px-3 py-2">
                      <StatusBadge status={b.status} />
                      <span className="mt-1 block">
                        <PaymentLabel status={b.money.status} />
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right whitespace-nowrap tabular-nums">
                      {money(b.total)}
                      {b.money.amountDue > 0 && <span className="block text-xs text-amber-800 dark:text-amber-300">còn {money(b.money.amountDue)}</span>}
                    </td>
                    <td className="px-3 py-2 text-xs">{sourceLabels[b.source]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalPages > 1 && (
            <nav aria-label="Phân trang" className="mt-4 flex items-center justify-center gap-3">
              <Button variant="secondary" size="sm" disabled={filters.page <= 1} onClick={() => update({ page: filters.page - 1 })}>
                <ChevronLeft className="size-4" aria-hidden />
                Trước
              </Button>
              <span className="text-sm">
                Trang {filters.page} / {totalPages}
              </span>
              <Button variant="secondary" size="sm" disabled={filters.page >= totalPages} onClick={() => update({ page: filters.page + 1 })}>
                Sau
                <ChevronRight className="size-4" aria-hidden />
              </Button>
            </nav>
          )}
        </div>
      )}
    </div>
  )
}
