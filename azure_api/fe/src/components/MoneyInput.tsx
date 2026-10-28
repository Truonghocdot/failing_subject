import { useId, useState, type Ref } from 'react'
import { formatMoneyInput, parseMoneyInput } from '../lib/format'
import { FieldShell, inputClass } from './Field'

type Props = {
  label: string
  value: number | null
  onChange: (value: number | null) => void
  onBlur?: () => void
  error?: string
  allowNegative?: boolean
  currency?: string
  autoFocus?: boolean
  name?: string
  ref?: Ref<HTMLInputElement>
}

/**
 * Ô nhập tiền kiểu Việt Nam: gõ "45000" hiện "45.000"; "," là phần thập phân.
 * Giữ nguyên chữ người dùng đang gõ, chỉ định dạng lại khi giá trị số đổi từ bên ngoài hoặc khi rời ô.
 */
export function MoneyInput({ label, value, onChange, onBlur, error, allowNegative, currency = 'VND', autoFocus, name, ref }: Props) {
  const id = useId()
  const [text, setText] = useState(() => formatMoneyInput(value))

  // value đổi từ ngoài (VD reset form) → hiện lại theo value; khi đang gõ thì text đã khớp value nên giữ nguyên
  const [syncedValue, setSyncedValue] = useState(value)
  if (value !== syncedValue) {
    setSyncedValue(value)
    if (parseMoneyInput(text) !== value) setText(formatMoneyInput(value))
  }

  return (
    <FieldShell id={id} label={label} error={error}>
      <div className="relative">
        <input
          ref={ref}
          id={id}
          name={name}
          inputMode={allowNegative ? 'text' : 'decimal'}
          autoComplete="off"
          autoFocus={autoFocus}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-error` : undefined}
          className={`${inputClass} pr-14 text-right text-base font-semibold tabular-nums`}
          placeholder="0"
          value={text}
          onChange={(e) => {
            const raw = allowNegative ? e.target.value : e.target.value.replace('-', '')
            const parsed = parseMoneyInput(raw)
            // Định dạng lại ngay phần nguyên để thấy dấu chấm nghìn khi gõ; giữ "," cuối nếu đang gõ phần thập phân
            const typingDecimal = /,\d{0,2}$/.test(raw)
            setText(parsed === null || typingDecimal ? raw : formatMoneyInput(parsed))
            onChange(parsed)
          }}
          onBlur={() => {
            // Định dạng lại từ chữ đang có (không dùng value của lần render trước – có thể đã cũ)
            setText((t) => formatMoneyInput(parseMoneyInput(t)))
            onBlur?.()
          }}
        />
        <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-sm text-slate-500 dark:text-slate-400">{currency}</span>
      </div>
    </FieldShell>
  )
}
