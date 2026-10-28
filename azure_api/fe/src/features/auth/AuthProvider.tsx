import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, onSessionChange, refreshSession, setAccessToken } from '../../lib/api'
import type { AuthResponse, User } from '../../lib/types'

type AuthState = { status: 'loading' } | { status: 'anonymous' } | { status: 'authenticated'; user: User }

type AuthContextValue = AuthState & {
  login: (email: string, password: string) => Promise<void>
  register: (input: { email: string; password: string; displayName: string }) => Promise<void>
  logout: () => Promise<void>
  /** Đổi mật khẩu trả phiên mới (backend thu hồi mọi phiên cũ). */
  applySession: (session: AuthResponse) => void
  updateUser: (user: User) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * Giữ phiên đăng nhập: khởi động thì đổi cookie refresh lấy access token (F5 vẫn đăng nhập);
 * refresh ngầm thất bại ở bất kỳ đâu → về trạng thái khách + xoá cache dữ liệu.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [state, setState] = useState<AuthState>({ status: 'loading' })

  useEffect(() => {
    let active = true
    const unsubscribe = onSessionChange((session) => {
      if (session) {
        setState({ status: 'authenticated', user: session.user })
      } else {
        setState({ status: 'anonymous' })
        queryClient.clear()
      }
    })
    void refreshSession().then((session) => {
      if (active && !session) setState((s) => (s.status === 'loading' ? { status: 'anonymous' } : s))
    })
    return () => {
      active = false
      unsubscribe()
    }
  }, [queryClient])

  const applySession = useCallback((session: AuthResponse) => {
    setAccessToken(session.accessToken)
    setState({ status: 'authenticated', user: session.user })
  }, [])

  const login = useCallback(
    async (email: string, password: string) => {
      applySession(await api<AuthResponse>('/auth/login', { method: 'POST', body: { email, password }, auth: false }))
    },
    [applySession],
  )

  const register = useCallback(
    async (input: { email: string; password: string; displayName: string }) => {
      applySession(await api<AuthResponse>('/auth/register', { method: 'POST', body: input, auth: false }))
    },
    [applySession],
  )

  const logout = useCallback(async () => {
    try {
      await api('/auth/logout', { method: 'POST', auth: false })
    } catch {
      // mất mạng vẫn đăng xuất phía trình duyệt
    }
    setAccessToken(null)
    setState({ status: 'anonymous' })
    queryClient.clear()
  }, [queryClient])

  const updateUser = useCallback((user: User) => setState({ status: 'authenticated', user }), [])

  const value = useMemo(
    () => ({ ...state, login, register, logout, applySession, updateUser }),
    [state, login, register, logout, applySession, updateUser],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

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

/** Tiền tệ hiển thị của user (mặc định VND khi chưa đăng nhập / ngoài provider). */
export function useCurrency(): string {
  const ctx = useContext(AuthContext)
  return ctx?.status === 'authenticated' ? ctx.user.currency : 'VND'
}
