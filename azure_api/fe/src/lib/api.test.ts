import { afterEach, describe, expect, it, vi } from 'vitest'
import { mockApi, session } from '../test/render'
import { api, ApiError, onSessionChange, setAccessToken } from './api'

afterEach(() => setAccessToken(null))

describe('api', () => {
  it('gắn Bearer token và trả JSON', async () => {
    setAccessToken('abc')
    const mock = mockApi({ 'GET /api/me': [200, { id: 'u1' }] })

    const me = await api<{ id: string }>('/me')

    expect(me.id).toBe('u1')
    expect(mock.requests[0].headers.get('Authorization')).toBe('Bearer abc')
  })

  it('401 → refresh bằng cookie → gửi lại với token mới', async () => {
    setAccessToken('het-han')
    const mock = mockApi({
      'GET /api/me': (req) => (req.headers.get('Authorization') === 'Bearer access-1' ? [200, { ok: true }] : [401]),
      'POST /api/auth/refresh': [200, session],
    })

    await expect(api('/me')).resolves.toEqual({ ok: true })
    expect(mock.requests.map((r) => `${r.method} ${r.path}`)).toEqual(['GET /api/me', 'POST /api/auth/refresh', 'GET /api/me'])
  })

  it('nhiều request cùng gặp 401 chỉ gọi refresh 1 lần', async () => {
    setAccessToken('het-han')
    const mock = mockApi({
      'GET /api/wallets': (req) => (req.headers.get('Authorization') === 'Bearer access-1' ? [200, []] : [401]),
      'POST /api/auth/refresh': async () => {
        await new Promise((r) => setTimeout(r, 20))
        return [200, session]
      },
    })

    await Promise.all([api('/wallets'), api('/wallets'), api('/wallets')])

    expect(mock.requests.filter((r) => r.path === '/api/auth/refresh')).toHaveLength(1)
  })

  it('refresh thất bại → báo hết phiên (null) và ném 401', async () => {
    mockApi({ 'GET /api/me': [401], 'POST /api/auth/refresh': [401, { title: 'Chưa xác thực' }] })
    const listener = vi.fn()
    const unsubscribe = onSessionChange(listener)

    await expect(api('/me')).rejects.toMatchObject({ status: 401 })
    expect(listener).toHaveBeenCalledWith(null)
    unsubscribe()
  })

  it('auth: false (đăng nhập) không thử refresh khi 401', async () => {
    const mock = mockApi({ 'POST /api/auth/login': [401, { title: 'Chưa xác thực', detail: 'Email hoặc mật khẩu không đúng' }] })

    await expect(api('/auth/login', { method: 'POST', body: {}, auth: false })).rejects.toThrow('Email hoặc mật khẩu không đúng')
    expect(mock.requests).toHaveLength(1)
  })

  it('đọc ProblemDetails thành ApiError có lỗi từng ô', async () => {
    setAccessToken('abc')
    mockApi({ 'POST /api/wallets': [409, { title: 'Xung đột dữ liệu', detail: 'Tên ví đã tồn tại', errors: { name: ['Tên ví đã tồn tại'] } }] })

    const error = await api('/wallets', { method: 'POST', body: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, message: 'Tên ví đã tồn tại', errors: { name: ['Tên ví đã tồn tại'] } })
  })

  it('mất mạng → ApiError status 0 với thông báo tiếng Việt', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(api('/me')).rejects.toMatchObject({ status: 0, title: 'Không kết nối được máy chủ' })
  })

  it('204 → undefined', async () => {
    setAccessToken('abc')
    mockApi({ 'DELETE /api/wallets/1': [204] })

    await expect(api('/wallets/1', { method: 'DELETE' })).resolves.toBeUndefined()
  })
})
