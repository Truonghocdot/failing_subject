import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoute } from '../../test/render'
import { adminNav } from './nav'
import { asAdmin, asStaff, loggedIn } from '../../test/fixtures'

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
  const dashboard = { 'GET /api/admin/dashboard': [200, { date: '2026-11-01', arrivals: [], departures: [], inHouse: [], pendingHolds: [], next7Days: [] }] } as const

  it('admin: sidebar đủ mục, mục đang mở được đánh dấu, chuyển trang khi bấm', async () => {
    mockApi({ ...asAdmin, ...dashboard, 'GET /api/admin/calendar': [200, { from: '2026-11-01', to: '2026-11-14', rooms: [] }], 'GET /api/admin/homestays': [200, []] })
    renderRoute('/admin')

    const nav = await screen.findByRole('navigation', { name: 'Điều hướng quản trị' })
    expect(within(nav).getAllByRole('link')).toHaveLength(adminNav.length)
    expect(within(nav).getByRole('link', { name: 'Hôm nay' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByText('Quản Trị')).toBeInTheDocument()

    await userEvent.click(within(nav).getByRole('link', { name: 'Lịch phòng' }))
    expect(await screen.findByRole('heading', { name: 'Lịch phòng' })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Lịch phòng' })).toHaveAttribute('aria-current', 'page')
  })

  it('lễ tân: ẩn mục chỉ admin; gõ thẳng /admin/users → Không có quyền', async () => {
    mockApi({ ...asStaff, ...dashboard })
    const { router } = renderRoute('/admin')

    const nav = await screen.findByRole('navigation', { name: 'Điều hướng quản trị' })
    expect(within(nav).getAllByRole('link').map((a) => a.textContent)).toEqual(['Hôm nay', 'Lịch phòng', 'Đặt phòng', 'Khách'])
    await router.navigate('/admin/users')
    expect(await screen.findByRole('heading', { name: 'Không có quyền' })).toBeInTheDocument()
    expect(screen.getByText('Mục này chỉ dành cho quản trị viên.')).toBeInTheDocument()
  })

  it('khách đã đăng nhập vào /admin → Không có quyền; chưa đăng nhập → /login?next', async () => {
    mockApi({ ...loggedIn })
    renderRoute('/admin')
    expect(await screen.findByText('Khu quản trị chỉ dành cho lễ tân và quản trị viên.')).toBeInTheDocument()
  })

  it('chưa đăng nhập → /login?next=/admin/calendar', async () => {
    mockApi({ ...healthy })
    const { router } = renderRoute('/admin/calendar')
    await screen.findByRole('heading', { name: 'Đăng nhập' })
    expect(router.state.location.search).toBe(`?next=${encodeURIComponent('/admin/calendar')}`)
  })

  it('404 trong khu quản trị dẫn về trang Hôm nay', async () => {
    mockApi({ ...asAdmin })
    renderRoute('/admin/khong-co')

    expect(await screen.findByRole('link', { name: 'Về trang Hôm nay' })).toHaveAttribute('href', '/admin')
  })
})
