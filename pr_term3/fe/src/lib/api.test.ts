import { describe, expect, it } from 'vitest'
import { api, ApiError } from './api'
import { mockApi } from '../test/render'

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

  it('lỗi 5xx không lộ chi tiết máy chủ', async () => {
    mockApi({ 'GET /api/c': [500, { title: 'boom', detail: 'stack trace' }] })
    await expect(api('/c')).rejects.toMatchObject({ status: 500, message: 'Máy chủ trả lỗi 500' })
  })
})
