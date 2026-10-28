import dayjs from 'dayjs'
import { ISO_DATE } from '../../lib/format'
import type { DateRange, TrendGroup } from './api'

export const rangePresets = [
  { value: 'this-month', label: 'Tháng này' },
  { value: 'last-month', label: 'Tháng trước' },
  { value: '3-months', label: '3 tháng' },
  { value: 'this-year', label: 'Năm nay' },
  { value: 'custom', label: 'Tuỳ chọn' },
] as const
export type Preset = (typeof rangePresets)[number]['value']

/** Khoảng ngày của từng lựa chọn – "3 tháng" / "năm nay" tính tới hết tháng hiện tại. */
export function presetRange(preset: Exclude<Preset, 'custom'>, now = dayjs()): DateRange {
  const fmt = (d: dayjs.Dayjs) => d.format(ISO_DATE)
  const month = now.startOf('month')
  switch (preset) {
    case 'last-month':
      return { from: fmt(month.subtract(1, 'month')), to: fmt(month.subtract(1, 'day')) }
    case '3-months':
      return { from: fmt(month.subtract(2, 'month')), to: fmt(month.endOf('month')) }
    case 'this-year':
      return { from: fmt(now.startOf('year')), to: fmt(month.endOf('month')) }
    default:
      return { from: fmt(month), to: fmt(month.endOf('month')) }
  }
}

/** Khoảng ≤ 1 tháng xem theo ngày, dài hơn xem theo tháng (theo ngày tối đa 92 ngày ở backend). */
export function trendGroupFor(range: DateRange): TrendGroup {
  return dayjs(range.to).diff(dayjs(range.from), 'day') <= 31 ? 'day' : 'month'
}
