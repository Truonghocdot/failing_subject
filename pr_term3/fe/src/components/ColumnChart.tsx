import { useLayoutEffect, useRef, useState } from 'react'

export type ColumnDatum = { key: string; label: string; value: number }

type Props = {
  /** Tên biểu đồ cho trình đọc màn hình (tiêu đề hiển thị nằm ở ngoài). */
  title: string
  data: ColumnDatum[]
  /** Giá trị đầy đủ – tooltip + nhãn cột cao nhất. */
  format: (v: number) => string
  /** Nhãn trục Y gọn (500k, 1,5tr, 50%). */
  axisFormat: (v: number) => string
  /** Trần trục Y cố định (VD công suất = 1); bỏ trống → làm tròn từ giá trị lớn nhất. */
  max?: number
  height?: number
}

const PAD = { top: 20, right: 8, bottom: 28, left: 48 }

/** 4 vạch "đẹp": 0, ¼, ½, ¾, trần làm tròn theo 1 / 2 / 5 × 10ⁿ. */
function niceMax(v: number) {
  if (v <= 0) return 1
  const step = 10 ** Math.floor(Math.log10(v / 4))
  const nice = [1, 2, 2.5, 5, 10].map((m) => m * step * 4).find((c) => c >= v)
  return nice ?? v
}

/**
 * Biểu đồ cột 1 chuỗi (SVG, không thư viện): cột ≤ 24 px, đầu bo 4 px – đáy vuông, lưới mảnh, chỉ ghi số ở cột cao nhất;
 * hover / focus (Tab) từng cột → tooltip. Số liệu đầy đủ nằm ở bảng ngay dưới biểu đồ trên trang.
 */
export function ColumnChart({ title, data, format, axisFormat, max, height = 220 }: Props) {
  const box = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(600)
  const [active, setActive] = useState<number | null>(null)

  useLayoutEffect(() => {
    const el = box.current
    if (!el) return
    const measure = () => el.clientWidth > 0 && setWidth(el.clientWidth)
    measure()
    if (typeof ResizeObserver === 'undefined') return // jsdom
    const ro = new ResizeObserver(measure)
    ro.observe(el)
    return () => ro.disconnect()
  }, [])

  const top = max ?? niceMax(Math.max(0, ...data.map((d) => d.value)))
  const innerW = Math.max(0, width - PAD.left - PAD.right)
  const innerH = height - PAD.top - PAD.bottom
  const band = data.length ? innerW / data.length : innerW
  const barW = Math.max(4, Math.min(24, band * 0.6))
  const y = (v: number) => PAD.top + innerH - (Math.min(v, top) / top) * innerH
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((t) => t * top)
  const peak = data.reduce((best, d, i) => (d.value > (data[best]?.value ?? -1) ? i : best), 0)
  // nhãn trục X thưa dần khi nhiều cột (mỗi nhãn cần ~44 px)
  const every = Math.max(1, Math.ceil(44 / Math.max(band, 1)))

  return (
    // contain: inline-size – bề rộng khung theo bố cục, không theo SVG bên trong (tránh vòng lặp đo → SVG rộng → khung rộng)
    <div ref={box} className="relative w-full min-w-0 [contain:inline-size]">
      <svg width={width} height={height} role="group" aria-label={title} className="block overflow-visible">
        {ticks.map((t) => (
          <g key={t}>
            <line x1={PAD.left} x2={width - PAD.right} y1={y(t)} y2={y(t)} className="stroke-slate-200 dark:stroke-slate-800" strokeWidth={1} />
            <text x={PAD.left - 8} y={y(t)} dy="0.32em" textAnchor="end" className="fill-slate-500 text-[11px] dark:fill-slate-400">
              {axisFormat(t)}
            </text>
          </g>
        ))}
        {data.map((d, i) => {
          const cx = PAD.left + band * i + band / 2
          const h = Math.max(0, PAD.top + innerH - y(d.value))
          const r = Math.min(4, h, barW / 2)
          const x0 = cx - barW / 2
          const base = PAD.top + innerH
          // đầu cột bo 4 px, chân cột vuông trên đường gốc
          const path = h === 0 ? '' : `M${x0},${base} V${base - h + r} Q${x0},${base - h} ${x0 + r},${base - h} H${x0 + barW - r} Q${x0 + barW},${base - h} ${x0 + barW},${base - h + r} V${base} Z`
          return (
            <g key={d.key}>
              <path d={path} className={active === i ? 'fill-brand-700' : 'fill-brand-600'} />
              {i === peak && d.value > 0 && (
                <text x={cx} y={base - h - 6} textAnchor="middle" className="fill-slate-700 text-[11px] font-medium dark:fill-slate-200">
                  {format(d.value)}
                </text>
              )}
              {i % every === 0 && (
                <text x={cx} y={base + 18} textAnchor="middle" className="fill-slate-500 text-[11px] dark:fill-slate-400">
                  {d.label}
                </text>
              )}
              {/* vùng bắt hover / focus = cả cột dọc, lớn hơn thanh */}
              <rect
                x={PAD.left + band * i}
                y={PAD.top}
                width={band}
                height={innerH}
                fill="transparent"
                tabIndex={0}
                role="img"
                aria-label={`${d.label}: ${format(d.value)}`}
                className="cursor-default outline-none focus-visible:stroke-brand-600 focus-visible:stroke-2"
                onPointerEnter={() => setActive(i)}
                onPointerLeave={() => setActive(null)}
                onFocus={() => setActive(i)}
                onBlur={() => setActive(null)}
              />
            </g>
          )
        })}
        <line x1={PAD.left} x2={width - PAD.right} y1={PAD.top + innerH} y2={PAD.top + innerH} className="stroke-slate-300 dark:stroke-slate-700" strokeWidth={1} />
      </svg>
      {active !== null && data[active] && (
        <div
          role="status"
          className="pointer-events-none absolute z-10 -translate-x-1/2 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs whitespace-nowrap shadow-md dark:border-slate-700 dark:bg-slate-900"
          style={{ left: Math.min(Math.max(PAD.left + band * active + band / 2, 60), width - 60), top: Math.max(0, y(data[active].value) - 44) }}
        >
          <span className="block text-sm font-semibold">{format(data[active].value)}</span>
          <span className="text-slate-500 dark:text-slate-400">{data[active].label}</span>
        </div>
      )}
    </div>
  )
}
