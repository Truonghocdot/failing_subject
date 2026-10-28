import { zodResolver } from '@hookform/resolvers/zod'
import { LogOut } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { SelectField, TextField } from '../../components/Field'
import { Alert, Card } from '../../components/Feedback'
import { PageHeader } from '../../components/PageHeader'
import { api } from '../../lib/api'
import { formatDate } from '../../lib/format'
import { applyServerErrors } from '../../lib/forms'
import type { AuthResponse, User } from '../../lib/types'
import { useAuth, useUser } from '../auth/AuthProvider'
import { displayName, newPassword } from '../auth/schemas'

const currencies = [
  ['VND', 'Việt Nam Đồng (₫)'],
  ['USD', 'Đô la Mỹ ($)'],
  ['EUR', 'Euro (€)'],
  ['JPY', 'Yên Nhật (¥)'],
  ['KRW', 'Won Hàn Quốc (₩)'],
  ['CNY', 'Nhân dân tệ (¥)'],
  ['THB', 'Baht Thái (฿)'],
  ['SGD', 'Đô la Singapore'],
] as const

const profileSchema = z.object({ displayName, currency: z.string().length(3) })
type ProfileValues = z.infer<typeof profileSchema>

const passwordSchema = z
  .object({ currentPassword: z.string().min(1, 'Vui lòng nhập mật khẩu hiện tại'), newPassword, confirm: z.string() })
  .refine((v) => v.newPassword === v.confirm, { path: ['confirm'], message: 'Mật khẩu nhập lại không khớp' })
type PasswordValues = z.infer<typeof passwordSchema>

export function SettingsPage() {
  const user = useUser()
  const { logout } = useAuth()
  return (
    <>
      <PageHeader title="Cài đặt" description={`${user.email} · tham gia từ ${formatDate(user.createdAt)}`} />
      <div className="grid max-w-3xl gap-6">
        <ProfileForm user={user} />
        <PasswordForm />
        <Card>
          <h2 className="mb-1 font-semibold">Đăng xuất</h2>
          <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">Thoát khỏi tài khoản trên trình duyệt này.</p>
          <Button variant="secondary" onClick={() => void logout()}>
            <LogOut className="size-4" aria-hidden />
            Đăng xuất
          </Button>
        </Card>
      </div>
    </>
  )
}

function ProfileForm({ user }: { user: User }) {
  const { updateUser } = useAuth()
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { displayName: user.displayName, currency: user.currency },
  })

  const onSubmit = handleSubmit(async (values) => {
    setMessage(null)
    try {
      updateUser(await api<User>('/me', { method: 'PUT', body: values }))
      setMessage({ tone: 'info', text: 'Đã lưu thông tin tài khoản' })
    } catch (e) {
      const text = applyServerErrors(e, setError, ['displayName', 'currency'])
      if (text) setMessage({ tone: 'error', text })
    }
  })

  return (
    <Card>
      <h2 className="mb-4 font-semibold">Thông tin tài khoản</h2>
      <form onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
        <TextField label="Tên hiển thị" autoComplete="name" error={formState.errors.displayName?.message} {...register('displayName')} />
        <SelectField label="Tiền tệ hiển thị" hint="Chỉ đổi cách hiển thị, không quy đổi số tiền" error={formState.errors.currency?.message} {...register('currency')}>
          {currencies.map(([code, label]) => (
            <option key={code} value={code}>
              {label}
            </option>
          ))}
          {!currencies.some(([c]) => c === user.currency) && <option value={user.currency}>{user.currency}</option>}
        </SelectField>
        <div className="flex flex-col gap-3 sm:col-span-2">
          {message && <Alert tone={message.tone}>{message.text}</Alert>}
          <div>
            <Button type="submit" loading={formState.isSubmitting}>
              Lưu thay đổi
            </Button>
          </div>
        </div>
      </form>
    </Card>
  )
}

function PasswordForm() {
  const { applySession } = useAuth()
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | null>(null)
  const { register, handleSubmit, setError, reset, formState } = useForm<PasswordValues>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirm: '' },
  })

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    setMessage(null)
    try {
      applySession(await api<AuthResponse>('/me/password', { method: 'PUT', body: { currentPassword, newPassword } }))
      reset()
      setMessage({ tone: 'info', text: 'Đã đổi mật khẩu – các thiết bị khác đã được đăng xuất' })
    } catch (e) {
      // Sai mật khẩu hiện tại: backend trả 400 không kèm errors → gắn vào ô mật khẩu hiện tại
      const text = applyServerErrors(e, setError, ['currentPassword', 'newPassword'])
      if (text === 'Mật khẩu hiện tại không đúng') setError('currentPassword', { type: 'server', message: text })
      else if (text) setMessage({ tone: 'error', text })
    }
  })

  return (
    <Card>
      <h2 className="mb-1 font-semibold">Đổi mật khẩu</h2>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">Sau khi đổi, mọi thiết bị khác sẽ phải đăng nhập lại.</p>
      <form onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-3">
        <TextField label="Mật khẩu hiện tại" type="password" autoComplete="current-password" error={formState.errors.currentPassword?.message} {...register('currentPassword')} />
        <TextField label="Mật khẩu mới" type="password" autoComplete="new-password" error={formState.errors.newPassword?.message} {...register('newPassword')} />
        <TextField label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" error={formState.errors.confirm?.message} {...register('confirm')} />
        <div className="flex flex-col gap-3 sm:col-span-3">
          {message && <Alert tone={message.tone}>{message.text}</Alert>}
          <div>
            <Button type="submit" loading={formState.isSubmitting}>
              Đổi mật khẩu
            </Button>
          </div>
        </div>
      </form>
    </Card>
  )
}
