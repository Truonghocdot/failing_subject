import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeftRight, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { CategoryIcon } from '../../components/CategoryIcon'
import { ConfirmDialog, Dialog } from '../../components/Dialog'
import { SelectField, TextField } from '../../components/Field'
import { Alert } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { errorMessage } from '../../lib/api'
import { today } from '../../lib/format'
import { applyServerErrors } from '../../lib/forms'
import type { Category, Transaction, TransactionKind, Wallet } from '../../lib/types'
import { useCurrency } from '../auth/AuthProvider'
import { useCategories } from '../categories/api'
import { useWallets } from '../wallets/api'
import { useDeleteTransaction, useSaveTransaction } from './api'

const MAX_MONEY = 9_999_999_999_999.99
const LAST_WALLET_KEY = 'expense:lastWalletId'

const schema = z
  .object({
    kind: z.enum(['expense', 'income', 'transfer']),
    amount: z.number({ error: 'Vui lòng nhập số tiền' }).positive('Số tiền phải lớn hơn 0').max(MAX_MONEY, 'Số tiền vượt quá giới hạn').nullable(),
    categoryId: z.number().nullable(),
    walletId: z.number().nullable(),
    toWalletId: z.number().nullable(),
    occurredOn: z.string().min(1, 'Vui lòng chọn ngày'),
    note: z.string().max(500, 'Ghi chú tối đa 500 ký tự'),
  })
  .superRefine((v, ctx) => {
    if (v.amount === null) ctx.addIssue({ code: 'custom', path: ['amount'], message: 'Vui lòng nhập số tiền' })
    if (v.walletId === null) ctx.addIssue({ code: 'custom', path: ['walletId'], message: 'Vui lòng chọn ví' })
    if (v.kind === 'transfer') {
      if (v.toWalletId === null) ctx.addIssue({ code: 'custom', path: ['toWalletId'], message: 'Vui lòng chọn ví nhận' })
      else if (v.toWalletId === v.walletId) ctx.addIssue({ code: 'custom', path: ['toWalletId'], message: 'Ví nhận phải khác ví nguồn' })
    } else if (v.categoryId === null) {
      ctx.addIssue({ code: 'custom', path: ['categoryId'], message: 'Vui lòng chọn danh mục' })
    }
  })
type Values = z.infer<typeof schema>

function readLastWallet(): number | null {
  try {
    const v = Number(localStorage.getItem(LAST_WALLET_KEY))
    return Number.isFinite(v) && v > 0 ? v : null
  } catch {
    return null
  }
}

function rememberWallet(id: number) {
  try {
    localStorage.setItem(LAST_WALLET_KEY, String(id))
  } catch {
    // trình duyệt chặn storage: bỏ qua
  }
}

function initialValues(tx: Transaction | undefined, kind: TransactionKind, wallets: Wallet[]): Values {
  if (tx) {
    return {
      kind: tx.kind,
      amount: tx.amount,
      categoryId: tx.category?.id ?? null,
      walletId: tx.wallet.id,
      toWalletId: tx.toWallet?.id ?? null,
      occurredOn: tx.occurredOn,
      note: tx.note ?? '',
    }
  }
  const last = readLastWallet()
  const walletId = wallets.find((w) => w.id === last)?.id ?? wallets[0]?.id ?? null
  return { kind, amount: null, categoryId: null, walletId, toWalletId: null, occurredOn: today(), note: '' }
}

const numberOrNull = (v: unknown) => (v === '' || v == null ? null : Number(v))

type Props = { open: boolean; transaction?: Transaction; defaultKind?: TransactionKind; onClose: () => void }

export function TransactionFormDialog({ open, transaction, defaultKind = 'expense', onClose }: Props) {
  const currency = useCurrency()
  const wallets = useWallets()
  const categories = useCategories()
  const save = useSaveTransaction()
  const remove = useDeleteTransaction()
  const [formError, setFormError] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const activeWallets = useMemo(() => wallets.data?.items ?? [], [wallets.data])
  const { register, control, handleSubmit, setError, reset, watch, setValue, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: initialValues(transaction, defaultKind, activeWallets),
  })

  useEffect(() => {
    if (!open) return
    reset(initialValues(transaction, defaultKind, activeWallets))
    setFormError(null)
    // Ví tải xong sau khi mở → điền ví mặc định khi đó, không reset lại số tiền đã gõ
  }, [open, transaction, defaultKind, reset])

  useEffect(() => {
    if (open && !transaction && watch('walletId') === null && activeWallets.length) {
      setValue('walletId', initialValues(undefined, defaultKind, activeWallets).walletId)
    }
  }, [open, transaction, activeWallets, defaultKind, setValue, watch])

  const kind = watch('kind')
  const categoryId = watch('categoryId')

  // Giao dịch đang sửa có thể trỏ tới ví / danh mục đã lưu trữ → vẫn hiện trong ô chọn
  const walletOptions = useMemo(() => {
    const list: { id: number; name: string }[] = [...activeWallets]
    for (const ref of [transaction?.wallet, transaction?.toWallet]) if (ref && !list.some((w) => w.id === ref.id)) list.push({ ...ref, name: `${ref.name} (đã lưu trữ)` })
    return list
  }, [activeWallets, transaction])

  const categoryOptions = useMemo(() => {
    if (kind === 'transfer') return []
    const list: Pick<Category, 'id' | 'name' | 'icon' | 'color'>[] = (categories.data ?? []).filter((c) => c.kind === kind)
    const current = transaction?.category
    if (current && transaction?.kind === kind && !list.some((c) => c.id === current.id)) list.push(current)
    return list
  }, [categories.data, kind, transaction])

  const onSubmit = handleSubmit(async (v) => {
    setFormError(null)
    try {
      await save.mutateAsync({
        id: transaction?.id,
        input: {
          kind: v.kind,
          amount: v.amount!,
          walletId: v.walletId!,
          toWalletId: v.kind === 'transfer' ? v.toWalletId : null,
          categoryId: v.kind === 'transfer' ? null : v.categoryId,
          occurredOn: v.occurredOn,
          note: v.note.trim() || null,
        },
      })
      rememberWallet(v.walletId!)
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['kind', 'amount', 'categoryId', 'walletId', 'toWalletId', 'occurredOn', 'note']))
    }
  })

  const onDelete = async () => {
    if (!transaction) return
    setDeleteError(null)
    try {
      await remove.mutateAsync(transaction.id)
      setConfirmDelete(false)
      onClose()
    } catch (e) {
      setDeleteError(errorMessage(e))
    }
  }

  const { errors } = formState
  return (
    <>
      <Dialog
        open={open && !confirmDelete}
        onClose={onClose}
        title={transaction ? 'Sửa giao dịch' : 'Thêm giao dịch'}
        footer={
          <>
            {transaction && (
              <Button variant="ghost" className="mr-auto text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40" onClick={() => setConfirmDelete(true)}>
                <Trash2 className="size-4" aria-hidden />
                Xoá
              </Button>
            )}
            <Button variant="secondary" onClick={onClose}>
              Huỷ
            </Button>
            <Button type="submit" form="transaction-form" loading={formState.isSubmitting}>
              {transaction ? 'Lưu' : 'Thêm'}
            </Button>
          </>
        }
      >
        <form id="transaction-form" onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
          {formError && <Alert>{formError}</Alert>}

          <Controller
            control={control}
            name="kind"
            render={({ field }) => (
              <div role="radiogroup" aria-label="Loại giao dịch" className="grid grid-cols-3 rounded-lg bg-slate-100 p-1 dark:bg-slate-800">
                {(
                  [
                    ['expense', 'Khoản chi', 'bg-red-600'],
                    ['income', 'Khoản thu', 'bg-emerald-600'],
                    ['transfer', 'Chuyển tiền', 'bg-sky-600'],
                  ] as const
                ).map(([value, label, activeColor]) => (
                  <button
                    key={value}
                    type="button"
                    role="radio"
                    aria-checked={field.value === value}
                    onClick={() => {
                      field.onChange(value)
                      setValue('categoryId', null)
                    }}
                    className={`rounded-md py-1.5 text-sm font-medium transition-colors ${
                      field.value === value ? `${activeColor} text-white shadow-sm` : 'text-slate-600 hover:text-slate-900 dark:text-slate-300'
                    }`}
                  >
                    {label}
                  </button>
                ))}
              </div>
            )}
          />

          <Controller
            control={control}
            name="amount"
            render={({ field }) => (
              <MoneyInput label="Số tiền" autoFocus currency={currency} value={field.value} onChange={field.onChange} onBlur={field.onBlur} error={errors.amount?.message} />
            )}
          />

          {kind !== 'transfer' && (
            <fieldset>
              <legend className="mb-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">Danh mục</legend>
              <div className="grid grid-cols-3 gap-2 sm:grid-cols-4">
                {categoryOptions.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    aria-pressed={categoryId === c.id}
                    onClick={() => setValue('categoryId', c.id, { shouldValidate: formState.isSubmitted })}
                    className={`flex flex-col items-center gap-1 rounded-xl border px-1 py-2 text-center text-xs transition-colors ${
                      categoryId === c.id
                        ? 'border-transparent font-semibold'
                        : 'border-slate-200 text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800'
                    }`}
                    style={categoryId === c.id ? { backgroundColor: `${c.color}1f`, outline: `2px solid ${c.color}` } : undefined}
                  >
                    <CategoryIcon icon={c.icon} color={c.color} size="sm" />
                    <span className="line-clamp-2 leading-tight">{c.name}</span>
                  </button>
                ))}
              </div>
              {errors.categoryId && <p className="mt-1.5 text-xs text-red-600 dark:text-red-400">{errors.categoryId.message}</p>}
            </fieldset>
          )}

          <div className={`grid gap-4 ${kind === 'transfer' ? 'sm:grid-cols-[1fr_auto_1fr] sm:items-start' : 'sm:grid-cols-2'}`}>
            <SelectField label={kind === 'transfer' ? 'Từ ví' : 'Ví'} error={errors.walletId?.message} {...register('walletId', { setValueAs: numberOrNull })}>
              <option value="">— Chọn ví —</option>
              {walletOptions.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </SelectField>
            {kind === 'transfer' ? (
              <>
                <ArrowLeftRight className="mx-auto hidden size-5 text-slate-400 sm:mt-9 sm:block" aria-hidden />
                <SelectField label="Đến ví" error={errors.toWalletId?.message} {...register('toWalletId', { setValueAs: numberOrNull })}>
                  <option value="">— Chọn ví nhận —</option>
                  {walletOptions.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name}
                    </option>
                  ))}
                </SelectField>
              </>
            ) : (
              <TextField label="Ngày" type="date" error={errors.occurredOn?.message} {...register('occurredOn')} />
            )}
          </div>
          {kind === 'transfer' && <TextField label="Ngày" type="date" error={errors.occurredOn?.message} {...register('occurredOn')} />}

          <TextField label="Ghi chú" placeholder="VD: Phở bò, tiền điện tháng 10…" autoComplete="off" error={errors.note?.message} {...register('note')} />
        </form>
      </Dialog>

      <ConfirmDialog
        open={confirmDelete}
        title="Xoá giao dịch?"
        message="Giao dịch sẽ bị xoá hẳn và số dư ví được tính lại."
        confirmLabel="Xoá"
        danger
        loading={remove.isPending}
        error={deleteError ?? undefined}
        onConfirm={() => void onDelete()}
        onCancel={() => {
          setConfirmDelete(false)
          setDeleteError(null)
        }}
      />
    </>
  )
}
