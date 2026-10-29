/** Cấu hình build-time từ fe/.env (chỉ biến VITE_*). */
export const env = {
  appName: import.meta.env.VITE_APP_NAME || 'Homestay Booking',
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, ''),
} as const
