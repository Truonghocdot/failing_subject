import dayjs from 'dayjs'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { Button, IconButton } from '../../components/Button'
import { TextField } from '../../components/Field'
import { Alert, Card, LoadingBlock } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { ScrollX } from '../../components/ScrollX'
import { errorMessage, query } from '../../lib/api'
import { isoDate, money, shortMoney, today } from '../../lib/format'
import type { AdminRoom, PriceSource } from '../../lib/types'
import { catalogApi, useAdminHomestay, useCatalogMutation, useRates } from './api'
import { HomestaySelect, PageHeader } from './shared'

const sourceTones: Record<PriceSource, string> = {
  override: 'bg-amber-100 font-semibold text-amber-950 dark:bg-amber-900/60 dark:text-amber-50',
  weekend: 'bg-sky-50 text-sky-950 dark:bg-sky-950/60 dark:text-sky-100',
  base: '',
}

/** /admin/rates?homestayId&month=YYYY-MM – lịch giá thực tế từng phòng, đặt / bỏ giá riêng cho khoảng ngày (lễ, cao điểm). */
export function RatesPage() {
  const [params, setParams] = useSearchParams()
  const homestayId = Number(params.get('homestayId')) || null
  const monthParam = params.get('month')
  const month = monthParam && /^\d{4}-\d{2}$/.test(monthParam) ? dayjs(`${monthParam}-01`) : dayjs(today()).startOf('month')
  const homestay = useAdminHomestay(homestayId)
  const rooms = homestay.data?.rooms ?? []
  const from = isoDate(month)
  const to = isoDate(month.endOf('month'))
  const rates = useRates(
    rooms.map((r) => r.id),
    from,
    to,
  )
  const setUrl = (patch: { homestayId?: number | null; month?: string }) =>
    setParams(new URLSearchParams(query({ homestayId: patch.homestayId !== undefined ? patch.homestayId : homestayId, month: patch.month ?? month.format('YYYY-MM') })), { replace: true })
  const days = Array.from({ length: month.daysInMonth() }, (_, i) => isoDate(month.add(i, 'day')))

  return (
    <div>
      <PageHeader title="Giá theo ngày" description="Ưu tiên: giá riêng > giá T6, T7 > giá cơ bản. Đặt phòng đã tạo giữ nguyên giá đã chốt." />
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="w-full sm:w-72">
          <HomestaySelect value={homestayId} allLabel={null} onChange={(id) => setUrl({ homestayId: id })} />
        </div>
        <div className="flex items-center gap-1">
          <IconButton label="Tháng trước" onClick={() => setUrl({ month: month.subtract(1, 'month').format('YYYY-MM') })}>
            <ChevronLeft className="size-5" aria-hidden />
          </IconButton>
          <span className="min-w-32 text-center font-medium capitalize">{month.format('MMMM YYYY')}</span>
          <IconButton label="Tháng sau" onClick={() => setUrl({ month: month.add(1, 'month').format('YYYY-MM') })}>
            <ChevronRight className="size-5" aria-hidden />
          </IconButton>
        </div>
      </div>

      {!homestayId ? (
        <Alert tone="info">Chọn homestay để xem và đặt giá.</Alert>
      ) : homestay.isPending || (rooms.length > 0 && rates.isPending) ? (
        <LoadingBlock />
      ) : homestay.isError ? (
        <Alert>{errorMessage(homestay.error)}</Alert>
      ) : rooms.length === 0 ? (
        <Alert tone="info">Homestay chưa có phòng.</Alert>
      ) : (
        <div className="flex flex-col gap-4">
          {rates.isError && <Alert>{errorMessage(rates.error)}</Alert>}
          <ScrollX label={`Bảng giá ${month.format('MM/YYYY')}`} busy={rates.isFetching} className="rounded-2xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
            <table className="min-w-max text-xs">
              <thead>
                <tr>
                  <th className="sticky left-0 z-10 bg-white px-3 py-2 text-left font-medium text-slate-500 dark:text-slate-400 dark:bg-slate-900">Phòng</th>
                  {days.map((d) => (
                    <th key={d} className={`w-14 px-1 py-1.5 text-center font-normal ${[5, 6].includes(dayjs(d).day()) ? 'text-sky-800 dark:text-sky-300' : 'text-slate-500 dark:text-slate-400'}`}>
                      <span className="block">{dayjs(d).format('dd')}</span>
                      {dayjs(d).format('DD')}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {rates.data?.map((r) => (
                  <tr key={r.roomId}>
                    <th scope="row" className="sticky left-0 z-10 bg-white px-3 py-2 text-left text-sm font-medium whitespace-nowrap dark:bg-slate-900">
                      {r.roomName}
                    </th>
                    {r.days.map((d) => (
                      <td key={d.date} className={`px-1 py-2 text-center tabular-nums ${sourceTones[d.source]}`} title={`${money(d.price)}${d.note ? ` – ${d.note}` : ''}`}>
                        {shortMoney(d.price)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </ScrollX>
          <p className="flex flex-wrap gap-4 text-xs">
            <span className="flex items-center gap-1.5">
              <span className={`inline-block size-3 rounded ${sourceTones.override}`} aria-hidden /> Giá riêng (in đậm)
            </span>
            <span className="flex items-center gap-1.5">
              <span className={`inline-block size-3 rounded ${sourceTones.weekend}`} aria-hidden /> Giá T6, T7
            </span>
          </p>
          <RateForm key={homestayId} rooms={rooms} defaultFrom={from < today() ? today() : from} defaultTo={to} />
        </div>
      )}
    </div>
  )
}

function RateForm({ rooms, defaultFrom, defaultTo }: { rooms: AdminRoom[]; defaultFrom: string; defaultTo: string }) {
  const [roomIds, setRoomIds] = useState(rooms.map((r) => r.id))
  const [from, setFrom] = useState(defaultFrom)
  const [to, setTo] = useState(defaultTo)
  const [price, setPrice] = useState<number | null>(null)
  const [note, setNote] = useState('')
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | null>(null)
  const set = useCatalogMutation(catalogApi.setRates)
  const clear = useCatalogMutation(catalogApi.clearRates)

  const check = () => {
    if (roomIds.length === 0) return 'Chọn ít nhất 1 phòng'
    if (!from || !to || to < from) return 'Khoảng ngày không hợp lệ'
    return null
  }
  const done = (verb: string) => (r: { count: number }) => setMessage({ tone: 'info', text: `${verb} ${r.count} đêm-phòng` })
  const fail = (e: unknown) => setMessage({ tone: 'error', text: errorMessage(e) })

  return (
    <Card className="p-5">
      <h2 className="mb-4 font-semibold">Đặt giá riêng cho khoảng ngày</h2>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(e) => {
          e.preventDefault()
          const problem = check() ?? (!price ? 'Vui lòng nhập giá' : null)
          if (problem) return setMessage({ tone: 'error', text: problem })
          set.mutate({ roomIds, from, to, price: price!, note: note.trim() || null }, { onSuccess: done('Đã đặt giá cho'), onError: fail })
        }}
      >
        {message && <Alert tone={message.tone}>{message.text}</Alert>}
        <fieldset>
          <legend className="mb-2 text-sm font-medium">Phòng</legend>
          <div className="flex flex-wrap gap-x-4 gap-y-2">
            {rooms.map((r) => (
              <label key={r.id} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-4 accent-brand-700"
                  checked={roomIds.includes(r.id)}
                  onChange={() => setRoomIds(roomIds.includes(r.id) ? roomIds.filter((x) => x !== r.id) : [...roomIds, r.id])}
                />
                {r.name}
              </label>
            ))}
          </div>
        </fieldset>
        <div className="grid gap-4 sm:grid-cols-4">
          <TextField label="Từ đêm" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          <TextField label="Đến hết đêm" type="date" min={from} value={to} onChange={(e) => setTo(e.target.value)} />
          <MoneyInput label="Giá / đêm" value={price} onChange={setPrice} />
          <TextField label="Ghi chú" maxLength={100} value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: Tết Dương lịch" />
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="submit" loading={set.isPending}>
            Lưu giá riêng
          </Button>
          <Button
            variant="secondary"
            loading={clear.isPending}
            onClick={() => {
              const problem = check()
              if (problem) return setMessage({ tone: 'error', text: problem })
              clear.mutate({ roomIds, from, to }, { onSuccess: done('Đã bỏ giá riêng của'), onError: fail })
            }}
          >
            Bỏ giá riêng trong khoảng này
          </Button>
        </div>
      </form>
    </Card>
  )
}
