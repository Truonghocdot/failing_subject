import { env } from '../app/env'
import type { AuthResponse } from './types'

/** Lỗi từ ProblemDetails của backend – `errors` là lỗi từng ô (400 / 409). */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly errors: Record<string, string[]>

  constructor(status: number, title: string, detail?: string, errors?: Record<string, string[]>) {
    // 400 không có detail (VD khoảng ngày sai trên URL) → lỗi đầu tiên dễ hiểu hơn "Dữ liệu không hợp lệ"
    super(detail || Object.values(errors ?? {})[0]?.[0] || title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.errors = errors ?? {}
  }
}

// Access token chỉ nằm trong RAM; refresh token ở cookie HttpOnly mà JS không đọc được
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
 * (hoặc StrictMode gọi 2 lần lúc khởi động) chỉ gửi đúng 1 request refresh.
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
  signal?: AbortSignal
  /** false cho /auth/login, /auth/register: không gắn token, không thử refresh khi 401. */
  auth?: boolean
}

async function send(path: string, { method = 'GET', body, signal, auth = true }: ApiOptions) {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`
  try {
    return await fetch(`${env.apiBaseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
      credentials: 'same-origin',
    })
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e
    throw new ApiError(0, 'Không kết nối được máy chủ', 'Không kết nối được máy chủ – kiểm tra mạng rồi thử lại')
  }
}

async function toError(res: Response): Promise<ApiError> {
  try {
    const p = (await res.json()) as { title?: string; detail?: string; errors?: Record<string, string[]> }
    return new ApiError(res.status, p.title ?? 'Lỗi', p.detail, p.errors)
  } catch {
    if (res.status === 429) return new ApiError(429, 'Quá nhiều yêu cầu', 'Bạn thao tác quá nhanh – vui lòng thử lại sau ít phút')
    return new ApiError(res.status, 'Lỗi máy chủ', `Máy chủ trả lỗi ${res.status}`)
  }
}

/** Gửi request; 401 → refresh 1 lần rồi gửi lại; vẫn 401 → báo hết phiên. */
export async function apiResponse(path: string, options: ApiOptions = {}): Promise<Response> {
  let res = await send(path, options)
  if (res.status === 401 && options.auth !== false) {
    const session = await refreshSession()
    if (session) res = await send(path, options)
  }
  if (!res.ok) throw await toError(res)
  return res
}

export async function api<T>(path: string, options: ApiOptions = {}): Promise<T> {
  const res = await apiResponse(path, options)
  return (res.status === 204 ? undefined : await res.json()) as T
}

/** Thông báo dễ hiểu cho người dùng từ một lỗi bất kỳ. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return 'Đã có lỗi xảy ra – vui lòng thử lại'
}
