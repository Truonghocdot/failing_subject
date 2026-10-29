import { Minus, Plus } from 'lucide-react'
import { useId } from 'react'
import { addDays, today } from '../lib/format'
import { inputClass } from './Field'

type DatesProps = {
  checkIn: string
  checkOut: string
  onChange: (checkIn: string, checkOut: string) => void
  error?: string | null
}

/**
 * Nhận – trả phòng bằng 2 ô date gốc (bàn phím / trình đọc màn hình / điện thoại đều dùng tốt).
 * Chọn ngày nhận ≥ ngày trả → tự dời ngày trả sang hôm sau.
 */
export function DateRangeFields({ checkIn, checkOut, onChange, error }: DatesProps) {
  const id = useId()
  const min = today()
  return (
    <fieldset className="flex min-w-0 flex-col gap-1.5" aria-describedby={error ? `${id}-error` : undefined}>
      <legend className="sr-only">Ngày nhận – trả phòng</legend>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">
          Nhận phòng
          <input
            type="date"
            className={inputClass}
            min={min}
            max={addDays(min, 365)}
            value={checkIn}
            aria-invalid={!!error}
            onChange={(e) => {
              const ci = e.target.value
              onChange(ci, ci && (!checkOut || checkOut <= ci) ? addDays(ci, 1) : checkOut)
            }}
          />
        </label>
        <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">
          Trả phòng
          <input
            type="date"
            className={inputClass}
            min={checkIn ? addDays(checkIn, 1) : addDays(min, 1)}
            value={checkOut}
            aria-invalid={!!error}
            onChange={(e) => onChange(checkIn, e.target.value)}
          />
        </label>
      </div>
      {error && (
        <p id={`${id}-error`} className="text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </fieldset>
  )
}

/** Số khách: nút − / + (vùng bấm 40 px) quanh con số, có nhãn cho trình đọc màn hình. */
export function GuestStepper({ value, onChange, min = 1, max = 20, label = 'Số khách' }: { value: number; onChange: (v: number) => void; min?: number; max?: number; label?: string }) {
  const id = useId()
  const btn =
    'inline-flex size-10 items-center justify-center rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50 disabled:opacity-40 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200'
  return (
    <div className="flex flex-col gap-1.5">
      <span id={id} className="text-sm font-medium text-slate-700 dark:text-slate-200">
        {label}
      </span>
      <div role="group" aria-labelledby={id} className="flex items-center gap-2">
        <button type="button" className={btn} aria-label="Bớt 1 khách" disabled={value <= min} onClick={() => onChange(value - 1)}>
          <Minus className="size-4" aria-hidden />
        </button>
        <output aria-live="polite" className="min-w-16 text-center text-sm font-medium">
          {value} khách
        </output>
        <button type="button" className={btn} aria-label="Thêm 1 khách" disabled={value >= max} onClick={() => onChange(value + 1)}>
          <Plus className="size-4" aria-hidden />
        </button>
      </div>
    </div>
  )
}
