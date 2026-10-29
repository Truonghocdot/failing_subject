import type { ReactNode } from 'react'

/** Bảng rộng cuộn ngang trong khung: focus được bằng Tab để cuộn bằng phím mũi tên (WCAG 2.1.1), có nhãn vùng. */
export function ScrollX({ label, className = '', children, busy }: { label: string; className?: string; children: ReactNode; busy?: boolean }) {
  return (
    <div role="region" aria-label={label} aria-busy={busy} tabIndex={0} className={`overflow-x-auto focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600 ${className}`}>
      {children}
    </div>
  )
}
