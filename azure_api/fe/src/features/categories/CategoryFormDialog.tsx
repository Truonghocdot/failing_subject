import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { CategoryIcon, ColorPicker, IconPicker } from '../../components/CategoryIcon'
import { Dialog } from '../../components/Dialog'
import { TextField } from '../../components/Field'
import { Alert } from '../../components/Feedback'
import { applyServerErrors } from '../../lib/forms'
import type { Category, CategoryKind } from '../../lib/types'
import { categoryKindLabels, useSaveCategory } from './api'

const schema = z.object({
  name: z.string().trim().min(1, 'Vui lòng nhập tên danh mục').max(50, 'Tên danh mục tối đa 50 ký tự'),
  icon: z.string().min(1),
  color: z.string(),
})
type Values = z.infer<typeof schema>

type Props = { open: boolean; kind: CategoryKind; category?: Category; onClose: () => void }

export function CategoryFormDialog({ open, kind, category, onClose }: Props) {
  const save = useSaveCategory()
  const [formError, setFormError] = useState<string | null>(null)
  const defaults = (): Values =>
    category
      ? { name: category.name, icon: category.icon, color: category.color }
      : { name: '', icon: kind === 'expense' ? 'shopping-bag' : 'banknote', color: kind === 'expense' ? '#f97316' : '#16a34a' }
  const { register, control, handleSubmit, setError, reset, watch, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: defaults(),
  })

  useEffect(() => {
    if (open) {
      reset(defaults())
      setFormError(null)
    }
    // chỉ reset khi mở / đổi danh mục (defaults() đọc lại props)
  }, [open, category, kind])

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await save.mutateAsync({ id: category?.id, input: { ...values, kind: category?.kind ?? kind, isArchived: category?.isArchived ?? false } })
      onClose()
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['name', 'icon', 'color']))
    }
  })

  const [icon, color] = watch(['icon', 'color'])
  const label = categoryKindLabels[category?.kind ?? kind].toLowerCase()
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={category ? `Sửa danh mục ${label}` : `Thêm danh mục ${label}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Huỷ
          </Button>
          <Button type="submit" form="category-form" loading={formState.isSubmitting}>
            {category ? 'Lưu' : 'Thêm'}
          </Button>
        </>
      }
    >
      <form id="category-form" onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError && <Alert>{formError}</Alert>}
        <div className="flex items-end gap-3">
          <CategoryIcon icon={icon} color={color} />
          <div className="flex-1">
            <TextField label="Tên danh mục" placeholder={kind === 'expense' ? 'VD: Cà phê' : 'VD: Làm thêm'} autoFocus error={formState.errors.name?.message} {...register('name')} />
          </div>
        </div>
        <Controller control={control} name="icon" render={({ field }) => <IconPicker value={field.value} color={color} onChange={field.onChange} />} />
        <Controller control={control} name="color" render={({ field }) => <ColorPicker value={field.value} onChange={field.onChange} />} />
      </form>
    </Dialog>
  )
}
