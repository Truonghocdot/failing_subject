import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import { describe, expect, it } from 'vitest'
import type { CategoryReportItem, ReportSummary } from '../../lib/types'
import { renderRoute, signedInApi, type ApiRequest } from '../../test/render'
import { presetRange, trendGroupFor } from './ranges'

const summary: ReportSummary = {
  from: '2026-09-01',
  to: '2026-09-30',
  income: 15_000_000,
  expense: 300_000,
  net: 14_700_000,
  transactionCount: 4,
  dailyAverageExpense: 10_000,
}

const item = (id: number, name: string, total: number, percent: number, count = 1): CategoryReportItem => ({
  category: { id, name, icon: 'utensils', color: '#f97316' },
  total,
  percent,
  count,
})

const expenseItems = [item(10, 'Ăn uống', 200_000, 66.7, 2), item(11, 'Đi lại', 100_000, 33.3)]

function reportApi(extra = {}) {
  return signedInApi({
    'GET /api/reports/summary': [200, summary],
    'GET /api/reports/by-category': (req: ApiRequest) =>
      [200, req.query.get('kind') === 'income' ? [item(20, 'Lương', 15_000_000, 100)] : expenseItems],
    'GET /api/reports/trend': [200, [{ period: '2026-09-01', income: 0, expense: 200_000 }]],
    ...extra,
  })
}

describe('khoảng ngày báo cáo', () => {
  const now = dayjs('2026-10-28')

  it.each([
    ['this-month', '2026-10-01', '2026-10-31'],
    ['last-month', '2026-09-01', '2026-09-30'],
    ['3-months', '2026-08-01', '2026-10-31'],
    ['this-year', '2026-01-01', '2026-10-31'],
  ] as const)('%s', (preset, from, to) => {
    expect(presetRange(preset, now)).toEqual({ from, to })
  })

  it('≤ 1 tháng xem theo ngày, dài hơn theo tháng', () => {
    expect(trendGroupFor({ from: '2026-10-01', to: '2026-10-31' })).toBe('day')
    expect(trendGroupFor({ from: '2026-08-01', to: '2026-10-31' })).toBe('month')
  })
})

describe('trang Báo cáo', () => {
  it('hiện tổng quan, cơ cấu theo danh mục (biểu đồ + bảng) và gửi đúng khoảng', async () => {
    const mock = reportApi()
    renderRoute('/reports?range=custom&from=2026-09-01&to=2026-09-30')

    expect(await screen.findByText('+15.000.000 ₫')).toBeInTheDocument()
    expect(screen.getByText('14.700.000 ₫')).toBeInTheDocument()
    expect(screen.getByText('10.000 ₫')).toBeInTheDocument()
    expect(await screen.findByRole('img', { name: 'Biểu đồ tròn tổng chi theo danh mục' })).toBeInTheDocument()

    const rows = within(screen.getByRole('table', { name: 'Chi tiết chi theo danh mục' })).getAllByRole('row').slice(1)
    expect(rows.map((r) => r.textContent)).toEqual(['Ăn uống266,7%200.000 ₫', 'Đi lại133,3%100.000 ₫'])

    const sent = mock.requests.filter((r) => r.path.startsWith('/api/reports/'))
    expect(sent.every((r) => r.query.get('from') === '2026-09-01' && r.query.get('to') === '2026-09-30')).toBe(true)
    expect(sent.find((r) => r.path === '/api/reports/trend')!.query.get('groupBy')).toBe('day')
  })

  it('đổi sang khoản thu và sang "3 tháng" (xu hướng theo tháng)', async () => {
    const mock = reportApi()
    const { router } = renderRoute('/reports')
    await screen.findByText('Ăn uống', { selector: 'td span' })

    await userEvent.click(screen.getByRole('tab', { name: 'Khoản thu' }))
    expect(await screen.findByText('Lương', { selector: 'td span' })).toBeInTheDocument()
    expect(router.state.location.search).toBe('?kind=income')

    await userEvent.click(screen.getByRole('button', { name: '3 tháng' }))
    await waitFor(() => expect(mock.requests.some((r) => r.path === '/api/reports/trend' && r.query.get('groupBy') === 'month')).toBe(true))
    expect(screen.getByRole('heading', { name: 'Thu chi theo tháng' })).toBeInTheDocument()
  })

  it('khoảng không có giao dịch → hướng dẫn thay vì biểu đồ rỗng', async () => {
    reportApi({ 'GET /api/reports/summary': [200, { ...summary, income: 0, expense: 0, net: 0, transactionCount: 0, dailyAverageExpense: 0 }] })
    renderRoute('/reports?range=last-month')

    expect(await screen.findByText('Chưa có giao dịch thu chi trong khoảng này')).toBeInTheDocument()
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })

  it('khoảng tuỳ chọn sai → hiện lỗi dễ hiểu của server', async () => {
    reportApi({ 'GET /api/reports/summary': [400, { title: 'Dữ liệu không hợp lệ', errors: { to: ['Khoảng ngày tối đa 366 ngày'] } }] })
    renderRoute('/reports?range=custom&from=2024-01-01&to=2026-09-30')

    expect(await screen.findByRole('alert')).toHaveTextContent('Khoảng ngày tối đa 366 ngày')
  })
})
