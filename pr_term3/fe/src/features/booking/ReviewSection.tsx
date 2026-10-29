import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../components/Button'
import { TextAreaField } from '../../components/Field'
import { Alert, Card } from '../../components/Feedback'
import { StarInput, Stars } from '../../components/Stars'
import { api, errorMessage } from '../../lib/api'
import { dateTime } from '../../lib/format'
import type { Booking, Review } from '../../lib/types'

/** Đánh giá của khách trên trang đặt phòng: đã viết → hiện lại; được viết (trả phòng ≤ 30 ngày) → form. */
export function ReviewSection({ booking }: { booking: Booking }) {
  const qc = useQueryClient()
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const submit = useMutation({
    mutationFn: () => api<Review>(`/bookings/${encodeURIComponent(booking.code)}/review`, { method: 'POST', body: { rating, comment: comment.trim() || null } }),
    onSuccess: (review) => {
      qc.setQueryData<Booking>(['booking', booking.code], (b) => b && { ...b, review, canReview: false })
      // điểm trung bình trên trang homestay đổi theo
      void qc.invalidateQueries({ queryKey: ['stay', booking.homestaySlug] })
      void qc.invalidateQueries({ queryKey: ['reviews', booking.homestaySlug] })
    },
  })

  if (booking.review)
    return (
      <Card className="p-5">
        <h2 className="mb-2 font-semibold">Đánh giá của bạn</h2>
        <Stars value={booking.review.rating} />
        {booking.review.comment && <p className="mt-2 text-sm whitespace-pre-line">{booking.review.comment}</p>}
        <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">Gửi lúc {dateTime(booking.review.createdAt)} – cảm ơn bạn!</p>
      </Card>
    )
  if (!booking.canReview) return null

  return (
    <Card className="p-5">
      <h2 className="mb-3 font-semibold">Chuyến đi thế nào?</h2>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(e) => {
          e.preventDefault()
          if (rating === 0) return setError('Vui lòng chọn số sao')
          setError(null)
          submit.mutate()
        }}
      >
        <StarInput value={rating} onChange={setRating} error={error ?? undefined} />
        <TextAreaField label="Nhận xét (không bắt buộc)" rows={3} maxLength={1000} value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Phòng, chủ nhà, vị trí…" />
        {submit.isError && <Alert>{errorMessage(submit.error)}</Alert>}
        <Button type="submit" loading={submit.isPending} className="self-start">
          Gửi đánh giá
        </Button>
        <p className="text-xs text-slate-500 dark:text-slate-400">Đánh giá hiện công khai trên trang homestay với tên rút gọn (VD "Minh T."). Mỗi đặt phòng đánh giá 1 lần.</p>
      </form>
    </Card>
  )
}
