import { ImageOff, MapPin, Star } from 'lucide-react'
import { Link } from 'react-router'
import { amenityLabel, money } from '../../lib/format'
import type { StayCard as StayCardData } from '../../lib/types'
import { tripQuery, type Trip } from './api'

export function Rating({ rating, count }: { rating: number | null; count: number }) {
  if (rating === null || count === 0) return <span className="text-sm text-slate-500 dark:text-slate-400">Chưa có đánh giá</span>
  return (
    <span className="inline-flex items-center gap-1 text-sm">
      <Star className="size-4 fill-amber-400 text-amber-500" aria-hidden />
      <span className="font-medium">{rating.toLocaleString('vi-VN', { maximumFractionDigits: 1 })}</span>
      <span className="text-slate-500 dark:text-slate-400">({count} đánh giá)</span>
    </span>
  )
}

export function Cover({ url, alt, className = '' }: { url: string | null; alt: string; className?: string }) {
  if (!url)
    return (
      <div className={`flex items-center justify-center bg-slate-100 text-slate-400 dark:bg-slate-800 ${className}`}>
        <ImageOff className="size-8" aria-hidden />
        <span className="sr-only">Chưa có ảnh</span>
      </div>
    )
  return <img src={url} alt={alt} loading="lazy" className={`object-cover ${className}`} />
}

/** Thẻ homestay: ảnh bìa, tỉnh, điểm, tiện nghi, "giá từ / đêm" (đã chọn ngày → gồm phụ thu, phí dọn). */
export function StayCard({ stay, trip }: { stay: StayCardData; trip: Trip }) {
  const hasDates = trip.checkIn !== null
  return (
    <li className="group overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm transition-shadow hover:shadow-md dark:border-slate-800 dark:bg-slate-900">
      <Link to={`/stays/${stay.slug}${tripQuery(trip)}`} className="flex h-full flex-col focus-visible:outline-2 focus-visible:outline-brand-600">
        <Cover url={stay.coverUrl} alt={stay.name} className="aspect-[4/3] w-full" />
        <div className="flex flex-1 flex-col gap-2 p-4">
          <div className="flex items-start justify-between gap-2">
            <h3 className="font-semibold group-hover:underline">{stay.name}</h3>
          </div>
          <p className="flex items-center gap-1 text-sm text-slate-600 dark:text-slate-300">
            <MapPin className="size-4 shrink-0" aria-hidden />
            {stay.province}
          </p>
          <Rating rating={stay.rating} count={stay.reviewCount} />
          {stay.amenities.length > 0 && (
            <p className="line-clamp-1 text-xs text-slate-500 dark:text-slate-400">{stay.amenities.slice(0, 4).map(amenityLabel).join(' · ')}</p>
          )}
          <div className="mt-auto flex items-end justify-between gap-2 pt-2">
            <p>
              <span className="text-xs text-slate-500 dark:text-slate-400">Giá từ </span>
              <span className="text-lg font-semibold text-brand-700 dark:text-brand-100">{money(stay.priceFrom)}</span>
              <span className="text-xs text-slate-500 dark:text-slate-400"> / đêm</span>
            </p>
            {hasDates && stay.availableRooms !== null && (
              <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200">
                Còn {stay.availableRooms} phòng
              </span>
            )}
          </div>
        </div>
      </Link>
    </li>
  )
}
