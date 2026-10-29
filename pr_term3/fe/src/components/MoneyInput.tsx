import { useId } from 'react'
import { FieldShell, inputClass } from './Field'

/** Ô tiền VND: hiện "1.155.000", giá trị là số nguyên (null khi trống). */
export function MoneyInput({
  label,
  value,
  onChange,
  error,
  hint,
}: {
  label: string
  value: number | null
  onChange: (v: number | null) => void
  error?: string
  hint?: string
}) {
  const id = useId()
  return (
    <FieldShell id={id} label={label} error={error} hint={hint}>
      <div className="relative">
        <input
          id={id}
          inputMode="numeric"
          autoComplete="off"
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : undefined}
          className={`${inputClass} pr-8 text-right tabular-nums`}
          value={value === null ? '' : value.toLocaleString('vi-VN')}
          onChange={(e) => {
            const digits = e.target.value.replace(/\D/g, '').slice(0, 13)
            onChange(digits ? Number(digits) : null)
          }}
        />
        <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-slate-500 dark:text-slate-400" aria-hidden>
          ₫
        </span>
      </div>
    </FieldShell>
  )
}
