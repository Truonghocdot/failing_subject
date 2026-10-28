import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

// Trang được tải theo nhu cầu (React.lazy) → lần đầu mỗi file test phải biên dịch module, findBy* cần chờ lâu hơn 1 giây
configure({ asyncUtilTimeout: 5000 })

// jsdom không có ResizeObserver – ResponsiveContainer của Recharts cần (kích thước lấy từ initialDimension)
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})
