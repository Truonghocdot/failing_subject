import { ChevronDown, ChevronLeft, ChevronRight, SearchX, SlidersHorizontal } from 'lucide-react'
import { useId, useState } from 'react'
import { useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { inputClass, SelectField } from '../../components/Field'
import { Alert, EmptyState, LoadingBlock } from '../../components/Feedback'
import { errorMessage } from '../../lib/api'
import { amenityLabels, money, stayRange } from '../../lib/format'
import type { StaySort } from '../../lib/types'
import { filtersQuery, readFilters, useStays, type StayFilters } from './api'
import { SearchForm } from './SearchForm'
import { StayCard } from './StayCard'

const priceSteps = [500_000, 800_000, 1_000_000, 1_500_000, 2_000_000, 3_000_000]

const sortLabels: Record<StaySort, string> = { relevance: 'Phù hợp nhất', price: 'Giá thấp nhất', rating: 'Điểm cao nhất' }

/** /stays – mọi bộ lọc nằm trên URL (F5 / chia sẻ link giữ nguyên kết quả). */
export function StaysPage() {
  const [params, setParams] = useSearchParams()
  const filters = readFilters(params)
  const { data, isPending, isError, error, isPlaceholderData } = useStays(filters)

  const update = (patch: Partial<StayFilters>) => setParams(new URLSearchParams(filtersQuery({ ...filters, page: 1, ...patch })))

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1
  const where = filters.province ?? 'mọi nơi'

  return (
    <div className="flex flex-col gap-6">
      {/* key: URL đổi (Back / Forward) → nạp lại giá trị ô tìm kiếm */}
      <SearchForm key={params.toString()} initial={filters} compact />

      <div className="grid gap-6 lg:grid-cols-[16rem_1fr]">
        <Filters filters={filters} onChange={update} />

        <section aria-labelledby="results-title" aria-busy={isPlaceholderData} className="flex min-w-0 flex-col gap-4">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h1 id="results-title" className="text-xl font-semibold">
                {data ? `${data.total} homestay ở ${where}` : `Homestay ở ${where}`}
              </h1>
              <p className="text-sm text-slate-600 dark:text-slate-300">
                {filters.checkIn && filters.checkOut ? `${stayRange(filters.checkIn, filters.checkOut)} · ${filters.guests} khách` : `${filters.guests} khách · chọn ngày để xem phòng trống và giá chính xác`}
              </p>
            </div>
            <label className="flex items-center gap-2 text-sm">
              <span className="text-slate-600 dark:text-slate-300">Sắp xếp</span>
              <select className={`${inputClass} w-auto`} value={filters.sort} onChange={(e) => update({ sort: e.target.value as StaySort })}>
                {Object.entries(sortLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {isPending ? (
            <LoadingBlock label="Đang tìm homestay…" />
          ) : isError ? (
            <Alert>{errorMessage(error)}</Alert>
          ) : data.items.length === 0 ? (
            <EmptyState icon={SearchX} title="Không có homestay phù hợp">
              Thử đổi ngày, bớt tiện nghi hoặc tăng mức giá.
            </EmptyState>
          ) : (
            <>
              <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {data.items.map((stay) => (
                  <StayCard key={stay.id} stay={stay} trip={filters} />
                ))}
              </ul>
              {totalPages > 1 && (
                <nav aria-label="Phân trang" className="flex items-center justify-center gap-3">
                  <Button variant="secondary" size="sm" disabled={filters.page <= 1} onClick={() => update({ page: filters.page - 1 })}>
                    <ChevronLeft className="size-4" aria-hidden />
                    Trước
                  </Button>
                  <span className="text-sm">
                    Trang {filters.page} / {totalPages}
                  </span>
                  <Button variant="secondary" size="sm" disabled={filters.page >= totalPages} onClick={() => update({ page: filters.page + 1 })}>
                    Sau
                    <ChevronRight className="size-4" aria-hidden />
                  </Button>
                </nav>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  )
}

/** Bộ lọc bên trái (máy tính) / thu gọn được (điện thoại). */
function Filters({ filters, onChange }: { filters: StayFilters; onChange: (patch: Partial<StayFilters>) => void }) {
  const [q, setQ] = useState(filters.q ?? '')
  const panelId = useId()
  const active = filters.amenities.length + (filters.priceMax ? 1 : 0) + (filters.q ? 1 : 0)

  // Điện thoại: mặc định thu gọn (trừ khi đang có bộ lọc) để kết quả hiện ngay; máy tính luôn mở
  const [open, setOpen] = useState(active > 0)

  const toggle = (code: string) =>
    onChange({ amenities: filters.amenities.includes(code) ? filters.amenities.filter((a) => a !== code) : [...filters.amenities, code] })

  return (
    <section aria-label="Bộ lọc" className="self-start rounded-2xl border border-slate-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
      <button
        type="button"
        className="flex w-full items-center gap-2 font-medium lg:pointer-events-none"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((v) => !v)}
      >
        <SlidersHorizontal className="size-4" aria-hidden />
        Bộ lọc{active > 0 && ` (${active})`}
        <ChevronDown className={`ml-auto size-4 transition-transform lg:hidden ${open ? 'rotate-180' : ''}`} aria-hidden />
      </button>
      <div id={panelId} className={`mt-4 flex-col gap-5 lg:flex ${open ? 'flex' : 'hidden'}`}>
        <form
          onSubmit={(e) => {
            e.preventDefault()
            onChange({ q: q.trim() || null })
          }}
          className="flex flex-col gap-1.5"
        >
          <label htmlFor="stay-q" className="text-sm font-medium">
            Tên homestay
          </label>
          <div className="flex gap-2">
            <input id="stay-q" className={inputClass} value={q} maxLength={100} placeholder="VD: Đồi Thông" onChange={(e) => setQ(e.target.value)} />
            <Button type="submit" variant="secondary">
              Lọc
            </Button>
          </div>
        </form>

        <SelectField label="Giá tối đa / đêm" value={filters.priceMax ?? ''} onChange={(e) => onChange({ priceMax: e.target.value ? Number(e.target.value) : null })}>
          <option value="">Không giới hạn</option>
          {priceSteps.map((p) => (
            <option key={p} value={p}>
              {money(p)}
            </option>
          ))}
        </SelectField>

        <fieldset>
          <legend className="mb-2 text-sm font-medium">Tiện nghi</legend>
          <div className="grid grid-cols-2 gap-x-3 gap-y-2 lg:grid-cols-1">
            {Object.entries(amenityLabels).map(([code, label]) => (
              <label key={code} className="flex items-center gap-2 text-sm">
                <input type="checkbox" className="size-4 accent-brand-700" checked={filters.amenities.includes(code)} onChange={() => toggle(code)} />
                {label}
              </label>
            ))}
          </div>
        </fieldset>

        {active > 0 && (
          <Button
            variant="ghost"
            size="sm"
            className="self-start"
            onClick={() => {
              setQ('')
              onChange({ amenities: [], priceMax: null, q: null })
            }}
          >
            Xoá bộ lọc
          </Button>
        )}
      </div>
    </section>
  )
}
