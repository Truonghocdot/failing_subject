import { z } from 'zod'

// Khớp quy tắc backend (RequestValidator – docs/02-nghiep-vu.md §1)

const email = z.string().trim().min(1, 'Vui lòng nhập email').max(254, 'Email không hợp lệ').email('Email không hợp lệ')
export const newPassword = z.string().min(8, 'Mật khẩu phải từ 8 đến 100 ký tự').max(100, 'Mật khẩu phải từ 8 đến 100 ký tự')
export const displayName = z.string().trim().min(1, 'Vui lòng nhập tên hiển thị').max(100, 'Tên hiển thị tối đa 100 ký tự')

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Vui lòng nhập mật khẩu'),
})
export type LoginValues = z.infer<typeof loginSchema>

export const registerSchema = z
  .object({ displayName, email, password: newPassword, confirm: z.string() })
  .refine((v) => v.password === v.confirm, { path: ['confirm'], message: 'Mật khẩu nhập lại không khớp' })
export type RegisterValues = z.infer<typeof registerSchema>
