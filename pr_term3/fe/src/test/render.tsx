import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { vi } from 'vitest'
import { routes } from '../app/routes'

type Reply = readonly [status: number, body?: unknown]

/** fetch giả theo bảng "METHOD /path" (không tính query string). Route không khai báo → 404. */
export function mockApi(table: Record<string, Reply>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost')
    const [status, body] = table[`${init?.method ?? 'GET'} ${url.pathname}`] ?? [404, { title: 'Không tìm thấy' }]
    return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
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
