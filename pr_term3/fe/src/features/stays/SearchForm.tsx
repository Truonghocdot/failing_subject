import { Search } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { Button } from '../../components/Button'
import { SelectField } from '../../components/Field'
import { DateRangeFields, GuestStepper } from '../../components/TripFields'
import { filtersQuery, MAX_GUESTS, tripError, useProvinces, type StayFilters } from './api'

/**
 * Ô tìm kiếm (trang chủ + đầu trang kết quả): nơi đến, nhận – trả phòng, số khách → /stays?…
 * Giữ nguyên bộ lọc khác (tiện nghi, giá, sắp xếp) đang có trên URL; về trang 1.
 */
export function SearchForm({ initial, compact = false }: { initial?: Partial<StayFilters>; compact?: boolean }) {
  const navigate = useNavigate()
  const provinces = useProvinces()
  const [province, setProvince] = useState(initial?.province ?? '')
  const [checkIn, setCheckIn] = useState(initial?.checkIn ?? '')
  const [checkOut, setCheckOut] = useState(initial?.checkOut ?? '')
  const [guests, setGuests] = useState(initial?.guests ?? 2)
  const [error, setError] = useState<string | null>(null)

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const problem = tripError(checkIn, checkOut)
    setError(problem)
    if (problem) return
    navigate(`/stays${filtersQuery({ ...initial, province: province || null, checkIn: checkIn || null, checkOut: checkOut || null, guests, page: 1 })}`)
  }

  return (
    <form
      role="search"
      aria-label="Tìm homestay"
      onSubmit={submit}
      noValidate
      className={`grid gap-3 rounded-2xl bg-white p-4 text-slate-900 shadow-sm dark:bg-slate-900 dark:text-slate-100 ${
        compact ? 'border border-slate-200 md:grid-cols-[1fr_1.6fr_auto_auto] md:items-start dark:border-slate-800' : 'md:grid-cols-[1fr_1.6fr_auto_auto] md:items-start'
      }`}
    >
      <SelectField label="Nơi đến" value={province} onChange={(e) => setProvince(e.target.value)}>
        <option value="">Mọi nơi</option>
        {provinces.data?.map((p) => (
          <option key={p.province} value={p.province}>
            {p.province} ({p.homestays})
          </option>
        ))}
        {/* tỉnh trên URL chưa có trong danh sách (đang tải / đã ẩn) vẫn hiện để không mất lựa chọn */}
        {province && !provinces.data?.some((p) => p.province === province) && <option value={province}>{province}</option>}
      </SelectField>
      <DateRangeFields
        checkIn={checkIn}
        checkOut={checkOut}
        error={error}
        onChange={(ci, co) => {
          setCheckIn(ci)
          setCheckOut(co)
          setError(null)
        }}
      />
      <GuestStepper value={guests} onChange={setGuests} max={MAX_GUESTS} />
      <Button type="submit" className="h-11 md:mt-6.5">
        <Search className="size-4" aria-hidden />
        Tìm
      </Button>
    </form>
  )
}
