import dayjs from 'dayjs'
import 'dayjs/locale/vi'
import type { TransactionKind } from './types'

dayjs.locale('vi')

const moneyFormatters = new Map<string, Intl.NumberFormat>()

/** 45000 → "45.000 ₫" (vi-VN). Mã tiền tệ lạ → chỉ định dạng số + mã. */
export function formatMoney(amount: number, currency = 'VND'): string {
  let f = moneyFormatters.get(currency)
  if (!f) {
    try {
      f = new Intl.NumberFormat('vi-VN', { style: 'currency', currency })
    } catch {
      const n = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 })
      f = { format: (v: number) => `${n.format(v)} ${currency}` } as Intl.NumberFormat
    }
    moneyFormatters.set(currency, f)
  }
  return f.format(amount).replace(/ /g, ' ')
}

/** Có dấu theo loại giao dịch: chi "−45.000 ₫", thu "+45.000 ₫", chuyển không dấu. */
export function formatSignedMoney(amount: number, kind: TransactionKind, currency?: string): string {
  const text = formatMoney(Math.abs(amount), currency)
  return kind === 'expense' ? `−${text}` : kind === 'income' ? `+${text}` : text
}

const numberFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 })

/** Hiển thị trong ô nhập tiền: 1234567.5 → "1.234.567,5". */
export function formatMoneyInput(value: number | null | undefined): string {
  return value == null || Number.isNaN(value) ? '' : numberFormat.format(value)
}

/**
 * Chuỗi người dùng gõ → số. Kiểu Việt Nam: "." phân cách nghìn, "," thập phân.
 * "1.234.567" → 1234567, "12,5" → 12.5, "" → null, chữ → null.
 */
export function parseMoneyInput(text: string): number | null {
  const cleaned = text.replace(/[\s.₫đ]/gi, '').replace(',', '.')
  if (cleaned === '' || cleaned === '-') return null
  if (!/^-?\d+(\.\d{0,2})?$/.test(cleaned)) return null
  return Number(cleaned)
}

export const ISO_DATE = 'YYYY-MM-DD'

export function today(): string {
  return dayjs().format(ISO_DATE)
}

/** "2026-10-28" → "28/10/2026" */
export function formatDate(iso: string): string {
  return dayjs(iso).format('DD/MM/YYYY')
}

/** "2026-10-28" → "Thứ tư, 28/10/2026"; hôm nay / hôm qua có nhãn riêng. */
export function formatDayHeading(iso: string): string {
  const d = dayjs(iso)
  const now = dayjs()
  if (d.isSame(now, 'day')) return `Hôm nay, ${d.format('DD/MM/YYYY')}`
  if (d.isSame(now.subtract(1, 'day'), 'day')) return `Hôm qua, ${d.format('DD/MM/YYYY')}`
  const weekday = d.format('dddd')
  return `${weekday.charAt(0).toUpperCase()}${weekday.slice(1)}, ${d.format('DD/MM/YYYY')}`
}

/** "2026-10" → { from: "2026-10-01", to: "2026-10-31" } */
export function monthRange(month: string): { from: string; to: string } {
  const start = dayjs(`${month}-01`)
  return { from: start.format(ISO_DATE), to: start.endOf('month').format(ISO_DATE) }
}

export function currentMonth(): string {
  return dayjs().format('YYYY-MM')
}

export function shiftMonth(month: string, delta: number): string {
  return dayjs(`${month}-01`).add(delta, 'month').format('YYYY-MM')
}

/** "2026-10" → "Tháng 10/2026" */
export function formatMonth(month: string): string {
  return `Tháng ${dayjs(`${month}-01`).format('MM/YYYY')}`
}

const compactFormat = new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 })

/** Nhãn trục biểu đồ: 1500000 → "1,5 Tr", 45000 → "45 N". */
export function formatCompact(amount: number): string {
  return compactFormat.format(amount).replace(/ /g, ' ')
}

/** Kỳ của báo cáo xu hướng: "2026-10" → "T10/26", "2026-10-28" → "28/10". */
export function formatPeriod(period: string): string {
  return period.length === 7 ? dayjs(`${period}-01`).format('[T]M/YY') : dayjs(period).format('DD/MM')
}

/** Kỳ đầy đủ cho tooltip / bảng: "2026-10" → "Tháng 10/2026", "2026-10-28" → "28/10/2026". */
export function formatPeriodLong(period: string): string {
  return period.length === 7 ? formatMonth(period) : formatDate(period)
}
