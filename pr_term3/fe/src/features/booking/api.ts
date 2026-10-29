import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api'
import type { Booking, BookingStatus, BookingSummary, CancelPreview } from '../../lib/types'

export type CreateBookingInput = {
  roomId: number
  checkIn: string
  checkOut: string
  guests: number
  guestName: string
  guestPhone: string
  guestEmail: string | null
  note: string | null
  /** Tổng khách đã thấy – khác giá hiện tại → 409 kèm "quote" mới. */
  expectedTotal: number
}

/** Đặt / huỷ làm đổi phòng trống → làm mới lịch, báo giá, kết quả tìm kiếm và danh sách của tôi. */
export function invalidateAvailability(qc: QueryClient) {
  void qc.invalidateQueries({ queryKey: ['my-bookings'] })
  void qc.invalidateQueries({ queryKey: ['calendar'] })
  void qc.invalidateQueries({ queryKey: ['quote'] })
  void qc.invalidateQueries({ queryKey: ['stays'] })
}

export function useCreateBooking() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateBookingInput) => api<Booking>('/bookings', { method: 'POST', body: input }),
    onSuccess: (booking) => {
      qc.setQueryData(['booking', booking.code], booking)
      invalidateAvailability(qc)
    },
  })
}

export function useMyBookings() {
  return useQuery({ queryKey: ['my-bookings'], queryFn: ({ signal }) => api<BookingSummary[]>('/bookings', { signal }) })
}

export function useBooking(code: string) {
  return useQuery({ queryKey: ['booking', code], queryFn: ({ signal }) => api<Booking>(`/bookings/${encodeURIComponent(code)}`, { signal }) })
}

export function useCancelPreview(code: string, enabled: boolean) {
  return useQuery({
    queryKey: ['booking', code, 'cancel-preview'],
    queryFn: ({ signal }) => api<CancelPreview>(`/bookings/${encodeURIComponent(code)}/cancel-preview`, { signal }),
    enabled,
    staleTime: 0,
  })
}

export function useCancelBooking(code: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (reason: string | null) => api<Booking>(`/bookings/${encodeURIComponent(code)}/cancel`, { method: 'POST', body: { reason } }),
    onSuccess: (booking) => {
      qc.setQueryData(['booking', code], booking)
      invalidateAvailability(qc)
    },
  })
}

export function lookupBooking(code: string, phone: string) {
  return api<Booking>('/bookings/lookup', { method: 'POST', body: { code, phone }, auth: false })
}

/** Tab danh sách của tôi (docs/04-frontend-react.md §2.1). */
export const bookingTabs = [
  { key: 'upcoming', label: 'Sắp tới', statuses: ['pending', 'confirmed', 'checked_in'] },
  { key: 'stayed', label: 'Đã ở', statuses: ['checked_out'] },
  { key: 'cancelled', label: 'Đã huỷ', statuses: ['cancelled', 'expired', 'no_show'] },
] as const satisfies readonly { key: string; label: string; statuses: readonly BookingStatus[] }[]

export type BookingTab = (typeof bookingTabs)[number]['key']
