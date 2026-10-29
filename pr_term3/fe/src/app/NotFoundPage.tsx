import { Link } from 'react-router'

export function NotFoundPage({ homeHref = '/', homeLabel = 'Về trang chủ' }: { homeHref?: string; homeLabel?: string }) {
  return (
    <div className="flex min-h-[50dvh] flex-col items-center justify-center gap-3 text-center">
      <p className="text-5xl font-bold text-slate-300 dark:text-slate-700">404</p>
      <p className="text-slate-600 dark:text-slate-300">Không tìm thấy trang bạn cần.</p>
      <Link to={homeHref} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
        {homeLabel}
      </Link>
    </div>
  )
}
