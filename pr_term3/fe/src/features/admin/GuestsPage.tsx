import { ChevronLeft, ChevronRight, Users } from 'lucide-react'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { TextField } from '../../components/Field'
import { Alert, EmptyState, LoadingBlock } from '../../components/Feedback'
import { errorMessage, query } from '../../lib/api'
import { fullDate, money } from '../../lib/format'
import { useGuests } from './api'
import { PageHeader } from './shared'

/** /admin/guests?q&page – khách gộp theo SĐT; bấm → lịch sử lưu trú (danh sách đặt phòng lọc theo SĐT). */
export function GuestsPage() {
  const [params, setParams] = useSearchParams()
  const q = params.get('q') ?? ''
  const page = Math.max(1, Number.parseInt(params.get('page') ?? '', 10) || 1)
  const guests = useGuests(q, page)
  const [term, setTerm] = useState(q)
  const go = (patch: { q?: string; page?: number }) => setParams(new URLSearchParams(query({ q: patch.q ?? q, page: (patch.page ?? 1) > 1 ? patch.page : null })))
  const totalPages = guests.data ? Math.max(1, Math.ceil(guests.data.total / guests.data.pageSize)) : 1

  return (
    <div>
      <PageHeader title="Khách" description={guests.data && `${guests.data.total} khách (gộp theo số điện thoại)`} />
      <form
        role="search"
        className="mb-4 max-w-sm"
        onSubmit={(e) => {
          e.preventDefault()
          go({ q: term.trim() })
        }}
      >
        <TextField label="Tìm tên / SĐT / email" type="search" value={term} maxLength={100} onChange={(e) => setTerm(e.target.value)} />
      </form>
      {guests.isPending ? (
        <LoadingBlock />
      ) : guests.isError ? (
        <Alert>{errorMessage(guests.error)}</Alert>
      ) : guests.data.items.length === 0 ? (
        <EmptyState icon={Users} title="Không có khách phù hợp" />
      ) : (
        <>
          <ul className="grid gap-3 md:grid-cols-2">
            {guests.data.items.map((g) => (
              <li key={g.phone}>
                <Link
                  to={`/admin/bookings?q=${encodeURIComponent(g.phone)}`}
                  className="flex h-full flex-col gap-1 rounded-2xl border border-slate-200 bg-white p-4 hover:shadow-md focus-visible:outline-2 focus-visible:outline-brand-600 dark:border-slate-800 dark:bg-slate-900"
                >
                  <div className="flex items-start justify-between gap-2">
                    <span className="font-semibold">{g.name}</span>
                    {g.hasAccount && <span className="rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-800 dark:bg-brand-700/30 dark:text-brand-100">Có tài khoản</span>}
                  </div>
                  <span className="text-sm break-all text-slate-600 dark:text-slate-300">
                    {g.phone}
                    {g.email && ` · ${g.email}`}
                  </span>
                  <span className="text-sm">
                    {g.bookings} lần đặt · {g.stays} lần ở · {g.upcoming} sắp tới{g.cancelled > 0 && ` · ${g.cancelled} huỷ`}
                  </span>
                  <span className="text-xs text-slate-500 dark:text-slate-400">
                    Đã trả {money(g.paid)} · nhận phòng gần nhất {fullDate(g.lastCheckIn)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          {totalPages > 1 && (
            <nav aria-label="Phân trang" className="mt-4 flex items-center justify-center gap-3">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => go({ page: page - 1 })}>
                <ChevronLeft className="size-4" aria-hidden />
                Trước
              </Button>
              <span className="text-sm">
                Trang {page} / {totalPages}
              </span>
              <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => go({ page: page + 1 })}>
                Sau
                <ChevronRight className="size-4" aria-hidden />
              </Button>
            </nav>
          )}
        </>
      )}
    </div>
  )
}
