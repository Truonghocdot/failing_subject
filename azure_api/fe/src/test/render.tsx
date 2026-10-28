import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'
import { routes } from '../app/routes'
import { AuthProvider } from '../features/auth/AuthProvider'
import type { AuthResponse, Category, Wallet } from '../lib/types'

type Reply = [status: number, body?: unknown]
export type ApiRequest = { method: string; path: string; query: URLSearchParams; body: unknown; headers: Headers }
type Handler = Reply | ((req: ApiRequest) => Reply | Promise<Reply>)

/**
 * fetch giả theo bảng "METHOD /path" (không tính query string). Route không khai báo → 404.
 * Trả về mock để kiểm tra request đã gửi (mock.requests).
 */
export function mockApi(routes: Record<string, Handler>) {
  const requests: ApiRequest[] = []
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost')
    const method = init?.method ?? 'GET'
    const req: ApiRequest = {
      method,
      path: url.pathname,
      query: url.searchParams,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
      headers: new Headers(init?.headers),
    }
    requests.push(req)
    const handler = routes[`${method} ${url.pathname}`]
    const [status, body] = handler === undefined ? [404, { title: 'Không tìm thấy' }] : typeof handler === 'function' ? await handler(req) : handler
    return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return Object.assign(fetchMock, { requests })
}

export const testUser = { id: 'u1', email: 'an@vidu.vn', displayName: 'An Nguyễn', currency: 'VND', createdAt: '2026-10-01T00:00:00Z' }

export const session: AuthResponse = { accessToken: 'access-1', expiresIn: 900, user: testUser }

export const wallets: Wallet[] = [
  { id: 1, name: 'Tiền mặt', type: 'cash', openingBalance: 0, balance: 500_000, color: '#16a34a', isArchived: false, sortOrder: 0 },
  { id: 2, name: 'Vietcombank', type: 'bank', openingBalance: 1_000_000, balance: 1_300_000, color: '#2563eb', isArchived: false, sortOrder: 1 },
]

export const categories: Category[] = [
  { id: 10, kind: 'expense', name: 'Ăn uống', icon: 'utensils', color: '#f97316', isArchived: false, sortOrder: 0 },
  { id: 11, kind: 'expense', name: 'Đi lại', icon: 'bus', color: '#0ea5e9', isArchived: false, sortOrder: 1 },
  { id: 20, kind: 'income', name: 'Lương', icon: 'banknote', color: '#16a34a', isArchived: false, sortOrder: 0 },
]

/** API của 1 user đã đăng nhập (cookie refresh còn hạn) – ghi đè / bổ sung route qua `extra`. */
export function signedInApi(extra: Record<string, Handler> = {}) {
  return mockApi({
    'POST /api/auth/refresh': [200, session],
    'GET /health': [200, { status: 'ok', database: 'up' }],
    'GET /api/wallets': [200, { items: wallets, total: 1_800_000 }],
    'GET /api/categories': [200, categories],
    ...extra,
  })
}

/** Render toàn bộ app (AuthProvider + route thật) ở 1 URL, QueryClient riêng cho từng test (không retry). */
export function renderRoute(path = '/') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return {
    router,
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>,
    ),
  }
}
