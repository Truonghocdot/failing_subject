import { describe, expect, it } from 'vitest'
import { formatCompact, formatMoney, formatMoneyInput, formatPeriod, formatPeriodLong, formatSignedMoney, monthRange, parseMoneyInput, shiftMonth } from './format'

describe('format', () => {
  it('định dạng tiền kiểu Việt Nam', () => {
    expect(formatMoney(45000)).toBe('45.000 ₫')
    expect(formatMoney(-2_500_000)).toBe('-2.500.000 ₫')
    expect(formatMoney(12.5, 'USD')).toBe('12,50 US$')
  })

  it('có dấu theo loại giao dịch', () => {
    expect(formatSignedMoney(45000, 'expense')).toBe('−45.000 ₫')
    expect(formatSignedMoney(45000, 'income')).toBe('+45.000 ₫')
    expect(formatSignedMoney(45000, 'transfer')).toBe('45.000 ₫')
  })

  it.each([
    ['45000', 45000],
    ['1.234.567', 1234567],
    ['12,5', 12.5],
    ['12,50', 12.5],
    ['-2.500.000', -2500000],
    ['45.000 ₫', 45000],
    ['', null],
    ['abc', null],
    ['1,234', null],
  ])('parseMoneyInput(%j) = %j', (text, expected) => {
    expect(parseMoneyInput(text)).toBe(expected)
  })

  it('formatMoneyInput hiện dấu chấm nghìn', () => {
    expect(formatMoneyInput(1234567.5)).toBe('1.234.567,5')
    expect(formatMoneyInput(null)).toBe('')
  })

  it('khoảng tháng, kể cả tháng 2 năm nhuận', () => {
    expect(monthRange('2026-10')).toEqual({ from: '2026-10-01', to: '2026-10-31' })
    expect(monthRange('2028-02')).toEqual({ from: '2028-02-01', to: '2028-02-29' })
    expect(shiftMonth('2026-01', -1)).toBe('2025-12')
  })

  it('nhãn trục và kỳ của biểu đồ', () => {
    expect(formatCompact(1_500_000)).toBe('1,5 Tr')
    expect(formatCompact(45_000)).toBe('45 N')
    expect(formatPeriod('2026-10')).toBe('T10/26')
    expect(formatPeriod('2026-10-28')).toBe('28/10')
    expect(formatPeriodLong('2026-10')).toBe('Tháng 10/2026')
    expect(formatPeriodLong('2026-10-28')).toBe('28/10/2026')
  })
})
