import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderRoute, session, signedInApi } from '../../test/render'
import { safeNext } from './guards'

const emptyPage = { items: [], page: 1, pageSize: 30, totalItems: 0, totalPages: 1, totals: { income: 0, expense: 0 } }

describe('đăng nhập', () => {
  it('chưa đăng nhập vào /wallets → chuyển sang /login?next=/wallets', async () => {
    mockApi({ 'POST /api/auth/refresh': [401] })
    const { router } = renderRoute('/wallets')

    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.search).toBe('?next=%2Fwallets')
  })

  it('F5 khi cookie còn hạn → vào thẳng app', async () => {
    signedInApi()
    renderRoute('/settings')

    expect(await screen.findByRole('heading', { name: 'Cài đặt' })).toBeInTheDocument()
    expect(screen.getByDisplayValue('An Nguyễn')).toBeInTheDocument()
  })

  it('báo lỗi từng ô khi để trống, không gọi API', async () => {
    const mock = mockApi({ 'POST /api/auth/refresh': [401] })
    renderRoute('/login')

    await userEvent.click(await screen.findByRole('button', { name: 'Đăng nhập' }))

    expect(await screen.findByText('Vui lòng nhập email')).toBeInTheDocument()
    expect(screen.getByText('Vui lòng nhập mật khẩu')).toBeInTheDocument()
    expect(mock.requests.some((r) => r.path === '/api/auth/login')).toBe(false)
  })

  it('sai mật khẩu → hiện thông báo của máy chủ', async () => {
    mockApi({
      'POST /api/auth/refresh': [401],
      'POST /api/auth/login': [401, { title: 'Chưa xác thực', detail: 'Email hoặc mật khẩu không đúng' }],
    })
    renderRoute('/login')

    await userEvent.type(await screen.findByLabelText('Email'), 'an@vidu.vn')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'sai-mat-khau')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Email hoặc mật khẩu không đúng')
  })

  it('đăng nhập đúng → quay về trang định vào (?next)', async () => {
    const mock = mockApi({
      'POST /api/auth/refresh': [401],
      'POST /api/auth/login': [200, session],
      'GET /health': [200, { status: 'ok', database: 'up' }],
      'GET /api/wallets': [200, { items: [], total: 0 }],
      'GET /api/categories': [200, []],
      'GET /api/transactions': [200, emptyPage],
    })
    const { router } = renderRoute('/login?next=%2Ftransactions')

    await userEvent.type(await screen.findByLabelText('Email'), '  an@vidu.vn ')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'matkhau123')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/transactions'))
    expect(mock.requests.find((r) => r.path === '/api/auth/login')?.body).toEqual({ email: 'an@vidu.vn', password: 'matkhau123' })
    // request sau đăng nhập mang access token
    await waitFor(() => expect(mock.requests.find((r) => r.path === '/api/transactions')?.headers.get('Authorization')).toBe('Bearer access-1'))
  })

  it('đăng ký: mật khẩu nhập lại không khớp, email trùng hiện ngay dưới ô email', async () => {
    mockApi({
      'POST /api/auth/refresh': [401],
      'POST /api/auth/register': [409, { title: 'Xung đột dữ liệu', detail: 'Email đã được sử dụng', errors: { email: ['Email đã được sử dụng'] } }],
    })
    renderRoute('/register')

    await userEvent.type(await screen.findByLabelText('Tên hiển thị'), 'An')
    await userEvent.type(screen.getByLabelText('Email'), 'an@vidu.vn')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'matkhau123')
    await userEvent.type(screen.getByLabelText('Nhập lại mật khẩu'), 'matkhau124')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))
    expect(await screen.findByText('Mật khẩu nhập lại không khớp')).toBeInTheDocument()

    await userEvent.clear(screen.getByLabelText('Nhập lại mật khẩu'))
    await userEvent.type(screen.getByLabelText('Nhập lại mật khẩu'), 'matkhau123')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng ký' }))
    expect(await screen.findByText('Email đã được sử dụng')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
  })

  it('đăng xuất → về trang đăng nhập', async () => {
    const mock = signedInApi({ 'POST /api/auth/logout': [204] })
    const { router } = renderRoute('/settings')

    // nút trong trang Cài đặt (sidebar cũng có nút Đăng xuất)
    await userEvent.click(within(await screen.findByRole('main')).getByRole('button', { name: 'Đăng xuất' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(mock.requests.some((r) => r.path === '/api/auth/logout')).toBe(true)
  })

  it.each([
    ['/transactions?month=2026-09', '/transactions?month=2026-09'],
    ['//evil.com', '/'],
    ['https://evil.com', '/'],
    [null, '/'],
  ])('safeNext(%j) = %j', (input, expected) => {
    expect(safeNext(input)).toBe(expected)
  })
})
