import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { setAccessToken } from '../lib/api'

// jsdom chưa có scrollIntoView
Element.prototype.scrollIntoView ??= () => {}

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  setAccessToken(null) // token nằm ở biến module – không để lọt sang test sau
})
