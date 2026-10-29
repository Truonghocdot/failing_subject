import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { toCsv } from '../../lib/csv'
import type { Report } from '../../lib/types'
import { asAdmin, booking, homestaySummaries, loggedIn, stay } from '../../test/fixtures'
import { calls, mockApi, renderRoute } from '../../test/render'

const metrics = (sold: number, sellable: number, revenue: number) => ({
  sellableNights: sellable, soldNights: sold, occupancy: sold / sellable, roomRevenue: revenue, adr: Math.round(revenue / sold), revPar: Math.round(revenue / sellable),
})

const report: Report = {
  from: '2026-10-01',
  to: '2026-11-30',
  homestayId: null,
  total: metrics(30, 120, 27_000_000),
  collected: 20_000_000,
  refunded: 500_000,
  bookings: 12,
  cancelled: 2,
  noShow: 1,
  expired: 3,
  cancelRate: 0.25,
  months: [
    { key: '2026-10', label: '10/2026', metrics: metrics(10, 62, 9_000_000), collected: 8_000_000, bookings: 5, cancelled: 1 },
    { key: '2026-11', label: '11/2026', metrics: metrics(20, 58, 18_000_000), collected: 12_000_000, bookings: 7, cancelled: 2 },
  ],
  homestays: [{ key: '1', label: 'Đồi Thông, "view" núi', metrics: metrics(30, 120, 27_000_000), collected: 20_000_000, bookings: 12, cancelled: 3 }],
  sources: [
    { source: 'web', bookings: 8, cancelled: 2, soldNights: 18, roomRevenue: 16_000_000 },
    { source: 'phone', bookings: 4, cancelled: 1, soldNights: 12, roomRevenue: 11_000_000 },
    { source: 'walk_in', bookings: 0, cancelled: 0, soldNights: 0, roomRevenue: 0 },
    { source: 'ota', bookings: 0, cancelled: 0, soldNights: 0, roomRevenue: 0 },
  ],
}

describe('báo cáo', () => {
  it('chỉ số, 2 biểu đồ riêng (không 2 trục), bảng theo tháng / homestay / nguồn; tooltip khi focus cột', async () => {
    mockApi({ ...asAdmin, 'GET /api/admin/homestays': [200, homestaySummaries], 'GET /api/admin/reports': [200, report] })
    renderRoute('/admin/reports?from=2026-10-01&to=2026-11-30')

    expect(await screen.findByText('30/120 đêm')).toBeInTheDocument()
    expect(screen.getByText('Công suất phòng', { selector: 'p' }).parentElement).toHaveTextContent('25%') // 30/120
    expect(screen.getByText(/đã trừ hoàn 500\.000/)).toBeInTheDocument()
    expect(screen.getByText('2 huỷ + 1 không đến / 12 đặt phòng')).toBeInTheDocument()

    const revenue = screen.getByRole('group', { name: 'Doanh thu phòng theo tháng' })
    expect(screen.getByRole('group', { name: 'Công suất phòng theo tháng' })).toBeInTheDocument()
    const nov = within(revenue).getByRole('img', { name: /^11\/2026: 18\.000\.000/ })
    await userEvent.hover(nov)
    expect(screen.getAllByRole('status').some((s) => /18\.000\.000.*11\/2026/.test(s.textContent ?? ''))).toBe(true)
    await userEvent.unhover(nov)
    expect(screen.getAllByRole('status').some((s) => /11\/2026/.test(s.textContent ?? ''))).toBe(false)

    expect(screen.getAllByRole('row', { name: /10\/2026/ }).length).toBeGreaterThan(0)
    expect(screen.getByRole('rowheader', { name: 'Điện thoại' })).toBeInTheDocument()
  })

  it('chọn nhanh "Năm nay" cập nhật URL + gọi API đúng khoảng; xuất CSV', async () => {
    const fetchMock = mockApi({ ...asAdmin, 'GET /api/admin/homestays': [200, homestaySummaries], 'GET /api/admin/reports': [200, report] })
    const createUrl = vi.fn(() => 'blob:csv')
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: createUrl, revokeObjectURL: vi.fn() }))
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const { router } = renderRoute('/admin/reports')

    await userEvent.click(await screen.findByRole('button', { name: 'Năm nay' }))
    const year = new Date().getFullYear()
    expect(router.state.location.search).toBe(`?from=${year}-01-01&to=${year}-12-31`)
    await waitFor(() => expect(calls(fetchMock, 'GET /api/admin/reports').at(-1)!.url.search).toBe(`?from=${year}-01-01&to=${year}-12-31`))

    await screen.findByText('30/120 đêm')
    await userEvent.click(screen.getAllByRole('button', { name: 'Xuất CSV' })[0])
    expect(createUrl).toHaveBeenCalledTimes(1)
    expect(click).toHaveBeenCalledTimes(1)
  })

  it('CSV: BOM cho Excel, bọc ô có dấu phẩy / ngoặc kép, số giữ dạng thô', () => {
    expect(toCsv([['Homestay', 'Doanh thu'], ['Đồi Thông, "view" núi', 27000000]])).toBe('﻿Homestay,Doanh thu\r\n"Đồi Thông, ""view"" núi",27000000')
  })
})

describe('đánh giá', () => {
  it('khách: sau trả phòng viết đánh giá (bắt buộc chọn sao) → hiện lại đánh giá của mình', async () => {
    const stayed = booking({ status: 'checked_out', transfer: null, canReview: true })
    const fetchMock = mockApi({
      ...loggedIn,
      'GET /api/bookings/HS-AB12-CD34': [200, stayed],
      'POST /api/bookings/HS-AB12-CD34/review': [201, { id: 9, rating: 4, comment: 'Rất sạch', createdAt: new Date().toISOString() }],
    })
    renderRoute('/bookings/HS-AB12-CD34')

    await userEvent.click(await screen.findByRole('button', { name: 'Gửi đánh giá' }))
    expect(screen.getByText('Vui lòng chọn số sao')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('radio', { name: '4 sao – Tốt' }))
    await userEvent.type(screen.getByLabelText('Nhận xét (không bắt buộc)'), 'Rất sạch')
    await userEvent.click(screen.getByRole('button', { name: 'Gửi đánh giá' }))

    expect(await screen.findByRole('heading', { name: 'Đánh giá của bạn' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: '4/5 sao' })).toBeInTheDocument()
    expect(calls(fetchMock, 'POST /api/bookings/HS-AB12-CD34/review')[0].body).toEqual({ rating: 4, comment: 'Rất sạch' })
  })

  it('trang homestay: điểm trung bình + danh sách tên rút gọn, "Xem thêm" tải trang 2', async () => {
    const review = (id: number) => ({ id, rating: 5, comment: `Nhận xét ${id}`, author: 'Minh T.', roomName: 'Phòng Gác Mái', stayedAt: '2026-09-12', createdAt: '2026-09-15T00:00:00Z' })
    mockApi({
      'GET /health': [200, { status: 'ok', database: 'up' }],
      'GET /api/stays/doi-thong': [200, stay],
      'GET /api/stays/doi-thong/reviews': (req) =>
        req.url.searchParams.get('page') === '2'
          ? [200, { items: [review(3)], page: 2, pageSize: 2, total: 3 }]
          : [200, { items: [review(1), review(2)], page: 1, pageSize: 2, total: 3 }],
    })
    renderRoute('/stays/doi-thong')

    await screen.findByText('Nhận xét 1')
    expect(screen.getByRole('heading', { name: (n) => n.startsWith('Đánh giá') && n.includes('4,6/5 (8)') })).toBeInTheDocument()
    expect(await screen.findByText('Nhận xét 2')).toBeInTheDocument()
    expect(screen.getAllByText('Minh T.')).toHaveLength(2)
    expect(screen.getAllByText('Phòng Gác Mái · ở tháng 09/2026')).toHaveLength(2)
    await userEvent.click(screen.getByRole('button', { name: 'Xem thêm đánh giá' }))
    expect(await screen.findByText('Nhận xét 3')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Xem thêm đánh giá' })).not.toBeInTheDocument()
  })

  it('admin: lọc đang hiện / ẩn trên URL, ẩn 1 đánh giá', async () => {
    const fetchMock = mockApi({
      ...asAdmin,
      'GET /api/admin/homestays': [200, homestaySummaries],
      'GET /api/admin/reviews': [200, { items: [{ id: 7, bookingId: 100, bookingCode: 'HS-AB12-CD34', homestayId: 1, homestayName: 'Đồi Thông Homestay', guestName: 'Nguyễn An', rating: 1, comment: 'Spam quảng cáo', isHidden: false, createdAt: '2026-10-01T00:00:00Z' }], page: 1, pageSize: 10, total: 1 }],
      'PUT /api/admin/reviews/7': [204],
    })
    const { router } = renderRoute('/admin/reviews')

    expect(await screen.findByText('Spam quảng cáo')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Trạng thái' }), 'false')
    expect(router.state.location.search).toBe('?hidden=false')
    await userEvent.click(screen.getByRole('button', { name: 'Ẩn' }))
    await waitFor(() => expect(calls(fetchMock, 'PUT /api/admin/reviews/7')[0].body).toEqual({ isHidden: true }))
  })
})
