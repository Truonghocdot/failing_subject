import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="flex min-h-[60dvh] flex-col items-center justify-center gap-3 text-center">
      <p className="text-5xl font-bold text-slate-300 dark:text-slate-700">404</p>
      <p className="text-slate-600 dark:text-slate-300">Không tìm thấy trang bạn cần.</p>
      <Link to="/" className="font-medium text-brand-700 hover:underline dark:text-brand-100">
        Về trang tổng quan
      </Link>
    </div>
  )
}
