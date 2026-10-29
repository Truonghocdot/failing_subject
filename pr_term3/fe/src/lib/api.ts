import { env } from '../app/env'

/** Lỗi từ API (ProblemDetails – RFC 9457). Phase 4 bổ sung Bearer token + tự refresh khi 401. */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly errors: Record<string, string[]>

  constructor(status: number, title: string, detail?: string, errors?: Record<string, string[]>) {
    // 400 không có detail → lỗi đầu tiên trong errors dễ hiểu hơn tiêu đề chung
    super(detail || Object.values(errors ?? {})[0]?.[0] || title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.errors = errors ?? {}
  }
}

type ProblemDetails = { title?: string; detail?: string; errors?: Record<string, string[]> }

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response
  try {
    res = await fetch(`${env.apiBaseUrl}${path}`, { ...init, headers: { Accept: 'application/json', ...init?.headers } })
  } catch {
    throw new ApiError(0, 'Không kết nối được máy chủ', 'Không kết nối được máy chủ – kiểm tra mạng rồi thử lại')
  }
  if (res.ok) return (res.status === 204 ? undefined : await res.json()) as T

  const problem = (await res.json().catch(() => ({}))) as ProblemDetails
  if (res.status >= 500) throw new ApiError(res.status, 'Lỗi máy chủ', `Máy chủ trả lỗi ${res.status}`)
  throw new ApiError(res.status, problem.title ?? 'Lỗi', problem.detail, problem.errors)
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return 'Đã có lỗi xảy ra – vui lòng thử lại'
}
