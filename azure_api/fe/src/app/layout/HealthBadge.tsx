import { useHealth } from '../../lib/health'

/** Chấm trạng thái FE ↔ backend ↔ MySQL (Phase 0). */
export function HealthBadge() {
  const { data, isPending, isError } = useHealth()

  const [color, text] = isPending
    ? ['bg-slate-400', 'Đang kiểm tra máy chủ…']
    : isError
      ? ['bg-red-500', 'Máy chủ: không kết nối được']
      : data.database === 'up'
        ? ['bg-emerald-500', 'Máy chủ: hoạt động']
        : ['bg-amber-500', 'Máy chủ: mất kết nối database']

  return (
    <p role="status" className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
      <span className={`size-2 rounded-full ${color}`} aria-hidden />
      {text}
    </p>
  )
}
