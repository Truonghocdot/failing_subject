import { useInfiniteQuery } from '@tanstack/react-query'
import dayjs from 'dayjs'
import { Button } from '../../components/Button'
import { Alert } from '../../components/Feedback'
import { Stars } from '../../components/Stars'
import { api, errorMessage, query } from '../../lib/api'
import type { Paged, PublicReview, StayDetail } from '../../lib/types'

/** Đánh giá đang hiện của homestay – mới nhất trước, "Xem thêm" tải trang kế. */
export function Reviews({ stay }: { stay: StayDetail }) {
  const reviews = useInfiniteQuery({
    queryKey: ['reviews', stay.slug],
    queryFn: ({ pageParam, signal }) => api<Paged<PublicReview>>(`/stays/${encodeURIComponent(stay.slug)}/reviews${query({ page: pageParam > 1 ? pageParam : null })}`, { signal, auth: false }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page * last.pageSize < last.total ? last.page + 1 : undefined),
  })
  const items = reviews.data?.pages.flatMap((p) => p.items) ?? []

  return (
    <section aria-labelledby="reviews-title" className="flex flex-col gap-3">
      <h2 id="reviews-title" className="flex items-center gap-2 text-lg font-semibold">
        Đánh giá
        {stay.rating !== null && stay.reviewCount > 0 && (
          <span className="text-base font-normal text-slate-600 dark:text-slate-300">
            {' '}· {stay.rating.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}/5 ({stay.reviewCount})
          </span>
        )}
      </h2>
      {reviews.isError ? (
        <Alert>{errorMessage(reviews.error)}</Alert>
      ) : reviews.isPending ? null : items.length === 0 ? (
        <p className="text-sm text-slate-600 dark:text-slate-300">Chưa có đánh giá – khách đã ở có thể đánh giá sau khi trả phòng.</p>
      ) : (
        <>
          <ul className="flex flex-col divide-y divide-slate-200 dark:divide-slate-800">
            {items.map((r) => (
              <li key={r.id} className="py-3">
                <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
                  <span className="font-medium">{r.author}</span>
                  <Stars value={r.rating} />
                  <span className="text-xs text-slate-500 dark:text-slate-400">
                    {r.roomName} · ở tháng {dayjs(r.stayedAt).format('MM/YYYY')}
                  </span>
                </div>
                {r.comment && <p className="mt-1.5 text-sm whitespace-pre-line text-slate-700 dark:text-slate-200">{r.comment}</p>}
              </li>
            ))}
          </ul>
          {reviews.hasNextPage && (
            <Button variant="secondary" size="sm" className="self-start" loading={reviews.isFetchingNextPage} onClick={() => void reviews.fetchNextPage()}>
              Xem thêm đánh giá
            </Button>
          )}
        </>
      )}
    </section>
  )
}
