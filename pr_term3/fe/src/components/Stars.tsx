import { Star } from 'lucide-react'
import { useId } from 'react'

/** Hiển thị số sao (chỉ đọc) – kèm chữ cho trình đọc màn hình. */
export function Stars({ value, className = 'size-4' }: { value: number; className?: string }) {
  return (
    <span className="inline-flex items-center gap-0.5" role="img" aria-label={`${value}/5 sao`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <Star key={i} className={`${className} ${i <= value ? 'fill-amber-400 text-amber-500' : 'text-slate-300 dark:text-slate-600'}`} aria-hidden />
      ))}
    </span>
  )
}

const labels = ['', 'Tệ', 'Chưa tốt', 'Bình thường', 'Tốt', 'Tuyệt vời']

/** Chọn 1–5 sao: nhóm radio thật (phím mũi tên đổi sao, Tab vào / ra 1 lần). */
export function StarInput({ value, onChange, error }: { value: number; onChange: (v: number) => void; error?: string }) {
  const name = useId()
  return (
    <fieldset aria-describedby={error ? `${name}-error` : undefined}>
      <legend className="mb-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">Số sao</legend>
      <div className="flex items-center gap-1">
        {[1, 2, 3, 4, 5].map((i) => (
          <label key={i} className="cursor-pointer rounded-md p-0.5 has-focus-visible:outline-2 has-focus-visible:outline-brand-600">
            <input type="radio" name={name} value={i} checked={value === i} onChange={() => onChange(i)} className="sr-only" aria-label={`${i} sao – ${labels[i]}`} />
            <Star className={`size-8 ${i <= value ? 'fill-amber-400 text-amber-500' : 'text-slate-300 dark:text-slate-600'}`} aria-hidden />
          </label>
        ))}
        <span className="ml-2 text-sm text-slate-600 dark:text-slate-300" aria-hidden>
          {labels[value]}
        </span>
      </div>
      {error && (
        <p id={`${name}-error`} className="mt-1 text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </fieldset>
  )
}
