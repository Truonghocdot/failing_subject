import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { SelectField } from '../../components/Field'
import { Alert } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { formatMonth } from '../../lib/format'
import { applyServerErrors } from '../../lib/forms'
import type { Budget } from '../../lib/types'
import { useCurrency } from '../auth/AuthProvider'
import { useCategories } from '../categories/api'
import { useSaveBudget } from './api'

const schema = z
  .object({
    categoryId: z.number({ error: 'Vui lòng chọn danh mục' }).int().positive('Vui lòng chọn danh mục'),
    amount: z.number().positive('Số tiền ngân sách phải lớn hơn 0').nullable(),
  })
  .refine((v) => v.amount !== null, { path: ['amount'], message: 'Vui lòng nhập số tiền ngân sách' })
type Values = z.infer<typeof schema>

type Props = { open: boolean; month: string; budget?: Budget; budgeted: number[]; onClose: () => void }

/** Thêm (chọn danh mục chi chưa có ngân sách tháng này) hoặc sửa hạn mức. */
export function BudgetFormDialog({ open, month, budget, budgeted, onClose }: Props) {
  const save = useSaveBudget()
  const currency = useCurrency()
  const categories = useCategories()
  const [formError, setFormError] = useState<string | null>(null)
  const choices = (categories.data ?? []).filter((c) => c.kind === 'expense' && !budgeted.includes(c.id))
  const { control, register, handleSubmit, setError, reset, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { categoryId: 0, amount: null },
  })

  useEffect(() => {
    if (open) reset({ categoryId: budget?.category.id ?? 0, amount: budget?.amount ?? null })
  }, [open, budget, reset])

  const close = () => {
    setFormError(null)
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await save.mutateAsync({ month, categoryId: values.categoryId, amount: values.amount! })
      close()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['categoryId', 'amount']))
    }
  })

  return (
    <Dialog
      open={open}
      onClose={close}
      title={budget ? `Sửa ngân sách ${budget.category.name}` : 'Thêm ngân sách'}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={close}>
            Huỷ
          </Button>
          <Button type="submit" form="budget-form" loading={formState.isSubmitting}>
            {budget ? 'Lưu' : 'Thêm'}
          </Button>
        </>
      }
    >
      <form id="budget-form" onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        <p className="text-sm text-slate-500 dark:text-slate-400">{formatMonth(month)}</p>
        {formError && <Alert>{formError}</Alert>}
        {!budget && (
          <SelectField
            label="Danh mục chi"
            error={formState.errors.categoryId?.message}
            hint={choices.length === 0 && categories.data ? 'Mọi danh mục chi đã có ngân sách tháng này' : undefined}
            {...register('categoryId', { valueAsNumber: true })}
          >
            <option value={0}>Chọn danh mục…</option>
            {choices.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </SelectField>
        )}
        <Controller
          control={control}
          name="amount"
          render={({ field, fieldState }) => (
            <MoneyInput
              label="Hạn mức trong tháng"
              currency={currency}
              autoFocus={!!budget}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              error={fieldState.error?.message}
            />
          )}
        />
      </form>
    </Dialog>
  )
}
