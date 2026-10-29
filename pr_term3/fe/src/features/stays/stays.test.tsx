import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { addDays, nightsBetween } from '../../lib/format'
import { base, checkIn, checkOut, quote, stay, stayCard } from '../../test/fixtures'
import { calls, mockApi, renderRoute } from '../../test/render'
import { tripError } from './api'

const paged = (items = [stayCard]) => [200, { items, page: 1, pageSize: 12, total: items.length }] as const

describe('tìm kiếm', () => {
  it('trang chủ: chọn nơi đến + ngày → /stays với bộ lọc trên URL', async () => {
    mockApi({ ...base, 'GET /api/stays': paged() })
    const { router } = renderRoute('/')

    const search = screen.getByRole('search', { name: 'Tìm homestay' })
    await within(search).findByRole('option', { name: 'Lâm Đồng (1)' })
    await userEvent.selectOptions(within(search).getByRole('combobox', { name: 'Nơi đến' }), 'Lâm Đồng')
    // gõ ngày nhận → ngày trả tự lùi sang hôm sau
    await userEvent.type(within(search).getByLabelText('Nhận phòng'), checkIn)
    expect(within(search).getByLabelText('Trả phòng')).toHaveValue(addDays(checkIn, 1))
    await userEvent.click(within(search).getByRole('button', { name: 'Thêm 1 khách' }))
    await userEvent.click(within(search).getByRole('button', { name: 'Tìm' }))

    // /stays tách chunk riêng → URL đổi sau khi tải xong trang
    await waitFor(() => expect(router.state.location.pathname).toBe('/stays'))
    expect(Object.fromEntries(new URLSearchParams(router.state.location.search))).toEqual({
      province: 'Lâm Đồng',
      checkIn,
      checkOut: addDays(checkIn, 1),
      guests: '3',
    })
  })

  it('kết quả đọc bộ lọc từ URL, đổi tiện nghi / sắp xếp cập nhật URL và gọi lại API', async () => {
    const fetchMock = mockApi({ ...base, 'GET /api/stays': paged() })
    const { router } = renderRoute(`/stays?province=L%C3%A2m+%C4%90%E1%BB%93ng&checkIn=${checkIn}&checkOut=${checkOut}&guests=2`)

    expect(await screen.findByRole('heading', { name: '1 homestay ở Lâm Đồng' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Đồi Thông Homestay/ })).toHaveAttribute('href', `/stays/doi-thong?checkIn=${checkIn}&checkOut=${checkOut}&guests=2`)
    expect(calls(fetchMock, 'GET /api/stays')[0].url.searchParams.get('checkIn')).toBe(checkIn)

    await userEvent.click(screen.getByRole('button', { name: /Bộ lọc/ }))
    await userEvent.click(screen.getByRole('checkbox', { name: 'Hồ bơi' }))
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Sắp xếp' }), 'price')

    const params = new URLSearchParams(router.state.location.search)
    expect(params.get('amenities')).toBe('pool')
    expect(params.get('sort')).toBe('price')
    await waitFor(() => expect(calls(fetchMock, 'GET /api/stays').at(-1)!.url.searchParams.get('sort')).toBe('price'))
  })

  it('ngày trên URL sai / đã qua thì bỏ qua thay vì gửi lên gây 400', async () => {
    const fetchMock = mockApi({ ...base, 'GET /api/stays': paged([]) })
    renderRoute('/stays?checkIn=2020-01-01&checkOut=2020-01-03')

    expect(await screen.findByText('Không có homestay phù hợp')).toBeInTheDocument()
    expect(calls(fetchMock, 'GET /api/stays')[0].url.searchParams.has('checkIn')).toBe(false)
  })

  it('kiểm tra khoảng ngày giống backend', () => {
    expect(tripError(checkIn, checkIn)).toBe('Ngày trả phòng phải sau ngày nhận phòng')
    expect(tripError(checkIn, addDays(checkIn, 31))).toBe('Mỗi lần đặt tối đa 30 đêm')
    expect(tripError('2020-01-01', '2020-01-02')).toBe('Ngày nhận phòng không được ở quá khứ')
    expect(tripError(checkIn, '')).toBe('Vui lòng chọn ngày trả phòng')
    expect(nightsBetween(checkIn, checkOut)).toBe(2)
  })
})

describe('chi tiết homestay', () => {
  const calendar = {
    from: checkIn,
    to: checkOut,
    rooms: [{ roomId: 11, name: 'Phòng Gác Mái', days: Array.from({ length: 40 }, (_, i) => ({ date: addDays(checkIn, i - 10), available: true, price: 800_000, source: 'base' })) }],
  }

  it('chọn phòng → báo giá từng đêm → nút đặt dẫn tới /book giữ ngày + số khách', async () => {
    const fetchMock = mockApi({ ...base, 'GET /api/stays/doi-thong': [200, stay], 'GET /api/stays/doi-thong/calendar': [200, calendar], 'POST /api/quotes': [200, quote()] })
    const { router } = renderRoute(`/stays/doi-thong?checkIn=${checkIn}&checkOut=${checkOut}&guests=2`)

    expect(await screen.findByRole('heading', { level: 1, name: 'Đồi Thông Homestay' })).toBeInTheDocument()
    expect(screen.getByText(/Huỷ trước ngày nhận phòng từ 7 ngày: hoàn 100%/)).toBeInTheDocument()
    const panel = screen.getByRole('complementary', { name: 'Đặt phòng' })
    expect(within(panel).getByText('Chọn phòng để xem giá.')).toBeInTheDocument()

    await userEvent.click(screen.getAllByRole('button', { name: 'Chọn phòng này' })[0])
    expect(new URLSearchParams(router.state.location.search).get('room')).toBe('11')

    expect(await within(panel).findByText('Tổng cộng')).toBeInTheDocument()
    expect(within(panel).getAllByText(/1\.700\.000/).length).toBeGreaterThan(0)
    expect(calls(fetchMock, 'POST /api/quotes')[0].body).toEqual({ roomId: 11, checkIn, checkOut, guests: 2 })
    expect(within(panel).getByRole('link', { name: 'Đặt phòng' })).toHaveAttribute(
      'href',
      `/book/11?stay=doi-thong&checkIn=${checkIn}&checkOut=${checkOut}&guests=2`,
    )
    expect(screen.getByRole('grid', { name: /Lịch trống Phòng Gác Mái/ })).toBeInTheDocument()
  })

  it('phòng đã có người đặt → báo, không cho bấm đặt', async () => {
    mockApi({ ...base, 'GET /api/stays/doi-thong': [200, stay], 'GET /api/stays/doi-thong/calendar': [200, calendar], 'POST /api/quotes': [200, quote(1_700_000, { available: false })] })
    renderRoute(`/stays/doi-thong?room=11&checkIn=${checkIn}&checkOut=${checkOut}&guests=2`)

    const panel = await screen.findByRole('complementary', { name: 'Đặt phòng' })
    expect(await within(panel).findByText(/Phòng đã có người đặt trong khoảng ngày này/)).toBeInTheDocument()
    expect(within(panel).getByRole('button', { name: 'Đặt phòng' })).toBeDisabled()
  })

  it('slug không tồn tại → 404', async () => {
    mockApi({ ...base, 'GET /api/stays/khong-co': [404, { title: 'Không tìm thấy', detail: 'Không tìm thấy homestay' }] })
    renderRoute('/stays/khong-co')
    expect(await screen.findByRole('link', { name: 'Xem homestay khác' })).toHaveAttribute('href', '/stays')
  })
})
