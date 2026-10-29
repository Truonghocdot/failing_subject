import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api, query } from '../../lib/api'
import type {
  AdminCalendar,
  AdminHomestay,
  AdminHomestaySummary,
  AdminReview,
  AdminRoom,
  Block,
  Booking,
  BookingSource,
  BookingStatus,
  BookingSummary,
  CancelPreview,
  Dashboard,
  Guest,
  Paged,
  PaymentKind,
  PaymentMethod,
  Photo,
  Report,
  RoomRates,
  Staff,
  UserRole,
} from '../../lib/types'

// --- Làm mới cache (docs/04-frontend-react.md §4) ---

/** Mọi thao tác trên đặt phòng / khoản thu / khoá phòng → lịch, hôm nay, danh sách, khách + phía khách (phòng trống, báo giá). */
export function invalidateBookings(qc: QueryClient) {
  for (const key of [['admin', 'bookings'], ['admin', 'booking'], ['admin', 'calendar'], ['admin', 'dashboard'], ['admin', 'guests'], ['admin', 'blocks'], ['calendar'], ['quote'], ['stays']])
    void qc.invalidateQueries({ queryKey: key })
}

/** Sửa homestay / phòng / giá → kéo theo lịch phòng, báo giá, trang khách. */
export function invalidateCatalog(qc: QueryClient) {
  for (const key of [['admin', 'homestays'], ['admin', 'homestay'], ['admin', 'rates'], ['admin', 'calendar'], ['stays'], ['stay'], ['calendar'], ['quote'], ['provinces']])
    void qc.invalidateQueries({ queryKey: key })
}

// --- Hôm nay, lịch phòng, khoá phòng ---

export function useDashboard(date: string) {
  return useQuery({ queryKey: ['admin', 'dashboard', date], queryFn: ({ signal }) => api<Dashboard>(`/admin/dashboard${query({ date })}`, { signal }), refetchInterval: 60_000 })
}

export function useAdminCalendar(homestayId: number | null, from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'calendar', homestayId, from, to],
    queryFn: ({ signal }) => api<AdminCalendar>(`/admin/calendar${query({ homestayId, from, to })}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export type BlockInput = { roomId: number; from: string; to: string; reason: string }

export function useCreateBlock() {
  const qc = useQueryClient()
  return useMutation({ mutationFn: (b: BlockInput) => api<Block>('/admin/blocks', { method: 'POST', body: b }), onSuccess: () => invalidateBookings(qc) })
}

export function useDeleteBlock() {
  const qc = useQueryClient()
  return useMutation({ mutationFn: (id: number) => api<void>(`/admin/blocks/${id}`, { method: 'DELETE' }), onSuccess: () => invalidateBookings(qc) })
}

// --- Đặt phòng ---

export type AdminBookingFilters = {
  status: BookingStatus[]
  homestayId: number | null
  from: string | null
  to: string | null
  source: BookingSource | null
  q: string | null
  page: number
}

export const adminBookingsQuery = (f: Partial<AdminBookingFilters>) =>
  query({
    status: f.status?.length ? f.status.join(',') : null,
    homestayId: f.homestayId,
    from: f.from,
    to: f.to,
    source: f.source,
    q: f.q,
    page: f.page && f.page > 1 ? f.page : null,
  })

export function useAdminBookings(f: AdminBookingFilters) {
  return useQuery({
    queryKey: ['admin', 'bookings', f],
    queryFn: ({ signal }) => api<Paged<BookingSummary>>(`/admin/bookings${adminBookingsQuery(f)}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useAdminBooking(id: number) {
  return useQuery({ queryKey: ['admin', 'booking', id], queryFn: ({ signal }) => api<Booking>(`/admin/bookings/${id}`, { signal }) })
}

export type PaymentInput = { kind: PaymentKind; method: PaymentMethod; amount: number; paidAt?: string | null; note?: string | null }

export type AdminCreateInput = {
  roomId: number
  checkIn: string
  checkOut: string
  guests: number
  guestName: string
  guestPhone: string
  guestEmail: string | null
  note: string | null
  internalNote: string | null
  source: BookingSource
  payment: PaymentInput | null
}

export function useAdminCreateBooking() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: AdminCreateInput) => api<Booking>('/admin/bookings', { method: 'POST', body: input }),
    onSuccess: (b) => {
      qc.setQueryData(['admin', 'booking', b.id], b)
      invalidateBookings(qc)
    },
  })
}

/** Mọi hành động trên 1 đặt phòng đều trả BookingDto mới → ghi thẳng vào cache chi tiết rồi làm mới phần còn lại. */
export function useBookingAction(id: number) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ path, method = 'POST', body }: { path: string; method?: 'POST' | 'PUT' | 'DELETE'; body?: unknown }) =>
      api<Booking>(`/admin/bookings/${id}${path}`, { method, body }),
    onSuccess: (b) => {
      qc.setQueryData(['admin', 'booking', id], b)
      invalidateBookings(qc)
    },
  })
}

export function useAdminCancelPreview(id: number, enabled: boolean) {
  return useQuery({
    queryKey: ['admin', 'booking', id, 'cancel-preview'],
    queryFn: ({ signal }) => api<CancelPreview>(`/admin/bookings/${id}/cancel-preview`, { signal }),
    enabled,
    staleTime: 0,
  })
}

// --- Homestay, phòng, ảnh, giá ---

export function useAdminHomestays() {
  return useQuery({ queryKey: ['admin', 'homestays'], queryFn: ({ signal }) => api<AdminHomestaySummary[]>('/admin/homestays', { signal }), staleTime: 60_000 })
}

export function useAdminHomestay(id: number | null) {
  return useQuery({
    queryKey: ['admin', 'homestay', id],
    queryFn: ({ signal }) => api<AdminHomestay>(`/admin/homestays/${id}`, { signal }),
    enabled: id !== null,
  })
}

export type HomestayInput = Omit<AdminHomestay, 'id' | 'photos' | 'rooms' | 'createdAt' | 'updatedAt'>
export type RoomInput = Omit<AdminRoom, 'id' | 'homestayId'>

export function useCatalogMutation<TInput, TResult>(fn: (input: TInput) => Promise<TResult>) {
  const qc = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => invalidateCatalog(qc) })
}

export const catalogApi = {
  createHomestay: (h: HomestayInput) => api<AdminHomestay>('/admin/homestays', { method: 'POST', body: h }),
  updateHomestay: ({ id, ...h }: HomestayInput & { id: number }) => api<AdminHomestay>(`/admin/homestays/${id}`, { method: 'PUT', body: h }),
  deleteHomestay: (id: number) => api<void>(`/admin/homestays/${id}`, { method: 'DELETE' }),
  uploadPhoto: ({ id, file }: { id: number; file: File }) => {
    const form = new FormData()
    form.append('file', file)
    return api<Photo>(`/admin/homestays/${id}/photos`, { method: 'POST', form })
  },
  reorderPhotos: ({ id, photoIds }: { id: number; photoIds: number[] }) => api<Photo[]>(`/admin/homestays/${id}/photos/order`, { method: 'PUT', body: { photoIds } }),
  deletePhoto: ({ id, photoId }: { id: number; photoId: number }) => api<void>(`/admin/homestays/${id}/photos/${photoId}`, { method: 'DELETE' }),
  createRoom: ({ homestayId, ...r }: RoomInput & { homestayId: number }) => api<AdminRoom>(`/admin/homestays/${homestayId}/rooms`, { method: 'POST', body: r }),
  updateRoom: ({ id, ...r }: RoomInput & { id: number }) => api<AdminRoom>(`/admin/rooms/${id}`, { method: 'PUT', body: r }),
  deleteRoom: (id: number) => api<void>(`/admin/rooms/${id}`, { method: 'DELETE' }),
  setRates: (r: { roomIds: number[]; from: string; to: string; price: number; note: string | null }) => api<{ count: number }>('/admin/rates', { method: 'PUT', body: r }),
  clearRates: (r: { roomIds: number[]; from: string; to: string }) =>
    api<{ count: number }>(`/admin/rates${query({ roomIds: r.roomIds.join(','), from: r.from, to: r.to })}`, { method: 'DELETE' }),
}

export function useRates(roomIds: number[], from: string, to: string) {
  return useQuery({
    queryKey: ['admin', 'rates', roomIds, from, to],
    queryFn: ({ signal }) => api<RoomRates[]>(`/admin/rates${query({ roomIds: roomIds.join(','), from, to })}`, { signal }),
    enabled: roomIds.length > 0,
    placeholderData: keepPreviousData,
  })
}

// --- Nhân viên, khách ---

export function useStaff(q: string) {
  return useQuery({ queryKey: ['admin', 'users', q], queryFn: ({ signal }) => api<Staff[]>(`/admin/users${query({ q })}`, { signal }), placeholderData: keepPreviousData })
}

export type StaffInput = { fullName: string; phone: string | null; role: Exclude<UserRole, 'guest'> }

export function useStaffMutation<TInput, TResult>(fn: (input: TInput) => Promise<TResult>) {
  const qc = useQueryClient()
  return useMutation({ mutationFn: fn, onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'users'] }) })
}

export const staffApi = {
  create: (s: StaffInput & { email: string; password: string }) => api<Staff>('/admin/users', { method: 'POST', body: s }),
  update: ({ id, ...s }: StaffInput & { id: string; isActive: boolean }) => api<Staff>(`/admin/users/${id}`, { method: 'PUT', body: s }),
  resetPassword: ({ id, password }: { id: string; password: string }) => api<void>(`/admin/users/${id}/reset-password`, { method: 'POST', body: { password } }),
}

export function useGuests(q: string, page: number) {
  return useQuery({
    queryKey: ['admin', 'guests', q, page],
    queryFn: ({ signal }) => api<Paged<Guest>>(`/admin/guests${query({ q, page: page > 1 ? page : null })}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

// --- Báo cáo, đánh giá (chỉ admin) ---

export function useReport(from: string, to: string, homestayId: number | null) {
  return useQuery({
    queryKey: ['admin', 'report', from, to, homestayId],
    queryFn: ({ signal }) => api<Report>(`/admin/reports${query({ from, to, homestayId })}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useAdminReviews(homestayId: number | null, hidden: boolean | null, page: number) {
  return useQuery({
    queryKey: ['admin', 'reviews', homestayId, hidden, page],
    queryFn: ({ signal }) => api<Paged<AdminReview>>(`/admin/reviews${query({ homestayId, hidden, page: page > 1 ? page : null })}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useSetReviewHidden() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isHidden }: { id: number; isHidden: boolean }) => api<void>(`/admin/reviews/${id}`, { method: 'PUT', body: { isHidden } }),
    onSuccess: () => {
      for (const key of [['admin', 'reviews'], ['reviews'], ['stay'], ['stays']]) void qc.invalidateQueries({ queryKey: key })
    },
  })
}
