import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderRoute, signedInApi } from '../../test/render'

describe('AppLayout', () => {
  it('hiện đủ 7 mục điều hướng, tên người dùng và chuyển trang khi bấm', async () => {
    signedInApi({ 'GET /api/transactions': [200, { items: [], page: 1, pageSize: 30, totalItems: 0, totalPages: 1, totals: { income: 0, expense: 0 } }] })
    renderRoute('/')

    const sidebar = await screen.findByRole('navigation', { name: 'Điều hướng chính' })
    expect(within(sidebar).getAllByRole('link')).toHaveLength(7)
    expect(screen.getByText('An Nguyễn')).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Tổng quan' })).toBeInTheDocument() // trang tải theo nhu cầu

    await userEvent.click(within(sidebar).getByRole('link', { name: 'Giao dịch' }))
    expect(await screen.findByRole('heading', { name: 'Giao dịch' })).toBeInTheDocument()
    expect(within(sidebar).getByRole('link', { name: 'Giao dịch' })).toHaveAttribute('aria-current', 'page')
  })

  it('báo máy chủ hoạt động khi /health ok', async () => {
    signedInApi()
    renderRoute('/')

    expect(await screen.findByText('Máy chủ: hoạt động')).toBeInTheDocument()
  })

  it('báo mất database khi /health trả 503', async () => {
    signedInApi({ 'GET /health': [503, { status: 'degraded', database: 'down' }] })
    renderRoute('/')

    expect(await screen.findByText('Máy chủ: mất kết nối database')).toBeInTheDocument()
  })

  it('hiện trang 404 cho đường dẫn lạ', async () => {
    signedInApi()
    renderRoute('/khong-co')

    expect(await screen.findByText('Không tìm thấy trang bạn cần.')).toBeInTheDocument()
  })

  it('nút "+ Thêm giao dịch" mở hộp thoại ở bất kỳ trang nào', async () => {
    signedInApi()
    renderRoute('/settings')

    const sidebar = await screen.findByRole('complementary')
    await userEvent.click(within(sidebar).getByRole('button', { name: 'Thêm giao dịch' }))

    expect(await screen.findByRole('dialog', { name: 'Thêm giao dịch' })).toBeInTheDocument()
  })
})
