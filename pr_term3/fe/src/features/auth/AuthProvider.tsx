import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, onSessionChange, refreshSession, setAccessToken } from '../../lib/api'
import type { AuthResponse, User } from '../../lib/types'
import { AuthContext, type AuthState, type RegisterInput } from './useAuth'

/**
 * Giữ phiên đăng nhập: khởi động thì đổi cookie refresh lấy access token (F5 vẫn đăng nhập);
 * refresh ngầm thất bại ở bất kỳ đâu → về trạng thái khách + xoá cache dữ liệu riêng tư.
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
        queryClient.removeQueries({ queryKey: ['my-bookings'] })
        queryClient.removeQueries({ queryKey: ['booking'] })
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
      const session = await api<AuthResponse>('/auth/login', { method: 'POST', body: { email, password }, auth: false })
      applySession(session)
      return session.user
    },
    [applySession],
  )

  const register = useCallback(
    async (input: RegisterInput) => {
      const session = await api<AuthResponse>('/auth/register', { method: 'POST', body: input, auth: false })
      applySession(session)
      return session.user
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
    queryClient.removeQueries({ queryKey: ['my-bookings'] })
    queryClient.removeQueries({ queryKey: ['booking'] })
  }, [queryClient])

  const updateUser = useCallback((user: User) => setState({ status: 'authenticated', user }), [])

  const value = useMemo(
    () => ({ ...state, login, register, logout, applySession, updateUser }),
    [state, login, register, logout, applySession, updateUser],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
