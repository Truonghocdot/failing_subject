import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { Budget } from '../../lib/types'
import { renderRoute, signedInApi } from '../../test/render'

const budget = (over: Partial<Budget>): Budget => ({
  id: 1,
  month: '2026-10',
  category: { id: 10, name: 'Ăn uống', icon: 'utensils', color: '#f97316' },
  amount: 1_000_000,
  spent: 850_000,
  remaining: 150_000,
  percent: 85,
  status: 'warning',
  ...over,
})

describe('trang Ngân sách', () => {
  it('hiện tiến độ từng danh mục với % và trạng thái bằng chữ, cùng tổng tháng', async () => {
    const mock = signedInApi({
      'GET /api/budgets': [
        200,
        [
          budget({}),
          budget({ id: 2, category: { id: 11, name: 'Đi lại', icon: 'bus', color: '#0ea5e9' }, amount: 200_000, spent: 240_000, remaining: -40_000, percent: 120, status: 'over' }),
        ],
      ],
    })
    renderRoute('/budgets?month=2026-09')

    const food = await screen.findByRole('progressbar', { name: 'Đã dùng ngân sách Ăn uống' })
    expect(food).toHaveAttribute('aria-valuenow', '85')
    // vượt 100% → thanh dừng ở 100 nhưng chữ vẫn ghi đúng 120%
    expect(screen.getByRole('progressbar', { name: 'Đã dùng ngân sách Đi lại' })).toHaveAttribute('aria-valuenow', '100')
    expect(screen.getByText('85% · Sắp hết')).toBeInTheDocument()
    expect(screen.getByText('120% · Vượt ngân sách')).toBeInTheDocument()
    expect(screen.getByText('Tháng 09/2026')).toBeInTheDocument()
    expect(screen.getByText('1.200.000 ₫')).toBeInTheDocument() // tổng ngân sách
    expect(screen.getByText('1.090.000 ₫')).toBeInTheDocument() // đã chi
    expect(mock.requests.find((r) => r.path === '/api/budgets')!.query.get('month')).toBe('2026-09')
  })

  it('thêm ngân sách: chỉ cho chọn danh mục chi chưa có ngân sách, gửi PUT đúng tháng', async () => {
    const mock = signedInApi({
      'GET /api/budgets': [200, [budget({})]],
      'PUT /api/budgets': (req) => [200, budget({ id: 2, ...(req.body as object) })],
    })
    renderRoute('/budgets?month=2026-10')
    await screen.findByText('85% · Sắp hết')

    await userEvent.click(screen.getByRole('button', { name: 'Thêm ngân sách' }))
    const dialog = await screen.findByRole('dialog', { name: 'Thêm ngân sách' })
    const select = within(dialog).getByLabelText('Danh mục chi')
    // Ăn uống đã có, Lương là danh mục thu → chỉ còn Đi lại
    expect(within(select).getAllByRole('option').map((o) => o.textContent)).toEqual(['Chọn danh mục…', 'Đi lại'])

    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm' }))
    expect(await within(dialog).findByText('Vui lòng chọn danh mục')).toBeInTheDocument()
    expect(within(dialog).getByText('Vui lòng nhập số tiền ngân sách')).toBeInTheDocument()

    await userEvent.selectOptions(select, 'Đi lại')
    await userEvent.type(within(dialog).getByLabelText('Hạn mức trong tháng'), '500000')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(mock.requests.find((r) => r.method === 'PUT')!.body).toEqual({ month: '2026-10', categoryId: 11, amount: 500_000 })
  })

  it('sửa hạn mức: ô tiền điền sẵn, lỗi server hiện dưới ô', async () => {
    signedInApi({
      'GET /api/budgets': [200, [budget({})]],
      'PUT /api/budgets': [400, { title: 'Dữ liệu không hợp lệ', errors: { amount: ['Số tiền ngân sách vượt quá giới hạn cho phép'] } }],
    })
    renderRoute('/budgets?month=2026-10')

    await userEvent.click(await screen.findByRole('button', { name: 'Sửa ngân sách Ăn uống' }))
    const dialog = await screen.findByRole('dialog', { name: 'Sửa ngân sách Ăn uống' })
    const amount = within(dialog).getByLabelText('Hạn mức trong tháng')
    expect(amount).toHaveValue('1.000.000')
    expect(amount).toHaveFocus()
    expect(within(dialog).queryByLabelText('Danh mục chi')).not.toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Lưu' }))
    expect(await within(dialog).findByText('Số tiền ngân sách vượt quá giới hạn cho phép')).toBeInTheDocument()
  })

  it('tháng trống: hướng dẫn + sao chép tháng trước báo số ngân sách đã thêm', async () => {
    let copied = false
    const mock = signedInApi({
      'GET /api/budgets': () => [200, copied ? [budget({})] : []],
      'POST /api/budgets/copy': () => {
        copied = true
        return [200, { created: 1 }]
      },
    })
    renderRoute('/budgets?month=2026-11')

    expect(await screen.findByText('Chưa có ngân sách cho tháng 11/2026')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Chép từ tháng 10/2026' }))

    expect(await screen.findByText('Đã thêm 1 ngân sách từ tháng 10/2026')).toBeInTheDocument()
    expect(await screen.findByText('85% · Sắp hết')).toBeInTheDocument()
    expect(mock.requests.find((r) => r.path === '/api/budgets/copy')!.body).toEqual({ from: '2026-10', to: '2026-11' })
  })

  it('xoá ngân sách sau khi xác nhận', async () => {
    let deleted = false
    const mock = signedInApi({
      'GET /api/budgets': () => [200, deleted ? [] : [budget({ id: 7 })]],
      'DELETE /api/budgets/7': () => {
        deleted = true
        return [204]
      },
    })
    renderRoute('/budgets?month=2026-10')

    await userEvent.click(await screen.findByRole('button', { name: 'Xoá ngân sách Ăn uống' }))
    await userEvent.click(within(await screen.findByRole('dialog', { name: 'Xoá ngân sách?' })).getByRole('button', { name: 'Xoá' }))

    expect(await screen.findByText('Chưa có ngân sách cho tháng 10/2026')).toBeInTheDocument()
    expect(mock.requests.some((r) => r.method === 'DELETE')).toBe(true)
  })
})
