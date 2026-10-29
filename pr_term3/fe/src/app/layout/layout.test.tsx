import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoute } from '../../test/render'
import { adminNav } from './nav'

const healthy = { 'GET /health': [200, { status: 'ok', database: 'up' }] } as const

describe('khu khách', () => {
  it('trang chủ có thanh điều hướng, ô tìm kiếm và trạng thái máy chủ', async () => {
    mockApi({ ...healthy })
    renderRoute('/')

    const nav = screen.getByRole('navigation', { name: 'Điều hướng khách' })
    expect(within(nav).getAllByRole('link').map((a) => a.textContent)).toEqual(['Tìm homestay', 'Đặt phòng của tôi', 'Tra cứu đặt phòng', 'Đăng nhập'])
    expect(screen.getByRole('heading', { level: 1, name: /Tìm homestay ưng ý/ })).toBeInTheDocument()
    expect(await screen.findByText('Máy chủ: hoạt động')).toBeInTheDocument()
  })

  it('menu điện thoại mở / đóng được', async () => {
    mockApi({ ...healthy })
    renderRoute('/')

    await userEvent.click(screen.getByRole('button', { name: 'Mở menu' }))
    const mobile = screen.getByRole('navigation', { name: 'Điều hướng khách (điện thoại)' })
    await userEvent.click(within(mobile).getByRole('link', { name: 'Tra cứu đặt phòng' }))

    expect(screen.getByRole('heading', { name: 'Tra cứu đặt phòng' })).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Điều hướng khách (điện thoại)' })).not.toBeInTheDocument()
  })

  it('báo mất database khi /health trả 503', async () => {
    mockApi({ 'GET /health': [503, { status: 'degraded', database: 'down' }] })
    renderRoute('/')

    expect(await screen.findByText('Máy chủ: mất kết nối database')).toBeInTheDocument()
  })

  it('đường dẫn lạ → 404', () => {
    mockApi({ ...healthy })
    renderRoute('/khong-co')

    expect(screen.getByText('Không tìm thấy trang bạn cần.')).toBeInTheDocument()
  })
})

describe('khu quản trị', () => {
  it('sidebar đủ mục, mục đang mở được đánh dấu, chuyển trang khi bấm', async () => {
    mockApi({ ...healthy })
    renderRoute('/admin')

    const nav = screen.getByRole('navigation', { name: 'Điều hướng quản trị' })
    expect(within(nav).getAllByRole('link')).toHaveLength(adminNav.length)
    expect(within(nav).getByRole('link', { name: 'Hôm nay' })).toHaveAttribute('aria-current', 'page')

    await userEvent.click(within(nav).getByRole('link', { name: 'Lịch phòng' }))
    expect(screen.getByRole('heading', { name: 'Lịch phòng' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Lịch phòng' })).toHaveAttribute('aria-current', 'page')
  })

  it('404 trong khu quản trị dẫn về trang Hôm nay', () => {
    mockApi({ ...healthy })
    renderRoute('/admin/khong-co')

    expect(screen.getByRole('link', { name: 'Về trang Hôm nay' })).toHaveAttribute('href', '/admin')
  })
})
