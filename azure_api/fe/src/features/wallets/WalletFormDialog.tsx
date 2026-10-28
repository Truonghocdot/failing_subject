import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { ColorPicker } from '../../components/CategoryIcon'
import { Dialog } from '../../components/Dialog'
import { SelectField, TextField } from '../../components/Field'
import { Alert } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { applyServerErrors } from '../../lib/forms'
import type { Wallet, WalletType } from '../../lib/types'
import { useCurrency } from '../auth/AuthProvider'
import { useSaveWallet, walletTypeLabels } from './api'

// Khớp DefaultData.WalletColors ở backend
export const walletTypeColors: Record<WalletType, string> = {
  cash: '#16a34a',
  bank: '#2563eb',
  ewallet: '#db2777',
  credit: '#7c3aed',
}

const MAX_MONEY = 9_999_999_999_999.99

const schema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập tên ví').max(50, 'Tên ví tối đa 50 ký tự'),
  type: z.enum(['cash', 'bank', 'ewallet', 'credit']),
  openingBalance: z.number().min(-MAX_MONEY, 'Số dư đầu vượt quá giới hạn').max(MAX_MONEY, 'Số dư đầu vượt quá giới hạn').nullable(),
  color: z.string(),
})
type Values = z.infer<typeof schema>

function toValues(wallet?: Wallet): Values {
  return wallet
    ? { name: wallet.name, type: wallet.type, openingBalance: wallet.openingBalance, color: wallet.color }
    : { name: '', type: 'bank', openingBalance: null, color: walletTypeColors.bank }
}

export function WalletFormDialog({ open, wallet, onClose }: { open: boolean; wallet?: Wallet; onClose: () => void }) {
  const currency = useCurrency()
  const save = useSaveWallet()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, control, handleSubmit, setError, reset, watch, setValue, getValues, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: toValues(wallet),
  })

  useEffect(() => {
    if (open) {
      reset(toValues(wallet))
      setFormError(null)
    }
  }, [open, wallet, reset])

  // Đổi loại ví khi màu vẫn là màu mặc định của loại cũ → đổi theo màu mặc định của loại mới
  const type = watch('type')
  const [prevType, setPrevType] = useState(type)
  if (type !== prevType) {
    setPrevType(type)
    if (getValues('color') === walletTypeColors[prevType]) setValue('color', walletTypeColors[type])
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await save.mutateAsync({
        id: wallet?.id,
        input: { ...values, openingBalance: values.openingBalance ?? 0, isArchived: wallet?.isArchived ?? false },
      })
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['name', 'type', 'openingBalance', 'color']))
    }
  })

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={wallet ? 'Sửa ví' : 'Thêm ví'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Huỷ
          </Button>
          <Button type="submit" form="wallet-form" loading={formState.isSubmitting}>
            {wallet ? 'Lưu' : 'Thêm ví'}
          </Button>
        </>
      }
    >
      <form id="wallet-form" onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError && <Alert>{formError}</Alert>}
        <TextField label="Tên ví" placeholder="VD: Vietcombank, Momo…" autoFocus error={formState.errors.name?.message} {...register('name')} />
        <SelectField label="Loại ví" {...register('type')}>
          {Object.entries(walletTypeLabels).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </SelectField>
        <Controller
          control={control}
          name="openingBalance"
          render={({ field, fieldState }) => (
            <MoneyInput
              label="Số dư ban đầu"
              allowNegative
              currency={currency}
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              error={fieldState.error?.message}
            />
          )}
        />
        <p className="-mt-2 text-xs text-slate-500 dark:text-slate-400">
          Số tiền đang có khi bắt đầu dùng app. Thẻ tín dụng đang nợ thì nhập số âm (VD: -2.500.000).
        </p>
        <Controller control={control} name="color" render={({ field }) => <ColorPicker value={field.value} onChange={field.onChange} />} />
      </form>
    </Dialog>
  )
}
