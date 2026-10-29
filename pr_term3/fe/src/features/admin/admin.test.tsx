import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { addDays, today } from '../../lib/format'
import type { AdminCalendar, Booking, Payment } from '../../lib/types'
import { adminHomestay, asAdmin, asStaff, booking, homestaySummaries, quote, summary } from '../../test/fixtures'
import { calls, mockApi, renderRoute, type MockRequest } from '../../test/render'

const d0 = today()
const calendarOf = (bookings: AdminCalendar['rooms'][number]['bookings'] = []): AdminCalendar => ({
  from: d0,
  to: addDays(d0, 13),
  rooms: [
    { roomId: 11, homestayId: 1, homestayName: 'Đồi Thông Homestay', name: 'Phòng Gác Mái', status: 'active', prices: Array(14).fill(800_000), bookings, blocks: [] },
    {
      roomId: 12,
      homestayId: 1,
      homestayName: 'Đồi Thông Homestay',
      name: 'Phòng Vườn',
      status: 'active',
      prices: Array(14).fill(700_000),
      bookings: [],
      blocks: [{ id: 5, from: addDays(d0, 3), to: addDays(d0, 4), reason: 'Sửa điều hoà' }],
    },
  ],
})

const catalog = { 'GET /api/admin/homestays': [200, homestaySummaries], 'GET /api/admin/homestays/1': [200, adminHomestay] } as const

describe('lịch phòng', () => {
  it('vẽ thanh đặt phòng + khoá; bấm ô trống → tạo đặt phòng điền sẵn phòng + ngày, hoặc khoá phòng', async () => {
    const fetchMock = mockApi({
      ...asStaff,
      ...catalog,
      'GET /api/admin/calendar': [200, calendarOf([{ id: 100, code: 'HS-AB12-CD34', guestName: 'Nguyễn An', guests: 2, status: 'confirmed', paymentStatus: 'deposit_paid', checkIn: addDays(d0, 1), checkOut: addDays(d0, 3), source: 'web' }])],
      'POST /api/admin/blocks': [201, { id: 6 }],
    })
    renderRoute('/admin/calendar')

    const bar = await screen.findByRole('link', { name: /HS-AB12-CD34 – Nguyễn An, 2 khách, Đã xác nhận/ })
    expect(bar).toHaveAttribute('href', '/admin/bookings/100')
    expect(screen.getByRole('button', { name: /Sửa điều hoà/ })).toBeInTheDocument()
    // đêm đã bán không bấm được
    expect(screen.getByRole('button', { name: new RegExp(`^Phòng Gác Mái, ${dayRe(addDays(d0, 1))}`) })).toBeDisabled()

    const cell = screen.getByRole('button', { name: new RegExp(`^Phòng Gác Mái, ${dayRe(addDays(d0, 5))}`) })
    await userEvent.click(cell)
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('link', { name: /Tạo đặt phòng/ })).toHaveAttribute(
      'href',
      `/admin/bookings/new?homestayId=1&roomId=11&checkIn=${addDays(d0, 5)}&checkOut=${addDays(d0, 6)}`,
    )

    await userEvent.click(within(dialog).getByRole('button', { name: /Khoá phòng/ }))
    await userEvent.type(within(dialog).getByLabelText('Lý do'), 'Sơn lại tường')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Khoá phòng' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(calls(fetchMock, 'POST /api/admin/blocks')[0].body).toEqual({ roomId: 11, from: addDays(d0, 5), to: addDays(d0, 5), reason: 'Sơn lại tường' })
    // lịch được tải lại sau khi khoá
    await waitFor(() => expect(calls(fetchMock, 'GET /api/admin/calendar').length).toBeGreaterThan(1))
  })

  it('đặt phòng đã trả phòng sớm / không đến: đêm đã nhả vẫn bấm được để đặt lại', async () => {
    mockApi({
      ...asStaff,
      ...catalog,
      'GET /api/admin/calendar': [200, calendarOf([{ id: 99, code: 'HS-OUT0-0001', guestName: 'Khách Về Sớm', guests: 1, status: 'checked_out', paymentStatus: 'paid', checkIn: d0, checkOut: addDays(d0, 1), source: 'walk_in' }])],
    })
    renderRoute('/admin/calendar')

    expect(await screen.findByRole('link', { name: /HS-OUT0-0001 – Khách Về Sớm, 1 khách, Đã trả phòng/ })).toHaveClass('h-4')
    expect(screen.getByRole('button', { name: new RegExp(`^Phòng Gác Mái, ${dayRe(d0)}`) })).toBeEnabled()
  })

  it('chuyển 30 ngày / tuần sau cập nhật URL và khoảng ngày gọi API', async () => {
    const fetchMock = mockApi({ ...asStaff, ...catalog, 'GET /api/admin/calendar': [200, calendarOf()] })
    const { router } = renderRoute('/admin/calendar')

    await screen.findByRole('grid')
    await userEvent.click(screen.getByRole('button', { name: '30 ngày' }))
    await userEvent.click(screen.getByRole('button', { name: 'Tới 7 ngày' }))
    expect(Object.fromEntries(new URLSearchParams(router.state.location.search))).toEqual({ from: addDays(d0, 7), days: '30' })
    await waitFor(() => {
      const last = calls(fetchMock, 'GET /api/admin/calendar').at(-1)!.url.searchParams
      expect([last.get('from'), last.get('to')]).toEqual([addDays(d0, 7), addDays(d0, 36)])
    })
  })
})

/** dayLabel → regex an toàn ("T5, 12/11"). */
function dayRe(date: string) {
  const [, m, d] = date.split('-')
  return `\\S+, ${d}/${m}`
}

describe('luồng lễ tân (DoD): tạo đặt phòng từ lịch → cọc → check-in → thu đủ → check-out', () => {
  it('đi trọn vòng đời, mỗi thao tác cập nhật chi tiết + làm mới lịch', async () => {
    let state: Booking | null = null
    const payments: Payment[] = []
    const withMoney = (b: Booking): Booking => {
      const paid = payments.reduce((s, p) => s + (p.kind === 'refund' ? -p.amount : p.amount), 0)
      const amountDue = ['checked_out', 'cancelled'].includes(b.status) ? 0 : b.total - paid
      return { ...b, payments: [...payments], money: { paid, extras: 0, refunded: 0, amountDue, pendingRefund: 0, status: paid >= b.total ? 'paid' : paid >= b.depositAmount ? 'deposit_paid' : 'unpaid' } }
    }
    const pay = (req: MockRequest) => {
      const body = req.body as { kind: Payment['kind']; method: Payment['method']; amount: number }
      payments.push({ id: payments.length + 1, ...body, paidAt: new Date().toISOString(), note: null, recordedBy: 'Lễ Tân' })
      if (state!.status === 'pending' && body.amount >= state!.depositAmount) state = { ...state!, status: 'confirmed' }
      return [200, (state = withMoney(state!))] as const
    }
    const transition = (to: Booking['status']) => () => [200, (state = withMoney({ ...state!, status: to }))] as const

    const fetchMock = mockApi({
      ...asStaff,
      ...catalog,
      'GET /api/admin/calendar': [200, calendarOf()],
      'POST /api/quotes': [200, quote(1_700_000, { checkIn: d0, checkOut: addDays(d0, 2) })],
      'POST /api/admin/bookings': (req) => {
        const body = req.body as { payment: { amount: number } | null; guestName: string; guestPhone: string }
        state = withMoney(booking({ status: 'pending', checkIn: d0, checkOut: addDays(d0, 2), guestName: body.guestName, guestPhone: body.guestPhone, source: 'phone', transfer: null, userId: null, internalNote: null }))
        if (body.payment) pay({ ...req, body: { kind: 'deposit', method: 'cash', ...body.payment } })
        return [201, state]
      },
      'GET /api/admin/bookings/100': () => [200, state],
      'POST /api/admin/bookings/100/check-in': transition('checked_in'),
      'POST /api/admin/bookings/100/payments': pay,
      'POST /api/admin/bookings/100/check-out': (req) =>
        state!.money.amountDue > 0 && !(req.body as { reason: string | null }).reason
          ? [409, { title: 'Xung đột', detail: 'Khách còn phải thu – thu đủ hoặc nhập lý do bỏ qua' }]
          : transition('checked_out')(),
    })
    const { router } = renderRoute(`/admin/bookings/new?homestayId=1&roomId=11&checkIn=${d0}&checkOut=${addDays(d0, 2)}`)

    // 1. Tạo đặt phòng (điền sẵn phòng + ngày từ lịch) kèm cọc tiền mặt
    await screen.findByRole('option', { name: /Phòng Gác Mái/ })
    expect(screen.getByRole('combobox', { name: 'Phòng' })).toHaveValue('11')
    expect(await screen.findByText('Tổng cộng')).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('Họ tên khách'), 'Khách Gọi Điện')
    await userEvent.type(screen.getByLabelText('Số điện thoại'), '0987 654 321')
    await userEvent.click(screen.getByRole('checkbox', { name: 'Thu tiền ngay' }))
    expect(screen.getByLabelText('Số tiền')).toHaveValue('510.000') // mặc định = tiền cọc
    await userEvent.click(screen.getByRole('button', { name: 'Tạo đặt phòng' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/bookings/100'))
    expect(calls(fetchMock, 'POST /api/admin/bookings')[0].body).toMatchObject({
      roomId: 11, checkIn: d0, guestPhone: '0987654321', source: 'phone', payment: { kind: 'deposit', method: 'cash', amount: 510_000 },
    })
    expect(await screen.findByText('Đã xác nhận', { selector: 'span' })).toBeInTheDocument()

    // 2. Check-in
    await userEvent.click(screen.getByRole('button', { name: 'Check-in' }))
    expect(await screen.findByText('Đang ở', { selector: 'span' })).toBeInTheDocument()

    // 3. Check-out khi còn nợ → yêu cầu lý do; thu đủ trước
    await userEvent.click(screen.getByRole('button', { name: 'Check-out' }))
    let dialog = await screen.findByRole('dialog', { name: 'Check-out' })
    expect(within(dialog).getByText(/Khách còn phải thu 1\.190\.000/)).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Trả phòng' }))
    expect(within(dialog).getByText('Vui lòng nhập lý do')).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: 'Quay lại' }))

    await userEvent.click(screen.getByRole('button', { name: 'Ghi khoản thu' }))
    dialog = await screen.findByRole('dialog', { name: 'Ghi khoản thu / hoàn' })
    expect(within(dialog).getByLabelText('Số tiền')).toHaveValue('1.190.000') // mặc định = còn phải thu
    await userEvent.click(within(dialog).getByRole('button', { name: 'Ghi khoản' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(screen.getByText('Đã thanh toán đủ')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Check-out' }))
    dialog = await screen.findByRole('dialog', { name: 'Check-out' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Trả phòng' }))
    expect(await screen.findByText('Đã trả phòng', { selector: 'span' })).toBeInTheDocument()
    expect(calls(fetchMock, 'POST /api/admin/bookings/100/check-out').at(-1)!.body).toEqual({ reason: null })
    expect(calls(fetchMock, 'POST /api/admin/bookings/100/payments')[0].body).toMatchObject({ kind: 'balance', amount: 1_190_000 })
    // mỗi thao tác làm mới lịch phòng (invalidateBookings) – lịch đã mở trong phiên nên được nạp lại khi quay về
    await router.navigate('/admin/calendar')
    expect(await screen.findByRole('grid')).toBeInTheDocument()
  })

  it('xác nhận khi chưa đủ cọc bắt buộc lý do; huỷ: xem trước + sửa số hoàn + bắt buộc lý do', async () => {
    const pending = booking({ status: 'pending', transfer: null, payments: [], internalNote: 'Khách quen' })
    let current = pending
    const fetchMock = mockApi({
      ...asStaff,
      ...catalog,
      'GET /api/admin/bookings/100': () => [200, current],
      'POST /api/admin/bookings/100/confirm': () => [200, (current = { ...pending, status: 'confirmed' })],
      'GET /api/admin/bookings/100/cancel-preview': [200, { canCancel: true, reason: null, daysBefore: 10, paid: 0, refundPercent: 100, refundAmount: 0 }],
      'POST /api/admin/bookings/100/cancel': [200, { ...pending, status: 'cancelled' }],
    })
    renderRoute('/admin/bookings/100')

    expect(await screen.findByText('Khách quen')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Xác nhận' }))
    let dialog = await screen.findByRole('dialog', { name: 'Xác nhận đặt phòng' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận' }))
    expect(within(dialog).getByText('Vui lòng nhập lý do')).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText('Lý do'), 'Khách quen, chuyển sau')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Xác nhận' }))
    expect(await screen.findByText('Đã xác nhận', { selector: 'span' })).toBeInTheDocument()
    expect(calls(fetchMock, 'POST /api/admin/bookings/100/confirm')[0].body).toEqual({ reason: 'Khách quen, chuyển sau' })

    await userEvent.click(screen.getByRole('button', { name: 'Huỷ đặt phòng' }))
    dialog = await screen.findByRole('dialog', { name: 'Huỷ HS-AB12-CD34' })
    await within(dialog).findByLabelText('Số hoàn chốt cho khách')
    await userEvent.type(within(dialog).getByLabelText('Số hoàn chốt cho khách'), '200000')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Huỷ đặt phòng' }))
    expect(within(dialog).getByText('Vui lòng nhập lý do huỷ')).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText('Lý do huỷ'), 'Homestay mất điện')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Huỷ đặt phòng' }))
    await waitFor(() => expect(calls(fetchMock, 'POST /api/admin/bookings/100/cancel')).toHaveLength(1))
    expect(calls(fetchMock, 'POST /api/admin/bookings/100/cancel')[0].body).toEqual({ reason: 'Homestay mất điện', refundAmount: 200_000 })
  })
})

describe('danh sách, hôm nay, khách', () => {
  it('danh sách đặt phòng: bộ lọc trên URL → query API', async () => {
    const fetchMock = mockApi({ ...asStaff, ...catalog, 'GET /api/admin/bookings': [200, { items: [summary()], page: 1, pageSize: 20, total: 1 }] })
    const { router } = renderRoute('/admin/bookings')

    expect((await screen.findAllByText('HS-AB12-CD34')).length).toBeGreaterThan(0)
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Trạng thái' }), 'closed')
    await userEvent.type(screen.getByLabelText('Tìm mã / tên / SĐT'), '0912{Enter}')
    const params = new URLSearchParams(router.state.location.search)
    expect([params.get('status'), params.get('q')]).toEqual(['cancelled,expired,no_show', '0912'])
    await waitFor(() => expect(calls(fetchMock, 'GET /api/admin/bookings').at(-1)!.url.search).toBe('?status=cancelled%2Cexpired%2Cno_show&q=0912'))
  })

  it('hôm nay: đếm khách đến / đi / đang ở / chờ cọc + công suất', async () => {
    mockApi({
      ...asStaff,
      'GET /api/admin/dashboard': [
        200,
        {
          date: d0,
          arrivals: [summary({ status: 'confirmed', guestName: 'Khách Đến' })],
          departures: [],
          inHouse: [summary({ id: 101, status: 'checked_in', guestName: 'Khách Đang Ở' })],
          pendingHolds: [summary({ id: 102, guestName: 'Khách Chờ Cọc' })],
          next7Days: [{ date: d0, sold: 3, sellable: 4, rate: 0.75 }],
        },
      ],
    })
    renderRoute('/admin')

    expect(await screen.findByRole('link', { name: /Khách Đến/ })).toHaveAttribute('href', '/admin/bookings/100')
    expect(screen.getByRole('link', { name: /Khách Chờ Cọc/ })).toHaveTextContent(/Còn \d+:\d{2}:\d{2} giữ chỗ/)
    expect(screen.getByText('75%')).toBeInTheDocument()
    expect(screen.getByText('Không có khách trả phòng')).toBeInTheDocument()
  })

  it('khách: gộp theo SĐT, bấm → lịch sử lưu trú', async () => {
    mockApi({
      ...asStaff,
      'GET /api/admin/guests': [200, { items: [{ phone: '0912345678', name: 'Nguyễn An', email: null, hasAccount: true, bookings: 3, stays: 2, upcoming: 1, cancelled: 0, paid: 4_000_000, lastCheckIn: d0 }], page: 1, pageSize: 20, total: 1 }],
    })
    renderRoute('/admin/guests')

    const link = await screen.findByRole('link', { name: /Nguyễn An/ })
    expect(link).toHaveAttribute('href', '/admin/bookings?q=0912345678')
    expect(link).toHaveTextContent('3 lần đặt · 2 lần ở · 1 sắp tới')
  })
})

describe('homestay (admin)', () => {
  it('ảnh: nút mũi tên / đặt bìa lưu thứ tự mới; phòng: thêm qua hộp thoại', async () => {
    const fetchMock = mockApi({
      ...asAdmin,
      ...catalog,
      'PUT /api/admin/homestays/1/photos/order': (req) => [200, (req.body as { photoIds: number[] }).photoIds.map((id, i) => ({ id, url: '', width: 1, height: 1, sortOrder: i }))],
      'POST /api/admin/homestays/1/rooms': [201, { ...adminHomestay.rooms[0], id: 13, name: 'Phòng Mới' }],
    })
    renderRoute('/admin/homestays/1')

    await userEvent.click(await screen.findByRole('button', { name: 'Đặt ảnh 3 làm ảnh bìa' }))
    await waitFor(() => expect(calls(fetchMock, 'PUT /api/admin/homestays/1/photos/order')[0].body).toEqual({ photoIds: [3, 1, 2] }))
    await userEvent.click(screen.getByRole('button', { name: 'Đưa ảnh 1 ra sau' }))
    await waitFor(() => expect(calls(fetchMock, 'PUT /api/admin/homestays/1/photos/order')[1].body).toEqual({ photoIds: [2, 1, 3] }))

    await userEvent.click(screen.getByRole('button', { name: 'Thêm phòng' }))
    const dialog = await screen.findByRole('dialog', { name: 'Thêm phòng' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm phòng' }))
    expect(within(dialog).getByText('Vui lòng nhập tên phòng')).toBeInTheDocument()
    await userEvent.type(within(dialog).getByLabelText('Tên phòng'), 'Phòng Mới')
    await userEvent.type(within(dialog).getByLabelText('Giá cơ bản / đêm'), '900000')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Thêm phòng' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(calls(fetchMock, 'POST /api/admin/homestays/1/rooms')[0].body).toMatchObject({ name: 'Phòng Mới', basePrice: 900_000, baseGuests: 2, maxGuests: 2 })
  })

  it('chính sách: hoàn một phần > huỷ miễn phí bị chặn trước khi gửi', async () => {
    const fetchMock = mockApi({ ...asAdmin, ...catalog })
    renderRoute('/admin/homestays/1')

    const field = await screen.findByLabelText('Hoàn một phần trước (ngày)')
    await userEvent.clear(field)
    await userEvent.type(field, '9')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu thông tin' }))
    expect(screen.getByText('Số ngày hoàn một phần phải ≤ số ngày huỷ miễn phí')).toBeInTheDocument()
    expect(calls(fetchMock, 'PUT /api/admin/homestays/1')).toHaveLength(0)
  })
})
