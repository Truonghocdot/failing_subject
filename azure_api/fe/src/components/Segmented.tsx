/** Nút chuyển kiểu tab (VD Khoản chi / Khoản thu). */
export function Segmented<T extends string>({
  label,
  options,
  value,
  onChange,
}: {
  label: string
  options: readonly { value: T; label: string }[]
  value: T
  onChange: (value: T) => void
}) {
  return (
    <div role="tablist" aria-label={label} className="inline-flex rounded-lg bg-slate-200/70 p-1 dark:bg-slate-800">
      {options.map((o) => (
        <button
          key={o.value}
          role="tab"
          type="button"
          aria-selected={value === o.value}
          onClick={() => onChange(o.value)}
          className={`rounded-md px-4 py-1.5 text-sm font-medium transition-colors ${
            value === o.value ? 'bg-white shadow-sm dark:bg-slate-950' : 'text-slate-600 hover:text-slate-900 dark:text-slate-300'
          }`}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}
