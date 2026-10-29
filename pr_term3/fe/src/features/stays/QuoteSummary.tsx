import { dayLabel, money } from '../../lib/format'
import type { PriceSource, Quote } from '../../lib/types'

const sourceLabels: Record<PriceSource, string | null> = { base: null, weekend: 'cuối tuần', override: 'giá đặc biệt' }

/** Bảng giá: từng đêm (≤ 7 đêm hiện hết, nhiều hơn thì thu gọn), phụ thu, phí dọn, tổng, tiền cọc. */
export function QuoteSummary({ quote }: { quote: Quote }) {
  const nights = (
    <ul className="flex flex-col gap-1">
      {quote.nightPrices.map((n) => (
        <li key={n.date} className="flex justify-between gap-2">
          <span>
            Đêm {dayLabel(n.date)}
            {sourceLabels[n.source] && <span className="text-amber-700 dark:text-amber-300"> · {sourceLabels[n.source]}</span>}
          </span>
          <span>{money(n.price)}</span>
        </li>
      ))}
    </ul>
  )
  return (
    <div className="flex flex-col gap-2 text-sm">
      {quote.nights <= 7 ? (
        nights
      ) : (
        <details>
          <summary className="cursor-pointer">Giá {quote.nights} đêm</summary>
          <div className="mt-2">{nights}</div>
        </details>
      )}
      <dl className="flex flex-col gap-2">
        <Row label={`Tiền phòng (${quote.nights} đêm)`} value={quote.roomTotal} />
        {quote.extraGuestTotal > 0 && <Row label="Phụ thu khách thêm" value={quote.extraGuestTotal} />}
        {quote.cleaningFee > 0 && <Row label="Phí dọn phòng" value={quote.cleaningFee} />}
        <div className="flex justify-between gap-2 border-t border-slate-200 pt-2 text-base font-semibold dark:border-slate-700">
          <dt>Tổng cộng</dt>
          <dd>{money(quote.total)}</dd>
        </div>
        <div className="flex justify-between gap-2 font-medium text-brand-700 dark:text-brand-100">
          <dt>Cọc trước ({quote.depositPercent}%)</dt>
          <dd>{money(quote.depositAmount)}</dd>
        </div>
      </dl>
    </div>
  )
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex justify-between gap-2 text-slate-600 dark:text-slate-300">
      <dt>{label}</dt>
      <dd>{money(value)}</dd>
    </div>
  )
}
