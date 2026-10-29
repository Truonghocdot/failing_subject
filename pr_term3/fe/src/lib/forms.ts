import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { z } from 'zod'
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

// Khớp RequestValidator / PhoneNumber của backend

const PHONE_RE = /^(0[35789][0-9]{8}|02[0-9]{9})$/

/** "+84 912.345.678" → "0912345678" (như PhoneNumber.TryNormalize). */
export function normalizePhone(input: string): string {
  let digits = input.trim().replace(/[\s.\-()]/g, '')
  if (digits.startsWith('+')) digits = digits.slice(1)
  if (digits.startsWith('84') && (digits.length === 11 || digits.length === 12)) digits = `0${digits.slice(2)}`
  return digits
}

export const isEmailish = (input: string) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(input.trim())

export const isPhone = (input: string) => PHONE_RE.test(normalizePhone(input))

const phoneMessage = 'Số điện thoại không hợp lệ (VD: 0912 345 678)'
export const phoneRequired = z.string().trim().min(1, 'Vui lòng nhập số điện thoại').refine(isPhone, phoneMessage)
export const phoneOptional = z.string().trim().refine((v) => v === '' || isPhone(v), phoneMessage)

export const email = z.string().trim().min(1, 'Vui lòng nhập email').max(254, 'Email không hợp lệ').email('Email không hợp lệ')
export const emailOptional = z.string().trim().max(254, 'Email không hợp lệ').refine((v) => v === '' || z.string().email().safeParse(v).success, 'Email không hợp lệ')
export const newPassword = z.string().min(8, 'Mật khẩu phải từ 8 đến 100 ký tự').max(100, 'Mật khẩu phải từ 8 đến 100 ký tự')
export const fullName = z.string().trim().min(1, 'Vui lòng nhập họ tên').max(100, 'Họ tên tối đa 100 ký tự')
