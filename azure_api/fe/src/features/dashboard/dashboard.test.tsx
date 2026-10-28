import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { currentMonth, formatMonth, monthRange, shiftMonth } from '../../lib/format'
import type { Budget, PagedTransactions } from '../../lib/types'
import { renderRoute, signedInApi } from '../../test/render'

const budget = (id: number, name: string, percent: number, status: Budget['status']): Budget => ({
  id,
  month: currentMonth(),
  category: { id, name, icon: 'utensils', color: '#f97316' },
  amount: 1_000_000,
  spent: percent * 10_000,
  remaining: 1_000_000 - percent * 10_000,
  percent,
  status,
})

const recent: PagedTransactions = {
  items: [
    {
      id: 9,
      kind: 'expense',
      amount: 45_000,
      occurredOn: '2026-10-28',
      note: 'Phở bò',
      wallet: { id: 1, name: 'Tiền mặt', color: '#16a34a' },
      toWallet: null,
      category: { id: 10, name: 'Ăn uống', icon: 'utensils', color: '#f97316' },
      createdAt: '2026-10-28T05:00:00Z',
    },
  ],
  page: 1,
  pageSize: 5,
  totalItems: 1,
  totalPages: 1,
  totals: { income: 0, expense: 45_000 },
}

function dashboardApi(extra = {}) {
  return signedInApi({
    'GET /api/reports/summary': [200, { from: '', to: '', income: 15_000_000, expense: 242_000, net: 14_758_000, transactionCount: 3, dailyAverageExpense: 8_642.86 }],
    'GET /api/reports/by-category': [200, [{ category: { id: 10, name: 'Ăn uống', icon: 'utensils', color: '#f97316' }, total: 242_000, percent: 100, count: 3 }]],
    'GET /api/reports/trend': [200, [{ period: currentMonth(), income: 15_000_000, expense: 242_000 }]],
    'GET /api/transactions': [200, recent],
    'GET /api/budgets': [200, [budget(1, 'Giải trí', 40, 'ok'), budget(2, 'Đi lại', 105, 'over'), budget(3, 'Ăn uống', 85.1, 'warning')]],
    ...extra,
  })
}

describe('trang Tổng quan', () => {
  it('4 thẻ số liệu tháng này + tổng số dư, ngân sách xếp theo % giảm dần, giao dịch gần nhất', async () => {
    const mock = dashboardApi()
    renderRoute('/')

    expect(await screen.findByText('+15.000.000 ₫')).toBeInTheDocument()
    expect(screen.getByText('−242.000 ₫')).toBeInTheDocument()
    expect(screen.getByText('14.758.000 ₫')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '1.800.000 ₫' })).toHaveAttribute('href', '/wallets')
    expect(screen.getByText(formatMonth(currentMonth()))).toBeInTheDocument()

    const meters = await screen.findAllByRole('progressbar')
    expect(meters.map((m) => m.getAttribute('aria-label'))).toEqual([
      'Đã dùng ngân sách Đi lại',
      'Đã dùng ngân sách Ăn uống',
      'Đã dùng ngân sách Giải trí',
    ])
    expect(screen.getByText('105% · Vượt ngân sách')).toBeInTheDocument()

    expect(await screen.findByText('Tiền mặt · Phở bò')).toBeInTheDocument()

    const month = monthRange(currentMonth())
    const query = (path: string) => Object.fromEntries(mock.requests.find((r) => r.path === path)!.query)
    expect(query('/api/reports/summary')).toEqual(month)
    expect(query('/api/reports/trend')).toEqual({ from: monthRange(shiftMonth(currentMonth(), -5)).from, to: month.to, groupBy: 'month' })
    expect(query('/api/transactions')).toMatchObject({ pageSize: '5', to: month.to })
  })

  it('chưa có giao dịch nào → hướng dẫn thêm khoản chi đầu tiên, mở hộp thoại', async () => {
    dashboardApi({
      'GET /api/reports/summary': [200, { from: '', to: '', income: 0, expense: 0, net: 0, transactionCount: 0, dailyAverageExpense: 0 }],
      'GET /api/reports/by-category': [200, []],
      'GET /api/transactions': [200, { ...recent, items: [], totalItems: 0 }],
      'GET /api/budgets': [200, []],
    })
    renderRoute('/')

    const empty = await screen.findByText('Chưa có giao dịch – thêm khoản chi đầu tiên')
    expect(screen.getByText('Chưa đặt ngân sách cho tháng này')).toBeInTheDocument()
    await userEvent.click(within(empty.parentElement!).getByRole('button', { name: 'Thêm khoản chi' }))
    expect(await screen.findByRole('dialog', { name: 'Thêm giao dịch' })).toBeInTheDocument()
  })
})
