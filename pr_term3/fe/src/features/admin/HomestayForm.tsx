import { useState } from 'react'
import { Button } from '../../components/Button'
import { SelectField, TextAreaField, TextField } from '../../components/Field'
import { Alert } from '../../components/Feedback'
import { errorMessage } from '../../lib/api'
import { amenityLabels } from '../../lib/format'
import type { HomestayStatus } from '../../lib/types'
import type { HomestayInput } from './api'
import { serverFieldErrors } from './forms'

type Props = { initial: HomestayInput; submitLabel: string; pending: boolean; error: unknown; onSubmit: (input: HomestayInput) => void }

/** Thông tin + chính sách cọc / huỷ (docs/02-nghiep-vu.md §2) – dùng cho tạo mới (hộp thoại) và trang sửa. */
export function HomestayForm({ initial, submitLabel, pending, error, onSubmit }: Props) {
  const [v, setV] = useState(initial)
  const [local, setLocal] = useState<Record<string, string>>({})
  const errors = { ...serverFieldErrors(error), ...local }
  const text = (k: keyof HomestayInput) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value })
  const num = (k: keyof HomestayInput) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value === '' ? 0 : Number(e.target.value) })
  const hasFieldErrors = Object.keys(serverFieldErrors(error)).length > 0

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(e) => {
        e.preventDefault()
        const next: Record<string, string> = {}
        if (!v.name.trim()) next.name = 'Vui lòng nhập tên homestay'
        if (!v.province.trim()) next.province = 'Vui lòng nhập tỉnh / thành'
        if (!v.address.trim()) next.address = 'Vui lòng nhập địa chỉ'
        if (v.partialRefundDays > v.freeCancelDays) next.partialRefundDays = 'Số ngày hoàn một phần phải ≤ số ngày huỷ miễn phí'
        setLocal(next)
        if (Object.keys(next).length === 0) onSubmit({ ...v, name: v.name.trim(), province: v.province.trim(), address: v.address.trim() })
      }}
    >
      {!!error && !hasFieldErrors && <Alert>{errorMessage(error)}</Alert>}
      <div className="grid gap-4 sm:grid-cols-2">
        <TextField label="Tên homestay" maxLength={120} value={v.name} error={errors.name} onChange={text('name')} />
        <TextField label="Tỉnh / thành" maxLength={60} value={v.province} error={errors.province} onChange={text('province')} placeholder="VD: Lâm Đồng" />
        <div className="sm:col-span-2">
          <TextField label="Địa chỉ" maxLength={255} value={v.address} error={errors.address} onChange={text('address')} />
        </div>
        <div className="sm:col-span-2">
          <TextAreaField label="Mô tả" rows={4} maxLength={5000} value={v.description} error={errors.description} onChange={text('description')} />
        </div>
      </div>

      <fieldset>
        <legend className="mb-2 text-sm font-medium">Tiện nghi</legend>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {Object.entries(amenityLabels).map(([code, label]) => (
            <label key={code} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                className="size-4 accent-brand-700"
                checked={v.amenities.includes(code)}
                onChange={() => setV({ ...v, amenities: v.amenities.includes(code) ? v.amenities.filter((a) => a !== code) : [...v.amenities, code] })}
              />
              {label}
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="grid gap-4 sm:grid-cols-3">
        <legend className="mb-2 text-sm font-medium">Chính sách</legend>
        <TextField label="Giờ nhận phòng" type="time" value={v.checkInTime} error={errors.checkInTime} onChange={text('checkInTime')} />
        <TextField label="Giờ trả phòng" type="time" value={v.checkOutTime} error={errors.checkOutTime} onChange={text('checkOutTime')} />
        <SelectField label="Trạng thái" value={v.status} onChange={(e) => setV({ ...v, status: e.target.value as HomestayStatus })}>
          <option value="active">Đang mở bán</option>
          <option value="hidden">Ẩn khỏi trang khách</option>
        </SelectField>
        <TextField label="Cọc (%)" type="number" min={0} max={100} value={v.depositPercent} error={errors.depositPercent} onChange={num('depositPercent')} />
        <TextField label="Giữ chỗ (giờ)" type="number" min={1} max={72} value={v.holdHours} error={errors.holdHours} onChange={num('holdHours')} />
        <TextField label="Huỷ miễn phí trước (ngày)" type="number" min={0} max={90} value={v.freeCancelDays} error={errors.freeCancelDays} onChange={num('freeCancelDays')} />
        <TextField label="Hoàn một phần trước (ngày)" type="number" min={0} max={90} value={v.partialRefundDays} error={errors.partialRefundDays} onChange={num('partialRefundDays')} />
        <TextField label="Tỉ lệ hoàn một phần (%)" type="number" min={0} max={100} value={v.partialRefundPercent} error={errors.partialRefundPercent} onChange={num('partialRefundPercent')} />
      </fieldset>

      <Button type="submit" loading={pending} className="self-start">
        {submitLabel}
      </Button>
    </form>
  )
}
