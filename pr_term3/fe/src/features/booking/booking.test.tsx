import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { base, booking, checkIn, checkOut, loggedIn, quote, session, stay } from '../../test/fixtures'
import { calls, mockApi, renderRoute } from '../../test/render'

const bookUrl = `/book/11?stay=doi-thong&checkIn=${checkIn}&checkOut=${checkOut}&guests=2`

describe('đặt phòng', () => {
  it('chưa đăng nhập → /login giữ nguyên ngày; đăng nhập xong quay lại đúng trang đặt', async () => {
    mockApi({ ...base, 'POST /api/auth/login': [200, session], 'GET /api/stays/doi-thong': [200, stay], 'POST /api/quotes': [200, quote()] })
    const { router } = renderRoute(bookUrl)

    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(screen.getByText(/ngày và số khách bạn chọn được giữ nguyên/)).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText('Email'), 'an@example.com')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'matkhau123')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    expect(await screen.findByRole('heading', { name: 'Xác nhận đặt phòng' })).toBeInTheDocument()
    expect(router.state.location.pathname + router.state.location.search).toBe(bookUrl)
  })

  it('giá đổi (409) → hiện giá mới, giữ thông tin đã nhập; đặt lại → mã + hướng dẫn chuyển khoản + đồng hồ giữ chỗ', async () => {
    let attempt = 0
    const created = booking({ total: 1_850_000, depositAmount: 555_000 })
    const fetchMock = mockApi({
      ...loggedIn,
      'GET /api/stays/doi-thong': [200, stay],
      'POST /api/quotes': [200, quote()],
      'POST /api/bookings': () =>
        ++attempt === 1
          ? [409, { title: 'Xung đột', detail: 'Giá đã thay đổi: tổng mới 1.850.000 đ – vui lòng xem lại và xác nhận', quote: quote(1_850_000) }]
          : [201, created],
      'GET /api/bookings': [200, []],
    })
    const { router } = renderRoute(bookUrl)

    // thông tin tài khoản điền sẵn
    const name = await screen.findByLabelText('Họ tên')
    expect(name).toHaveValue('Nguyễn An')
    expect(screen.getByLabelText('Số điện thoại')).toHaveValue('0912345678')
    expect(await screen.findByText(/1\.700\.000/)).toBeInTheDocument()

    await userEvent.clear(name)
    await userEvent.type(name, 'Trần Bình')
    await userEvent.type(screen.getByLabelText(/Ghi chú cho chủ nhà/), 'Đến muộn')
    await userEvent.click(screen.getByRole('button', { name: 'Xác nhận đặt phòng' }))
    expect(await screen.findByText('Vui lòng đồng ý chính sách đặt cọc và huỷ phòng')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('checkbox', { name: /Tôi đồng ý/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Xác nhận đặt phòng' }))

    expect(await screen.findByText(/Giá đã thay đổi: tổng mới 1\.850\.000/)).toBeInTheDocument()
    expect(screen.getAllByText(/1\.850\.000/).length).toBeGreaterThan(1) // bảng giá đã đổi theo báo giá mới
    expect(screen.getByLabelText('Họ tên')).toHaveValue('Trần Bình')
    expect(screen.getByLabelText(/Ghi chú cho chủ nhà/)).toHaveValue('Đến muộn')
    expect(calls(fetchMock, 'POST /api/bookings')[0].body).toMatchObject({ expectedTotal: 1_700_000, guestName: 'Trần Bình', note: 'Đến muộn' })

    await userEvent.click(screen.getByRole('button', { name: 'Xác nhận đặt phòng' }))
    expect(await screen.findByText(/Đặt phòng thành công/)).toBeInTheDocument()
    expect(calls(fetchMock, 'POST /api/bookings')[1].body).toMatchObject({ expectedTotal: 1_850_000 })
    expect(router.state.location.pathname).toBe('/bookings/HS-AB12-CD34')

    expect(screen.getByRole('heading', { level: 1, name: 'HS-AB12-CD34' })).toBeInTheDocument()
    expect(screen.getByText('Đang giữ chỗ – chờ chuyển khoản cọc')).toBeInTheDocument()
    expect(screen.getByText('Vietcombank')).toBeInTheDocument()
    expect(screen.getByText('0123456789')).toBeInTheDocument()
    expect(screen.getByText(/^1:5\d:\d{2}$|^2:00:00$/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sao chép nội dung chuyển khoản' })).toBeInTheDocument()
  })

  it('phòng vừa bị người khác đặt (409 không kèm giá) → báo lỗi, làm mới báo giá, không cho đặt tiếp', async () => {
    let quotes = 0
    mockApi({
      ...loggedIn,
      'GET /api/stays/doi-thong': [200, stay],
      'POST /api/quotes': () => [200, quote(1_700_000, { available: ++quotes === 1 })],
      'POST /api/bookings': [409, { title: 'Xung đột', detail: 'Phòng vừa có người đặt trong khoảng ngày này – vui lòng chọn ngày khác' }],
    })
    renderRoute(bookUrl)

    await screen.findByText(/1\.700\.000/)
    await userEvent.click(screen.getByRole('checkbox', { name: /Tôi đồng ý/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Xác nhận đặt phòng' }))

    expect(await screen.findByText(/Phòng vừa có người đặt trong khoảng ngày này – vui lòng chọn ngày khác/)).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Xác nhận đặt phòng' })).toBeDisabled())
    expect(screen.getByLabelText('Họ tên')).toHaveValue('Nguyễn An')
  })

  it('URL thiếu / sai ngày → hướng dẫn chọn lại', async () => {
    mockApi({ ...loggedIn })
    renderRoute('/book/11?stay=doi-thong&checkIn=2020-01-01&checkOut=2020-01-02')
    expect(await screen.findByText(/Ngày nhận – trả phòng không hợp lệ hoặc đã qua/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Chọn lại phòng và ngày/ })).toHaveAttribute('href', '/stays/doi-thong')
  })
})

describe('đặt phòng của tôi', () => {
  it('huỷ: xem trước số hoàn → xác nhận → trạng thái Đã huỷ', async () => {
    const confirmed = booking({ status: 'confirmed', transfer: null, holdUntil: null, money: { paid: 510_000, extras: 0, refunded: 0, amountDue: 1_190_000, pendingRefund: 0, status: 'deposit_paid' } })
    const fetchMock = mockApi({
      ...loggedIn,
      'GET /api/bookings/HS-AB12-CD34': [200, confirmed],
      'GET /api/bookings/HS-AB12-CD34/cancel-preview': [200, { canCancel: true, reason: null, daysBefore: 5, paid: 510_000, refundPercent: 50, refundAmount: 255_000 }],
      'POST /api/bookings/HS-AB12-CD34/cancel': [
        200,
        booking({ ...confirmed, status: 'cancelled', refundAmount: 255_000, cancelReason: 'Đổi kế hoạch', cancelledAt: new Date().toISOString(), money: { ...confirmed.money, pendingRefund: 255_000, amountDue: 0 } }),
      ],
      'GET /api/bookings': [200, []],
    })
    renderRoute('/bookings/HS-AB12-CD34')

    expect(await screen.findByText(/Đặt phòng đã được xác nhận/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Huỷ đặt phòng' }))

    const dialog = await screen.findByRole('dialog', { name: 'Huỷ đặt phòng HS-AB12-CD34' })
    expect(await within(dialog).findByText('Được hoàn (50%)')).toBeInTheDocument()
    expect(within(dialog).getByText(/255\.000/)).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText('Lý do (không bắt buộc)'), 'Đổi kế hoạch')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận huỷ' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByText('Đã huỷ', { selector: 'span' })).toBeInTheDocument()
    expect(screen.getByText(/chủ nhà sẽ chuyển trả/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Huỷ đặt phòng' })).not.toBeInTheDocument()
    expect(calls(fetchMock, 'POST /api/bookings/HS-AB12-CD34/cancel')[0].body).toEqual({ reason: 'Đổi kế hoạch' })
  })

  it('không được huỷ (đã tới ngày nhận phòng) → nêu lý do, khoá nút xác nhận', async () => {
    mockApi({
      ...loggedIn,
      'GET /api/bookings/HS-AB12-CD34': [200, booking({ status: 'confirmed' })],
      'GET /api/bookings/HS-AB12-CD34/cancel-preview': [200, { canCancel: false, reason: 'Đã tới ngày nhận phòng – vui lòng liên hệ chủ nhà', daysBefore: 0, paid: 0, refundPercent: 0, refundAmount: 0 }],
    })
    renderRoute('/bookings/HS-AB12-CD34')

    await userEvent.click(await screen.findByRole('button', { name: 'Huỷ đặt phòng' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('Đã tới ngày nhận phòng – vui lòng liên hệ chủ nhà')).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Xác nhận huỷ' })).toBeDisabled()
  })

  it('danh sách chia tab Sắp tới · Đã ở · Đã huỷ, tab nằm trên URL', async () => {
    const summary = (code: string, status: string, ci: string) => ({ ...booking({ code, status: status as never, checkIn: ci }), id: Math.random() })
    mockApi({
      ...loggedIn,
      'GET /api/bookings': [200, [summary('HS-0000-0001', 'pending', checkIn), summary('HS-0000-0002', 'checked_out', '2026-01-01'), summary('HS-0000-0003', 'expired', checkIn)]],
    })
    const { router } = renderRoute('/bookings')

    expect(await screen.findByRole('tab', { name: 'Sắp tới (1)' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('link', { name: /HS-0000-0001/ })).toHaveAttribute('href', '/bookings/HS-0000-0001')

    await userEvent.click(screen.getByRole('tab', { name: 'Đã huỷ (1)' }))
    expect(router.state.location.search).toBe('?tab=cancelled')
    expect(screen.getByText('HS-0000-0003')).toBeInTheDocument()
    expect(screen.queryByText('HS-0000-0001')).not.toBeInTheDocument()
  })

  it('tra cứu bằng mã + số điện thoại (không cần đăng nhập), chuẩn hoá SĐT', async () => {
    const fetchMock = mockApi({ ...base, 'POST /api/bookings/lookup': [200, booking()] })
    renderRoute('/lookup')

    await userEvent.type(screen.getByLabelText('Mã đặt phòng'), 'hs-ab12-cd34')
    await userEvent.type(screen.getByLabelText('Số điện thoại'), '+84 912 345 678')
    await userEvent.click(screen.getByRole('button', { name: 'Tra cứu' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'HS-AB12-CD34' })).toBeInTheDocument()
    expect(calls(fetchMock, 'POST /api/bookings/lookup')[0].body).toEqual({ code: 'HS-AB12-CD34', phone: '0912345678' })
    const result = screen.getByRole('region', { name: 'Kết quả tra cứu' })
    expect(within(result).getByRole('link', { name: 'Đăng nhập' })).toHaveAttribute('href', `/login?next=${encodeURIComponent('/bookings/HS-AB12-CD34')}`)
  })

  it('tra cứu sai → báo lỗi chung, không lộ đặt phòng có tồn tại hay không', async () => {
    mockApi({ ...base, 'POST /api/bookings/lookup': [404, { title: 'Không tìm thấy', detail: 'Không tìm thấy đặt phòng' }] })
    renderRoute('/lookup')

    await userEvent.type(screen.getByLabelText('Mã đặt phòng'), 'HS-XXXX-XXXX')
    await userEvent.type(screen.getByLabelText('Số điện thoại'), '0900000000')
    await userEvent.click(screen.getByRole('button', { name: 'Tra cứu' }))
    expect(await screen.findByText('Không tìm thấy đặt phòng')).toBeInTheDocument()
  })
})

describe('tài khoản', () => {
  it('đã đăng nhập: thanh trên hiện tên, trang tài khoản lưu được thông tin', async () => {
    const fetchMock = mockApi({ ...loggedIn, 'PUT /api/me': [200, { ...session.user, fullName: 'Nguyễn Văn An' }] })
    renderRoute('/account')

    expect(await screen.findByRole('heading', { name: 'Tài khoản' })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Điều hướng khách' })
    expect(within(nav).getByRole('link', { name: 'Nguyễn An' })).toHaveAttribute('href', '/account')

    const name = screen.getByLabelText('Họ tên')
    await userEvent.clear(name)
    await userEvent.type(name, 'Nguyễn Văn An')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu thông tin' }))

    expect(await screen.findByText('Đã lưu thông tin')).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: 'Nguyễn Văn An' })).toBeInTheDocument()
    expect(calls(fetchMock, 'PUT /api/me')[0].body).toEqual({ fullName: 'Nguyễn Văn An', phone: '0912345678' })
  })

  it('đăng ký: kiểm tra dữ liệu như backend, lỗi email trùng gắn vào ô', async () => {
    mockApi({ ...base, 'POST /api/auth/register': [409, { title: 'Xung đột', detail: 'Email đã được sử dụng', errors: { email: ['Email đã được sử dụng'] } }] })
    renderRoute('/register')

    await userEvent.type(await screen.findByLabelText('Họ tên'), 'An')
    await userEvent.type(screen.getByLabelText('Email'), 'an@example.com')
    await userEvent.type(screen.getByLabelText(/Số điện thoại/), '12345')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'ngan')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))
    expect(await screen.findByText('Số điện thoại không hợp lệ (VD: 0912 345 678)')).toBeInTheDocument()
    expect(screen.getByText('Mật khẩu phải từ 8 đến 100 ký tự')).toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText(/Số điện thoại/))
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'dai-hon-8')
    await userEvent.type(screen.getByLabelText('Nhập lại mật khẩu'), 'ngandai-hon-8')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))
    expect(await screen.findByText('Email đã được sử dụng')).toBeInTheDocument()
  })
})
