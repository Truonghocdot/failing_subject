/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // prefix '' → đọc cả biến không có VITE_ (API_PROXY_TARGET chỉ dùng ở đây, không vào bundle)
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.API_PROXY_TARGET || 'http://localhost:5100'

  return {
    plugins: [react(), tailwindcss()],
    server: {
      port: 5174,
      strictPort: true,
      // Cùng origin với backend → cookie refresh SameSite=Strict hoạt động, không cần CORS
      proxy: {
        '/api': { target, changeOrigin: true },
        '/uploads': { target, changeOrigin: true },
        '/health': { target, changeOrigin: true },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      css: false,
    },
  }
})
