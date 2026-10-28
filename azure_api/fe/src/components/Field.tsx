import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'

export const inputClass =
  'block h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-900 shadow-xs placeholder:text-slate-400 focus:border-brand-600 focus:outline-2 focus:outline-brand-600/30 disabled:bg-slate-100 aria-invalid:border-red-500 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:disabled:bg-slate-800'

type FieldShellProps = { id: string; label: string; error?: string; hint?: ReactNode; children: ReactNode }

/** Nhãn + ô + lỗi; lỗi được nối vào ô qua aria-describedby để trình đọc màn hình đọc. */
export function FieldShell({ id, label, error, hint, children }: FieldShellProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-slate-700 dark:text-slate-200">
        {label}
      </label>
      {children}
      {error ? (
        <p id={`${id}-error`} className="text-xs text-red-600 dark:text-red-400">
          {error}
        </p>
      ) : (
        hint && <p className="text-xs text-slate-500 dark:text-slate-400">{hint}</p>
      )}
    </div>
  )
}

function useFieldIds(error?: string) {
  const id = useId()
  return { id, describedBy: error ? `${id}-error` : undefined }
}

export function TextField({ label, error, hint, className = '', ...rest }: InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string; hint?: ReactNode }) {
  const { id, describedBy } = useFieldIds(error)
  return (
    <FieldShell id={id} label={label} error={error} hint={hint}>
      <input id={id} aria-invalid={!!error} aria-describedby={describedBy} className={`${inputClass} ${className}`} {...rest} />
    </FieldShell>
  )
}

export function SelectField({ label, error, hint, children, ...rest }: SelectHTMLAttributes<HTMLSelectElement> & { label: string; error?: string; hint?: ReactNode }) {
  const { id, describedBy } = useFieldIds(error)
  return (
    <FieldShell id={id} label={label} error={error} hint={hint}>
      <select id={id} aria-invalid={!!error} aria-describedby={describedBy} className={inputClass} {...rest}>
        {children}
      </select>
    </FieldShell>
  )
}

export function TextAreaField({ label, error, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement> & { label: string; error?: string }) {
  const { id, describedBy } = useFieldIds(error)
  return (
    <FieldShell id={id} label={label} error={error}>
      <textarea id={id} aria-invalid={!!error} aria-describedby={describedBy} className={`${inputClass} h-auto py-2`} rows={2} {...rest} />
    </FieldShell>
  )
}
