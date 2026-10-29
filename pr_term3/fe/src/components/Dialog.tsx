import { X } from 'lucide-react'
import { useEffect, useId, useLayoutEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Button } from './Button'

type DialogProps = {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  footer?: ReactNode
  size?: 'sm' | 'md'
}

/** Hộp thoại modal: Esc / bấm nền để đóng, focus vào ô đầu tiên, trả focus khi đóng. Trên điện thoại trượt từ dưới lên. */
export function Dialog({ open, onClose, title, children, footer, size = 'md' }: DialogProps) {
  const titleId = useId()
  const panel = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  useLayoutEffect(() => {
    onCloseRef.current = onClose
  })
  // Nút đã mở hộp thoại – để trả focus khi đóng. Ô autoFocus được React focus ngay lúc mount (trước effect),
  // nên đọc document.activeElement trong effect sẽ ra chính ô đó; lấy từ relatedTarget của lần focus đầu tiên vào hộp thoại.
  const returnFocus = useRef<HTMLElement | null>(null)

  useEffect(() => {
    if (!open) return
    const previous = panel.current?.contains(document.activeElement) ? returnFocus.current : (document.activeElement as HTMLElement | null)
    // Ô có autoFocus (React tự focus khi mount) được giữ nguyên; chỉ khi focus chưa vào hộp thoại mới chọn ô nhập đầu tiên.
    // Lưu ý: querySelector với danh sách selector trả phần tử ĐẦU TIÊN THEO THỨ TỰ DOM, nên tách riêng ô nhập và nút.
    if (!panel.current?.contains(document.activeElement)) {
      const first =
        panel.current?.querySelector<HTMLElement>('input, select, textarea') ??
        panel.current?.querySelector<HTMLElement>('button:not([data-dialog-close])')
      first?.focus()
    }
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onCloseRef.current()
    document.addEventListener('keydown', onKey)
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = overflow
      returnFocus.current = null
      if (previous?.isConnected) previous.focus()
    }
  }, [open])

  if (!open) return null
  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-4">
      <div className="absolute inset-0 bg-slate-950/50" onClick={onClose} aria-hidden />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onFocusCapture={(e) => {
          const from = e.relatedTarget
          if (!returnFocus.current && from instanceof HTMLElement && !panel.current?.contains(from)) returnFocus.current = from
        }}
        className={`relative flex max-h-[92dvh] w-full flex-col rounded-t-2xl bg-white shadow-xl sm:rounded-2xl dark:bg-slate-900 ${
          size === 'sm' ? 'sm:max-w-sm' : 'sm:max-w-lg'
        }`}
      >
        <header className="flex items-center justify-between gap-3 border-b border-slate-200 px-5 py-3 dark:border-slate-800">
          <h2 id={titleId} className="text-lg font-semibold">
            {title}
          </h2>
          <button
            type="button"
            data-dialog-close
            onClick={onClose}
            aria-label="Đóng"
            className="rounded-lg p-1.5 text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
          >
            <X className="size-5" aria-hidden />
          </button>
        </header>
        <div className="overflow-y-auto px-5 py-4">{children}</div>
        {footer && (
          <footer className="flex flex-wrap justify-end gap-2 border-t border-slate-200 px-5 py-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] dark:border-slate-800">
            {footer}
          </footer>
        )}
      </div>
    </div>,
    document.body,
  )
}

type ConfirmProps = {
  open: boolean
  title: string
  message: ReactNode
  confirmLabel: string
  danger?: boolean
  loading?: boolean
  error?: string
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({ open, title, message, confirmLabel, danger, loading, error, onConfirm, onCancel }: ConfirmProps) {
  return (
    <Dialog
      open={open}
      onClose={onCancel}
      title={title}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel}>
            Huỷ
          </Button>
          <Button variant={danger ? 'danger' : 'primary'} loading={loading} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <div className="space-y-3 text-sm text-slate-600 dark:text-slate-300">
        <div>{message}</div>
        {error && <p className="text-red-600 dark:text-red-400" role="alert">{error}</p>}
      </div>
    </Dialog>
  )
}
