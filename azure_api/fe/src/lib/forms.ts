import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { ApiError, errorMessage } from './api'

/**
 * Gắn lỗi từng ô từ ProblemDetails (400 / 409) vào react-hook-form.
 * Trả về thông báo chung cần hiện phía trên form (null nếu mọi lỗi đã gắn được vào ô).
 */
export function applyServerErrors<T extends FieldValues>(error: unknown, setError: UseFormSetError<T>, fields: readonly Path<T>[]): string | null {
  if (!(error instanceof ApiError)) return errorMessage(error)

  let unmatched = false
  for (const [key, messages] of Object.entries(error.errors)) {
    const field = fields.find((f) => f.toLowerCase() === key.toLowerCase())
    if (field) setError(field, { type: 'server', message: messages[0] })
    else unmatched = true
  }
  return Object.keys(error.errors).length > 0 && !unmatched ? null : error.message
}
