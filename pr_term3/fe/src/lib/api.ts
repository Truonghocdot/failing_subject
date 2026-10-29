import { env } from '../app/env'
import type { AuthResponse } from './types'

/** Lỗi từ API (ProblemDetails – RFC 9457). `errors` là lỗi từng ô; `extensions` là phần thêm (VD "quote" khi giá đổi). */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly errors: Record<string, string[]>
  readonly extensions: Record<string, unknown>

  constructor(status: number, title: string, detail?: string, errors?: Record<string, string[]>, extensions?: Record<string, unknown>) {
    // 400 không có detail → lỗi đầu tiên trong errors dễ hiểu hơn tiêu đề chung
    super(detail || Object.values(errors ?? {})[0]?.[0] || title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.errors = errors ?? {}
    this.extensions = extensions ?? {}
  }
}

type ProblemDetails = { title?: string; detail?: string; errors?: Record<string, string[]>; [key: string]: unknown }

// Access token chỉ nằm trong RAM; refresh token ở cookie HttpOnly (Path=/api/auth) mà JS không đọc được
let accessToken: string | null = null
let refreshing: Promise<AuthResponse | null> | null = null
const sessionListeners = new Set<(session: AuthResponse | null) => void>()

export function setAccessToken(token: string | null) {
  accessToken = token
}

/** Nhận thông báo khi phiên đổi do refresh ngầm (token mới) hoặc hết hạn hẳn (null). */
export function onSessionChange(listener: (session: AuthResponse | null) => void) {
  sessionListeners.add(listener)
  return () => {
    sessionListeners.delete(listener)
  }
}

/**
 * Đổi cookie refresh lấy access token mới. Single-flight: nhiều request cùng gặp 401
 * (hoặc StrictMode gọi 2 lần lúc khởi động) chỉ gửi đúng 1 request refresh – backend xoay vòng token
 * nên 2 request refresh song song sẽ làm 1 cái dùng token cũ.
 */
export function refreshSession(): Promise<AuthResponse | null> {
  refreshing ??= (async () => {
    try {
      const res = await fetch(`${env.apiBaseUrl}/auth/refresh`, { method: 'POST', credentials: 'same-origin' })
      const session = res.ok ? ((await res.json()) as AuthResponse) : null
      setAccessToken(session?.accessToken ?? null)
      sessionListeners.forEach((l) => l(session))
      return session
    } catch {
      return null // mất mạng: không coi là hết phiên
    } finally {
      refreshing = null
    }
  })()
  return refreshing
}

export type ApiOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** multipart (tải ảnh) – trình duyệt tự đặt Content-Type kèm boundary. */
  form?: FormData
  signal?: AbortSignal
  /** false cho /auth/login, /auth/register, tra cứu: không gắn token, không thử refresh khi 401. */
  auth?: boolean
}

async function send(path: string, { method = 'GET', body, form, signal, auth = true }: ApiOptions) {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`
  try {
    return await fetch(`${env.apiBaseUrl}${path}`, {
      method,
      headers,
      body: form ?? (body === undefined ? undefined : JSON.stringify(body)),
      signal,
      credentials: 'same-origin',
    })
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e
    throw new ApiError(0, 'Không kết nối được máy chủ', 'Không kết nối được máy chủ – kiểm tra mạng rồi thử lại')
  }
}

async function toError(res: Response): Promise<ApiError> {
  if (res.status >= 500) return new ApiError(res.status, 'Lỗi máy chủ', `Máy chủ trả lỗi ${res.status}`)
  const problem = (await res.json().catch(() => null)) as ProblemDetails | null
  if (!problem && res.status === 429) return new ApiError(429, 'Quá nhiều yêu cầu', 'Bạn thao tác quá nhanh – vui lòng thử lại sau ít phút')
  const { title, detail, errors, type: _type, status: _status, instance: _instance, traceId: _traceId, ...extensions } = problem ?? {}
  return new ApiError(res.status, title ?? 'Lỗi', detail, errors, extensions)
}

/** Gửi request; 401 → refresh 1 lần rồi gửi lại; vẫn 401 → ném lỗi (AuthProvider đã về trạng thái khách). */
export async function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  let res = await send(path, options)
  if (res.status === 401 && options.auth !== false) {
    const session = await refreshSession()
    if (session) res = await send(path, options)
  }
  if (!res.ok) throw await toError(res)
  return (res.status === 204 ? undefined : await res.json()) as T
}

/** Ghép query string, bỏ giá trị rỗng. */
export function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  const text = search.toString()
  return text ? `?${text}` : ''
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return 'Đã có lỗi xảy ra – vui lòng thử lại'
}
