import { lazy, type ComponentType } from 'react'

/**
 * Mỗi trang sau đăng nhập là 1 chunk riêng (Recharts chỉ tải khi mở Tổng quan / Báo cáo) – AppLayout bọc <Suspense>.
 * Đăng nhập / đăng ký nạp sẵn để màn hình đầu tiên hiện ngay.
 */
export function page<K extends string>(load: () => Promise<Record<K, ComponentType>>, name: K) {
  const Page = lazy<ComponentType>(() => load().then((m) => ({ default: m[name] })))
  return <Page />
}
