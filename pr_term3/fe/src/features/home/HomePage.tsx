import { MapPin } from 'lucide-react'
import { Link } from 'react-router'
import { Alert, LoadingBlock } from '../../components/Feedback'
import { errorMessage, query } from '../../lib/api'
import { readFilters, useProvinces, useStays } from '../stays/api'
import { SearchForm } from '../stays/SearchForm'
import { StayCard } from '../stays/StayCard'

const featuredFilters = { ...readFilters(new URLSearchParams()), sort: 'rating' as const }

/** Trang chủ khách: ô tìm kiếm lớn, tỉnh phổ biến, homestay điểm cao. */
export function HomePage() {
  const provinces = useProvinces()
  const featured = useStays(featuredFilters)

  return (
    <div className="flex flex-col gap-10">
      <section className="rounded-3xl bg-gradient-to-br from-brand-700 to-brand-800 px-4 py-8 text-white sm:px-6 md:px-10 md:py-14">
        <h1 className="max-w-xl text-3xl font-semibold tracking-tight md:text-4xl">Tìm homestay ưng ý cho chuyến đi tới</h1>
        <p className="mt-3 max-w-xl text-brand-50">Giá rõ từng đêm, giữ chỗ trước – chuyển khoản cọc sau, huỷ sớm được hoàn tiền.</p>
        <div className="mt-6">
          <SearchForm />
        </div>
      </section>

      <section aria-labelledby="provinces-title">
        <h2 id="provinces-title" className="mb-4 text-xl font-semibold">
          Điểm đến phổ biến
        </h2>
        {provinces.isError ? (
          <Alert>{errorMessage(provinces.error)}</Alert>
        ) : (
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
            {(provinces.data ?? []).slice(0, 12).map((p) => (
              <li key={p.province}>
                <Link
                  to={`/stays${query({ province: p.province })}`}
                  className="flex h-full flex-col items-center gap-1 rounded-2xl border border-slate-200 bg-white p-4 text-center hover:border-brand-600 focus-visible:outline-2 focus-visible:outline-brand-600 dark:border-slate-800 dark:bg-slate-900"
                >
                  <MapPin className="size-5 text-brand-700 dark:text-brand-100" aria-hidden />
                  <span className="font-medium">{p.province}</span>
                  <span className="text-xs text-slate-500 dark:text-slate-400">{p.homestays} homestay</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby="featured-title">
        <div className="mb-4 flex items-end justify-between gap-3">
          <h2 id="featured-title" className="text-xl font-semibold">
            Được yêu thích
          </h2>
          <Link to="/stays?sort=rating" className="text-sm font-medium text-brand-700 hover:underline dark:text-brand-100">
            Xem tất cả
          </Link>
        </div>
        {featured.isPending ? (
          <LoadingBlock />
        ) : featured.isError ? (
          <Alert>{errorMessage(featured.error)}</Alert>
        ) : featured.data.items.length === 0 ? (
          <p className="text-sm text-slate-600 dark:text-slate-300">Chưa có homestay nào mở bán.</p>
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {featured.data.items.slice(0, 6).map((s) => (
              <StayCard key={s.id} stay={s} trip={featuredFilters} />
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
