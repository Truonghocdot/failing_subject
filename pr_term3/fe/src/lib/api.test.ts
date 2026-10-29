import { describe, expect, it } from 'vitest'
import { calls, mockApi } from '../test/render'
import { session } from '../test/fixtures'
import { api, ApiError, refreshSession, setAccessToken } from './api'

describe('api', () => {
  it('trả JSON khi thành công', async () => {
    mockApi({ 'GET /api/provinces': [200, ['Đà Lạt']] })
    await expect(api<string[]>('/provinces')).resolves.toEqual(['Đà Lạt'])
  })

  it('ProblemDetails → ApiError, ưu tiên detail rồi tới lỗi từng ô', async () => {
    mockApi({
      'GET /api/a': [409, { title: 'Xung đột', detail: 'Phòng vừa có người đặt' }],
      'GET /api/b': [400, { title: 'Dữ liệu không hợp lệ', errors: { checkOut: ['Ngày trả phòng phải sau ngày nhận phòng'] } }],
    })

    await expect(api('/a')).rejects.toMatchObject({ status: 409, message: 'Phòng vừa có người đặt' })
    const error = await api('/b').catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).message).toBe('Ngày trả phòng phải sau ngày nhận phòng')
    expect((error as ApiError).errors.checkOut).toHaveLength(1)
  })

  it('giữ phần mở rộng của ProblemDetails (VD báo giá mới khi giá đổi)', async () => {
    mockApi({ 'POST /api/bookings': [409, { type: 'x', status: 409, title: 'Xung đột', detail: 'Giá đã thay đổi', traceId: 't', quote: { total: 2 } }] })
    const error = (await api('/bookings', { method: 'POST', body: {} }).catch((e: unknown) => e)) as ApiError
    expect(error.extensions).toEqual({ quote: { total: 2 } })
  })

  it('lỗi 5xx không lộ chi tiết máy chủ', async () => {
    mockApi({ 'GET /api/c': [500, { title: 'boom', detail: 'stack trace' }] })
    await expect(api('/c')).rejects.toMatchObject({ status: 500, message: 'Máy chủ trả lỗi 500' })
  })

  it('401 → refresh đúng 1 lần cho nhiều request song song rồi gửi lại với token mới', async () => {
    setAccessToken('cu')
    const fetchMock = mockApi({
      'POST /api/auth/refresh': [200, session],
      'GET /api/bookings': (req) => (req.headers.Authorization === `Bearer ${session.accessToken}` ? [200, []] : [401, { title: 'Hết phiên' }]),
    })

    await expect(Promise.all([api('/bookings'), api('/bookings'), api('/bookings')])).resolves.toEqual([[], [], []])
    expect(calls(fetchMock, 'POST /api/auth/refresh')).toHaveLength(1)
  })

  it('refresh thất bại → ném 401, không lặp vô hạn; login không thử refresh', async () => {
    const fetchMock = mockApi({ 'POST /api/auth/refresh': [401, { title: 'Hết phiên' }], 'GET /api/me': [401, {}], 'POST /api/auth/login': [401, { title: 'Sai', detail: 'Email hoặc mật khẩu không đúng' }] })

    await expect(api('/me')).rejects.toMatchObject({ status: 401 })
    await expect(api('/auth/login', { method: 'POST', body: {}, auth: false })).rejects.toMatchObject({ message: 'Email hoặc mật khẩu không đúng' })
    expect(calls(fetchMock, 'POST /api/auth/refresh')).toHaveLength(1)
    await expect(refreshSession()).resolves.toBeNull()
  })
})
