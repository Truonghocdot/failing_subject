import { ChevronLeft, ChevronRight, Eye, EyeOff, MessageSquareText } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { SelectField } from '../../components/Field'
import { Alert, EmptyState, LoadingBlock } from '../../components/Feedback'
import { Stars } from '../../components/Stars'
import { errorMessage, query } from '../../lib/api'
import { dateTime } from '../../lib/format'
import { useAdminReviews, useSetReviewHidden } from './api'
import { HomestaySelect, PageHeader } from './shared'

/** /admin/reviews?homestayId&hidden&page – ẩn đánh giá spam / hiện lại (không sửa nội dung). */
export function ReviewsPage() {
  const [params, setParams] = useSearchParams()
  const homestayId = Number(params.get('homestayId')) || null
  const hidden = params.get('hidden') === 'true' ? true : params.get('hidden') === 'false' ? false : null
  const page = Math.max(1, Number.parseInt(params.get('page') ?? '', 10) || 1)
  const reviews = useAdminReviews(homestayId, hidden, page)
  const toggle = useSetReviewHidden()
  const update = (patch: { homestayId?: number | null; hidden?: boolean | null; page?: number }) => {
    const next = { homestayId, hidden, page: 1, ...patch }
    setParams(new URLSearchParams(query({ homestayId: next.homestayId, hidden: next.hidden, page: next.page > 1 ? next.page : null })))
  }
  const totalPages = reviews.data ? Math.max(1, Math.ceil(reviews.data.total / reviews.data.pageSize)) : 1

  return (
    <div>
      <PageHeader title="Đánh giá" description="Ẩn đánh giá spam / xúc phạm – đánh giá ẩn không tính vào điểm trung bình." />
      <div className="mb-4 grid gap-3 sm:grid-cols-2 lg:max-w-2xl">
        <HomestaySelect value={homestayId} onChange={(id) => update({ homestayId: id })} />
        <SelectField label="Trạng thái" value={hidden === null ? '' : String(hidden)} onChange={(e) => update({ hidden: e.target.value === '' ? null : e.target.value === 'true' })}>
          <option value="">Tất cả</option>
          <option value="false">Đang hiện</option>
          <option value="true">Đang ẩn</option>
        </SelectField>
      </div>
      {toggle.isError && (
        <div className="mb-4">
          <Alert>{errorMessage(toggle.error)}</Alert>
        </div>
      )}
      {reviews.isPending ? (
        <LoadingBlock />
      ) : reviews.isError ? (
        <Alert>{errorMessage(reviews.error)}</Alert>
      ) : reviews.data.items.length === 0 ? (
        <EmptyState icon={MessageSquareText} title="Chưa có đánh giá" />
      ) : (
        <>
          <ul className="flex flex-col divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
            {reviews.data.items.map((r) => (
              <li key={r.id} className={`flex flex-wrap items-start justify-between gap-3 px-4 py-3 ${r.isHidden ? 'bg-slate-50 dark:bg-slate-950/40' : ''}`}>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                    <Stars value={r.rating} />
                    <span className="font-medium">{r.guestName}</span>
                    {r.isHidden && <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-800 dark:bg-slate-700 dark:text-slate-100">Đang ẩn</span>}
                  </div>
                  {r.comment ? <p className="mt-1 text-sm whitespace-pre-line">{r.comment}</p> : <p className="mt-1 text-sm text-slate-500 italic">(không có nhận xét)</p>}
                  <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
                    {r.homestayName} ·{' '}
                    <Link to={`/admin/bookings/${r.bookingId}`} className="font-mono hover:underline">
                      {r.bookingCode}
                    </Link>{' '}
                    · {dateTime(r.createdAt)}
                  </p>
                </div>
                <Button
                  variant="secondary"
                  size="sm"
                  loading={toggle.isPending && toggle.variables?.id === r.id}
                  onClick={() => toggle.mutate({ id: r.id, isHidden: !r.isHidden })}
                >
                  {r.isHidden ? <Eye className="size-4" aria-hidden /> : <EyeOff className="size-4" aria-hidden />}
                  {r.isHidden ? 'Hiện lại' : 'Ẩn'}
                </Button>
              </li>
            ))}
          </ul>
          {totalPages > 1 && (
            <nav aria-label="Phân trang" className="mt-4 flex items-center justify-center gap-3">
              <Button variant="secondary" size="sm" disabled={page <= 1} onClick={() => update({ page: page - 1 })}>
                <ChevronLeft className="size-4" aria-hidden />
                Trước
              </Button>
              <span className="text-sm">
                Trang {page} / {totalPages}
              </span>
              <Button variant="secondary" size="sm" disabled={page >= totalPages} onClick={() => update({ page: page + 1 })}>
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
