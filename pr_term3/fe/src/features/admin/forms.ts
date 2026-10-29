import { ApiError } from '../../lib/api'
import type { HomestayInput, RoomInput } from './api'

export const emptyHomestay: HomestayInput = {
  slug: '',
  name: '',
  province: '',
  address: '',
  description: '',
  amenities: [],
  checkInTime: '14:00',
  checkOutTime: '12:00',
  depositPercent: 30,
  holdHours: 24,
  freeCancelDays: 7,
  partialRefundDays: 3,
  partialRefundPercent: 50,
  status: 'active',
}

/** Lỗi từng ô từ ProblemDetails (khoá "name", "partialRefundDays"…). */
export function serverFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError)) return {}
  return Object.fromEntries(Object.entries(error.errors).map(([k, v]) => [k.charAt(0).toLowerCase() + k.slice(1), v[0]]))
}

export const emptyRoom: RoomInput = {
  name: '',
  description: '',
  bedInfo: null,
  areaM2: null,
  baseGuests: 2,
  maxGuests: 2,
  minNights: 1,
  basePrice: 0,
  weekendPrice: null,
  extraGuestFee: 0,
  cleaningFee: 0,
  status: 'active',
  sortOrder: 0,
}

/** Chỉ lấy các trường gửi lên PUT /homestays/{id} (bỏ ảnh, phòng, thời điểm). */
export const toHomestayInput = (h: HomestayInput): HomestayInput => ({
  slug: h.slug,
  name: h.name,
  province: h.province,
  address: h.address,
  description: h.description,
  amenities: h.amenities,
  checkInTime: h.checkInTime,
  checkOutTime: h.checkOutTime,
  depositPercent: h.depositPercent,
  holdHours: h.holdHours,
  freeCancelDays: h.freeCancelDays,
  partialRefundDays: h.partialRefundDays,
  partialRefundPercent: h.partialRefundPercent,
  status: h.status,
})
