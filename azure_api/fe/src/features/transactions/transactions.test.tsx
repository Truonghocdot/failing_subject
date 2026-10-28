import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { today } from '../../lib/format'
import type { PagedTransactions, Transaction } from '../../lib/types'
import { renderRoute, signedInApi } from '../../test/render'

const tx = (over: Partial<Transaction>): Transaction => ({
  id: 1,
  kind: 'expense',
  amount: 45_000,
  occurredOn: '2026-10-28',
  note: null,
  wallet: { id: 1, name: 'Tiền mặt', color: '#16a34a' },
  toWallet: null,
  category: { id: 10, name: 'Ăn uống', icon: 'utensils', color: '#f97316' },
  createdAt: '2026-10-28T05:00:00Z',
  ...over,
})

const page: PagedTransactions = {
  items: [
    tx({ id: 3, note: 'Phở bò' }),
    tx({ id: 2, kind: 'transfer', amount: 300_000, category: null, toWallet: { id: 2, name: 'Vietcombank', color: '#2563eb' } }),
    tx({ id: 1, kind: 'income', amount: 15_000_000, occurredOn: '2026-10-05', category: { id: 20, name: 'Lương', icon: 'banknote', color: '#16a34a' } }),
  ],
  page: 1,
  pageSize: 30,
  totalItems: 3,
  totalPages: 1,
  totals: { income: 15_000_000, expense: 45_000 },
}

describe('trang Giao dịch', () => {
  it('nhóm theo ngày, hiện tổng thu / chi / chênh lệch của bộ lọc', async () => {
    signedInApi({ 'GET /api/transactions': [200, page] })
    renderRoute('/transactions?month=2026-10')

    expect(await screen.findByText('Phở bò', { exact: false })).toBeInTheDocument()
    expect(screen.getByText('Tháng 10/2026')).toBeInTheDocument()
    expect(screen.getByText('+15.000.000 ₫', { selector: 'p span' })).toBeInTheDocument()
    expect(screen.getByText('14.955.000 ₫')).toBeInTheDocument()
    expect(screen.getByText('Tiền mặt → Vietcombank')).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual([
      expect.stringContaining('28/10/2026'),
      expect.stringContaining('05/10/2026'),
    ])
  })

  it('bộ lọc trên URL được gửi lên API; đổi tháng thì gọi lại', async () => {
    const mock = signedInApi({ 'GET /api/transactions': [200, page] })
    renderRoute('/transactions?month=2026-10&walletId=2&kind=expense')

    await screen.findByText('Phở bò', { exact: false })
    const first = mock.requests.find((r) => r.path === '/api/transactions')!
    expect(Object.fromEntries(first.query)).toEqual({ from: '2026-10-01', to: '2026-10-31', walletId: '2', kind: 'expense', pageSize: '30' })

    await userEvent.click(screen.getByRole('button', { name: 'Tháng trước' }))
    await waitFor(() => expect(mock.requests.some((r) => r.path === '/api/transactions' && r.query.get('from') === '2026-09-01')).toBe(true))
  })

  it('tìm kiếm rồi "Bỏ lọc" → mất hết bộ lọc, ô tìm kiếm trống (không bị ô tìm kiếm chờ 300ms ghi đè lại)', async () => {
    const mock = signedInApi({ 'GET /api/transactions': [200, page] })
    const { router } = renderRoute('/transactions?kind=expense')

    await userEvent.type(await screen.findByLabelText('Tìm trong ghi chú'), 'phở')
    await waitFor(() => expect(router.state.location.search).toContain('q=ph'))
    await userEvent.click(screen.getByRole('button', { name: 'Bỏ lọc' }))
    await new Promise((r) => setTimeout(r, 400))

    expect(router.state.location.search).toBe('')
    expect(screen.getByLabelText('Tìm trong ghi chú')).toHaveValue('')
    const last = mock.requests.filter((r) => r.path === '/api/transactions').at(-1)!
    expect(last.query.has('kind')).toBe(false)
    expect(last.query.has('q')).toBe(false)
  })

  it('thêm khoản chi: chọn danh mục, gõ số tiền → gửi đúng dữ liệu', async () => {
    const mock = signedInApi({
      'GET /api/transactions': [200, { ...page, items: [], totalItems: 0 }],
      'POST /api/transactions': (req) => [201, tx({ id: 9, ...(req.body as object) })],
    })
    renderRoute('/transactions')

    await userEvent.click(await screen.findByRole('button', { name: 'Thêm khoản chi đầu tiên' }))
    const dialog = await screen.findByRole('dialog', { name: 'Thêm giao dịch' })
    await userEvent.type(within(dialog).getByLabelText('Số tiền'), '45000')
    expect(within(dialog).getByLabelText('Số tiền')).toHaveValue('45.000')
    await userEvent.click(within(dialog).getByRole('button', { name: /Ăn uống/ }))
    await userEvent.type(within(dialog).getByLabelText('Ghi chú'), 'Phở bò')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(mock.requests.find((r) => r.method === 'POST' && r.path === '/api/transactions')?.body).toEqual({
      kind: 'expense',
      amount: 45000,
      walletId: 1,
      toWalletId: null,
      categoryId: 10,
      occurredOn: today(),
      note: 'Phở bò',
    })
  })

  it('chuyển tiền: bắt buộc chọn ví nhận khác ví nguồn', async () => {
    const mock = signedInApi({ 'GET /api/transactions': [200, page] })
    renderRoute('/transactions')

    await userEvent.click(within(await screen.findByRole('main')).getByRole('button', { name: 'Thêm giao dịch' }))
    const dialog = await screen.findByRole('dialog', { name: 'Thêm giao dịch' })
    expect(within(dialog).getByLabelText('Số tiền')).toHaveFocus() // gõ số tiền được ngay
    await userEvent.click(within(dialog).getByRole('radio', { name: 'Chuyển tiền' }))
    await userEvent.type(within(dialog).getByLabelText('Số tiền'), '300000')
    await userEvent.selectOptions(within(dialog).getByLabelText('Đến ví'), 'Tiền mặt')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm' }))

    expect(await within(dialog).findByText('Ví nhận phải khác ví nguồn')).toBeInTheDocument()
    expect(within(dialog).queryByText('Danh mục')).not.toBeInTheDocument()
    expect(mock.requests.some((r) => r.method === 'POST' && r.path === '/api/transactions')).toBe(false)
  })

  it('Esc đóng hộp thoại và trả focus về nút đã mở (người dùng bàn phím không bị mất vị trí)', async () => {
    signedInApi({ 'GET /api/transactions': [200, page] })
    renderRoute('/transactions')

    const opener = within(await screen.findByRole('main')).getByRole('button', { name: 'Thêm giao dịch' })
    await userEvent.click(opener)
    const dialog = await screen.findByRole('dialog', { name: 'Thêm giao dịch' })
    expect(within(dialog).getByLabelText('Số tiền')).toHaveFocus()

    await userEvent.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(opener).toHaveFocus()
  })

  it('bấm 1 dòng → sửa; lỗi từ máy chủ hiện đúng ô', async () => {
    signedInApi({
      'GET /api/transactions': [200, page],
      'PUT /api/transactions/3': [400, { title: 'Dữ liệu không hợp lệ', detail: 'Ví đã lưu trữ', errors: { walletId: ['Ví đã lưu trữ'] } }],
    })
    renderRoute('/transactions')

    await userEvent.click(await screen.findByRole('button', { name: /Phở bò/ }))
    const dialog = await screen.findByRole('dialog', { name: 'Sửa giao dịch' })
    expect(within(dialog).getByLabelText('Số tiền')).toHaveValue('45.000')
    expect(within(dialog).getByLabelText('Ghi chú')).toHaveValue('Phở bò')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Lưu' }))

    expect(await within(dialog).findByText('Ví đã lưu trữ')).toBeInTheDocument()
  })
})
