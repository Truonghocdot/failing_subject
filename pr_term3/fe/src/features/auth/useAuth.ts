import { createContext, useContext } from 'react'
import type { AuthResponse, User, UserRole } from '../../lib/types'

export type AuthState = { status: 'loading' } | { status: 'anonymous' } | { status: 'authenticated'; user: User }

export type RegisterInput = { email: string; password: string; fullName: string; phone: string | null }

export type AuthContextValue = AuthState & {
  login: (email: string, password: string) => Promise<User>
  register: (input: RegisterInput) => Promise<User>
  logout: () => Promise<void>
  /** Đổi mật khẩu trả phiên mới (backend thu hồi mọi phiên cũ). */
  applySession: (session: AuthResponse) => void
  updateUser: (user: User) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth phải nằm trong <AuthProvider>')
  return ctx
}

/** User đã đăng nhập – chỉ dùng bên trong route được <RequireAuth> bảo vệ. */
export function useUser(): User {
  const auth = useAuth()
  if (auth.status !== 'authenticated') throw new Error('useUser chỉ dùng khi đã đăng nhập')
  return auth.user
}

export const isStaff = (role: UserRole) => role === 'staff' || role === 'admin'
