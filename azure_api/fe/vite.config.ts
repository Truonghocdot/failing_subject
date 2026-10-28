/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // prefix '' → đọc cả biến không có VITE_ (API_PROXY_TARGET chỉ dùng ở đây, không vào bundle)
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.API_PROXY_TARGET || 'http://localhost:5090'

  return {
    plugins: [react(), tailwindcss()],
    server: {
      port: 5173,
      strictPort: true,
      // Cùng origin với backend → cookie refresh SameSite=Strict hoạt động, không cần CORS
      proxy: {
        '/api': { target, changeOrigin: true },
        '/health': { target, changeOrigin: true },
      },
    },
    build: {
      rolldownOptions: {
        output: {
          // Thư viện nền ít đổi → chunk riêng, trình duyệt giữ cache qua các lần cập nhật app
          codeSplitting: {
            groups: [
              { name: 'react', test: /node_modules[\\/](react|react-dom|react-router|scheduler)[\\/]/ },
              { name: 'vendor', test: /node_modules[\\/](@tanstack|zod|react-hook-form|@hookform|dayjs)[\\/]/ },
            ],
          },
        },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      css: false,
    },
  }
})
