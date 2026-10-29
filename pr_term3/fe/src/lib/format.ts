import dayjs, { type Dayjs } from 'dayjs'
import 'dayjs/locale/vi'
import type { BookingSource, BookingStatus, PaymentKind, PaymentMethod, PaymentStatus, StayPolicy } from './types'

dayjs.locale('vi')

const vnd = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 })

/** 1200000 → "1.200.000 ₫". */
export const money = (value: number) => vnd.format(value)

/** Giá gọn cho ô lịch: 800000 → "800k", 1250000 → "1,25tr". */
export function shortMoney(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 2 })}tr`
  return `${Math.round(value / 1000)}k`
}

/** 0.734 → "73,4%". */
export const percent = (ratio: number, digits = 1) => `${(ratio * 100).toLocaleString('vi-VN', { maximumFractionDigits: digits })}%`

/** Trục biểu đồ: 0 / 500k / 1,5tr / 2 tỷ. */
export function compactMoney(value: number): string {
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tỷ`
  if (value >= 1_000_000) return `${(value / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })}tr`
  if (value >= 1000) return `${Math.round(value / 1000)}k`
  return String(value)
}

/** Ngày theo giờ máy khách – "YYYY-MM-DD" như backend. */
export const isoDate = (d: Dayjs) => d.format('YYYY-MM-DD')
export const today = () => isoDate(dayjs())
export const addDays = (date: string, days: number) => isoDate(dayjs(date).add(days, 'day'))
export const nightsBetween = (checkIn: string, checkOut: string) => dayjs(checkOut).diff(dayjs(checkIn), 'day')

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/
export const isIsoDate = (value: string | null | undefined): value is string => !!value && DATE_RE.test(value) && dayjs(value).isValid()

/** "2026-11-14" → "T7, 14/11". */
export const dayLabel = (date: string) => dayjs(date).format('dd, DD/MM')
export const fullDate = (date: string) => dayjs(date).format('dd, DD/MM/YYYY')
export const dateTime = (iso: string) => dayjs(iso).format('HH:mm DD/MM/YYYY')

/** "T6, 14/11 → CN, 16/11 · 2 đêm". */
export const stayRange = (checkIn: string, checkOut: string) => `${dayLabel(checkIn)} → ${dayLabel(checkOut)} · ${nightsBetween(checkIn, checkOut)} đêm`

/** Thời gian còn lại "1:05:09" (giờ:phút:giây); ≤ 0 → null. */
export function countdown(untilIso: string, now: number): string | null {
  const ms = dayjs(untilIso).valueOf() - now
  if (ms <= 0) return null
  const s = Math.floor(ms / 1000)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${Math.floor(s / 3600)}:${pad(Math.floor((s % 3600) / 60))}:${pad(s % 60)}`
}

export const statusLabels: Record<BookingStatus, string> = {
  pending: 'Chờ cọc',
  confirmed: 'Đã xác nhận',
  checked_in: 'Đang ở',
  checked_out: 'Đã trả phòng',
  cancelled: 'Đã huỷ',
  expired: 'Hết hạn giữ chỗ',
  no_show: 'Không đến',
}

export const paymentStatusLabels: Record<PaymentStatus, string> = {
  unpaid: 'Chưa thanh toán',
  deposit_paid: 'Đã cọc',
  paid: 'Đã thanh toán đủ',
  refunded: 'Đã hoàn tiền',
}

export const sourceLabels: Record<BookingSource, string> = { web: 'Đặt online', phone: 'Điện thoại', walk_in: 'Khách vãng lai', ota: 'OTA' }

export const paymentKindLabels: Record<PaymentKind, string> = { deposit: 'Cọc', balance: 'Thu nốt', extra: 'Phát sinh', refund: 'Hoàn tiền' }

export const paymentMethodLabels: Record<PaymentMethod, string> = { cash: 'Tiền mặt', bank_transfer: 'Chuyển khoản', card: 'Thẻ', ewallet: 'Ví điện tử' }

/** Chính sách huỷ dạng câu cho khách đọc (docs/02-nghiep-vu.md §8). */
export function cancelPolicyLines(p: StayPolicy): string[] {
  const lines = [`Huỷ trước ngày nhận phòng từ ${p.freeCancelDays} ngày: hoàn 100% tiền đã trả`]
  if (p.partialRefundDays < p.freeCancelDays && p.partialRefundPercent > 0)
    lines.push(`Từ ${p.partialRefundDays} đến dưới ${p.freeCancelDays} ngày: hoàn ${p.partialRefundPercent}%`)
  lines.push(`Muộn hơn: không hoàn tiền`)
  return lines
}

export const amenityLabels: Record<string, string> = {
  wifi: 'Wi-Fi',
  parking: 'Chỗ đỗ xe',
  kitchen: 'Bếp',
  aircon: 'Điều hoà',
  hot_water: 'Nước nóng',
  washer: 'Máy giặt',
  tv: 'TV',
  pool: 'Hồ bơi',
  bbq: 'Nướng BBQ',
  garden: 'Sân vườn',
  balcony: 'Ban công',
  mountain_view: 'View núi',
  sea_view: 'View biển',
  breakfast: 'Bữa sáng',
  pet_friendly: 'Cho mang thú cưng',
  workspace: 'Góc làm việc',
}

export const amenityLabel = (code: string) => amenityLabels[code] ?? code
