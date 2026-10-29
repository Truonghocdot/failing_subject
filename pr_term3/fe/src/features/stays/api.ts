import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, query } from '../../lib/api'
import { addDays, isIsoDate, nightsBetween, today } from '../../lib/format'
import type { Paged, Province, Quote, StayCalendar, StayCard, StayDetail, StaySort } from '../../lib/types'

export const MAX_GUESTS = 20
export const MAX_NIGHTS = 30
export const MAX_DAYS_AHEAD = 365

/** Ngày + số khách đọc từ URL (?checkIn&checkOut&guests) – sai / quá khứ thì bỏ, không gửi lên để khỏi 400. */
export type Trip = { checkIn: string | null; checkOut: string | null; guests: number }

export function readTrip(params: URLSearchParams): Trip {
  const guests = Math.min(MAX_GUESTS, Math.max(1, Number.parseInt(params.get('guests') ?? '', 10) || 2))
  const checkIn = params.get('checkIn')
  const checkOut = params.get('checkOut')
  const ok =
    isIsoDate(checkIn) &&
    isIsoDate(checkOut) &&
    checkIn >= today() &&
    checkIn <= addDays(today(), MAX_DAYS_AHEAD) &&
    nightsBetween(checkIn, checkOut) >= 1 &&
    nightsBetween(checkIn, checkOut) <= MAX_NIGHTS
  return ok ? { checkIn, checkOut, guests } : { checkIn: null, checkOut: null, guests }
}

/** Lỗi khoảng ngày (khớp RequestValidator.StayDates) – null nếu hợp lệ. */
export function tripError(checkIn: string, checkOut: string): string | null {
  if (!checkIn && !checkOut) return null
  if (!checkIn) return 'Vui lòng chọn ngày nhận phòng'
  if (!checkOut) return 'Vui lòng chọn ngày trả phòng'
  if (checkIn < today()) return 'Ngày nhận phòng không được ở quá khứ'
  if (checkIn > addDays(today(), MAX_DAYS_AHEAD)) return `Chỉ nhận đặt phòng trong ${MAX_DAYS_AHEAD} ngày tới`
  if (checkOut <= checkIn) return 'Ngày trả phòng phải sau ngày nhận phòng'
  if (nightsBetween(checkIn, checkOut) > MAX_NIGHTS) return `Mỗi lần đặt tối đa ${MAX_NIGHTS} đêm`
  return null
}

export type StayFilters = Trip & {
  province: string | null
  q: string | null
  amenities: string[]
  priceMax: number | null
  sort: StaySort
  page: number
}

const SORTS: StaySort[] = ['relevance', 'price', 'rating']

export function readFilters(params: URLSearchParams): StayFilters {
  const sort = params.get('sort') as StaySort | null
  const priceMax = Number(params.get('priceMax'))
  return {
    ...readTrip(params),
    province: params.get('province') || null,
    q: params.get('q')?.trim() || null,
    amenities: (params.get('amenities') ?? '').split(',').filter(Boolean),
    priceMax: Number.isFinite(priceMax) && priceMax > 0 ? priceMax : null,
    sort: sort && SORTS.includes(sort) ? sort : 'relevance',
    page: Math.max(1, Number.parseInt(params.get('page') ?? '', 10) || 1),
  }
}

/** Query string cho cả URL trang lẫn API (cùng tên tham số). */
export function filtersQuery(f: Partial<StayFilters>) {
  return query({
    province: f.province,
    q: f.q,
    checkIn: f.checkIn,
    checkOut: f.checkOut,
    guests: f.guests,
    amenities: f.amenities?.length ? f.amenities.join(',') : null,
    priceMax: f.priceMax,
    sort: f.sort === 'relevance' ? null : f.sort,
    page: f.page && f.page > 1 ? f.page : null,
  })
}

export const tripQuery = (t: Trip, extra: Record<string, string | number | null> = {}) =>
  query({ checkIn: t.checkIn, checkOut: t.checkOut, guests: t.guests, ...extra })

export function useProvinces() {
  return useQuery({ queryKey: ['provinces'], queryFn: ({ signal }) => api<Province[]>('/provinces', { signal }), staleTime: 5 * 60_000 })
}

export function useStays(filters: StayFilters) {
  return useQuery({
    queryKey: ['stays', filters],
    queryFn: ({ signal }) => api<Paged<StayCard>>(`/stays${filtersQuery(filters)}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useStay(slug: string) {
  return useQuery({ queryKey: ['stay', slug], queryFn: ({ signal }) => api<StayDetail>(`/stays/${encodeURIComponent(slug)}`, { signal }) })
}

export function useStayCalendar(slug: string, from: string, to: string) {
  return useQuery({
    queryKey: ['calendar', slug, from, to],
    queryFn: ({ signal }) => api<StayCalendar>(`/stays/${encodeURIComponent(slug)}/calendar${query({ from, to })}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export type QuoteInput = { roomId: number; checkIn: string; checkOut: string; guests: number }

export function useQuote(input: QuoteInput | null) {
  return useQuery({
    queryKey: ['quote', input],
    queryFn: ({ signal }) => api<Quote>('/quotes', { method: 'POST', body: input, signal, auth: false }),
    enabled: input !== null,
    staleTime: 0,
  })
}
