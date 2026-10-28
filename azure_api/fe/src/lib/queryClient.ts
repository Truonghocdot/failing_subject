import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './api'

/** Lỗi 4xx (sai dữ liệu, không có quyền, không tìm thấy) thử lại cũng vô ích. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
  return failureCount < 1
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: shouldRetry,
        refetchOnWindowFocus: false,
      },
    },
  })
}
