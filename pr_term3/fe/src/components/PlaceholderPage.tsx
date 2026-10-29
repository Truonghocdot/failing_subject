import { Construction } from 'lucide-react'

/** Trang tạm của Phase 0 – mỗi trang ghi rõ phase sẽ làm và nội dung dự kiến. */
export function PlaceholderPage({ title, phase, description }: { title: string; phase: number; description: string }) {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
      <div className="flex items-start gap-3 rounded-2xl border border-dashed border-slate-300 bg-white p-6 dark:border-slate-700 dark:bg-slate-900">
        <Construction className="mt-0.5 size-6 shrink-0 text-amber-600" aria-hidden />
        <div>
          <p className="font-medium">Sẽ làm ở Phase {phase}</p>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{description}</p>
        </div>
      </div>
    </section>
  )
}
