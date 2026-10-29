import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'
import { routes } from '../app/routes'

type Reply = readonly [status: number, body?: unknown]

export type MockRequest = { url: URL; method: string; body: unknown; headers: Record<string, string> }

/** Trả lời cố định, hoặc hàm (đọc body / query, đếm lượt gọi để trả khác nhau). */
export type MockReply = Reply | ((req: MockRequest) => Reply)

/** fetch giả theo bảng "METHOD /path" (không tính query string). Route không khai báo → 404. */
export function mockApi(table: Record<string, MockReply>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost')
    const method = init?.method ?? 'GET'
    const entry = table[`${method} ${url.pathname}`]
    const req: MockRequest = {
      url,
      method,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
      headers: (init?.headers ?? {}) as Record<string, string>,
    }
    const [status, body] = typeof entry === 'function' ? entry(req) : (entry ?? [404, { title: 'Không tìm thấy' }])
    return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

/** Các request đã gửi tới "METHOD /path". */
export function calls(fetchMock: ReturnType<typeof mockApi>, key: string): MockRequest[] {
  return fetchMock.mock.calls
    .map(([input, init]) => ({
      url: new URL(String(input), 'http://localhost'),
      method: init?.method ?? 'GET',
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
      headers: (init?.headers ?? {}) as Record<string, string>,
    }))
    .filter((r) => `${r.method} ${r.url.pathname}` === key)
}

/** Render toàn bộ app ở 1 URL, QueryClient riêng cho từng test (không retry). */
export function renderRoute(path = '/') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return {
    router,
    ...render(
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    ),
  }
}
