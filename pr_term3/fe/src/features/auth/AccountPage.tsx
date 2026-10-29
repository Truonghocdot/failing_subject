import { zodResolver } from '@hookform/resolvers/zod'
import { LogOut } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { TextField } from '../../components/Field'
import { Alert, Card } from '../../components/Feedback'
import { api } from '../../lib/api'
import { applyServerErrors, fullName, newPassword, normalizePhone, phoneOptional } from '../../lib/forms'
import type { AuthResponse, User } from '../../lib/types'
import { useAuth, useUser } from './useAuth'

const profileSchema = z.object({ fullName, phone: phoneOptional })
type ProfileValues = z.infer<typeof profileSchema>

const passwordSchema = z
  .object({ currentPassword: z.string().min(1, 'Vui lòng nhập mật khẩu hiện tại'), newPassword, confirm: z.string() })
  .refine((v) => v.newPassword === v.confirm, { path: ['confirm'], message: 'Mật khẩu nhập lại không khớp' })
type PasswordValues = z.infer<typeof passwordSchema>

const roleLabels = { guest: 'Khách', staff: 'Lễ tân', admin: 'Quản trị' } as const

export function AccountPage() {
  const user = useUser()
  const { logout } = useAuth()
  const navigate = useNavigate()

  return (
    <div className="mx-auto flex w-full max-w-xl flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Tài khoản</h1>
          <p className="mt-1 text-sm break-all text-slate-600 dark:text-slate-300">
            {user.email} · {roleLabels[user.role]}
          </p>
        </div>
        <Button
          variant="secondary"
          onClick={async () => {
            await logout()
            navigate('/')
          }}
        >
          <LogOut className="size-4" aria-hidden />
          Đăng xuất
        </Button>
      </div>
      <ProfileForm user={user} />
      <PasswordForm />
    </div>
  )
}

function ProfileForm({ user }: { user: User }) {
  const { updateUser } = useAuth()
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<ProfileValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: user.fullName, phone: user.phone ?? '' },
  })

  const onSubmit = handleSubmit(async (v) => {
    setMessage(null)
    try {
      updateUser(await api<User>('/me', { method: 'PUT', body: { fullName: v.fullName, phone: v.phone ? normalizePhone(v.phone) : null } }))
      setMessage({ tone: 'info', text: 'Đã lưu thông tin' })
    } catch (e) {
      const text = applyServerErrors(e, setError, ['fullName', 'phone'])
      if (text) setMessage({ tone: 'error', text })
    }
  })

  return (
    <Card className="p-5">
      <h2 className="mb-4 font-semibold">Thông tin liên hệ</h2>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {message && <Alert tone={message.tone}>{message.text}</Alert>}
        <TextField label="Họ tên" autoComplete="name" error={formState.errors.fullName?.message} {...register('fullName')} />
        <TextField
          label="Số điện thoại"
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          hint="Điền sẵn khi đặt phòng, dùng để tra cứu đặt phòng"
          error={formState.errors.phone?.message}
          {...register('phone')}
        />
        <Button type="submit" loading={formState.isSubmitting} className="self-start">
          Lưu thông tin
        </Button>
      </form>
    </Card>
  )
}

function PasswordForm() {
  const { applySession } = useAuth()
  const [message, setMessage] = useState<{ tone: 'error' | 'info'; text: string } | null>(null)
  const { register, handleSubmit, setError, formState, reset } = useForm<PasswordValues>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirm: '' },
  })

  const onSubmit = handleSubmit(async (v) => {
    setMessage(null)
    try {
      applySession(await api<AuthResponse>('/me/password', { method: 'PUT', body: { currentPassword: v.currentPassword, newPassword: v.newPassword } }))
      reset()
      setMessage({ tone: 'info', text: 'Đã đổi mật khẩu – các thiết bị khác đã bị đăng xuất' })
    } catch (e) {
      const text = applyServerErrors(e, setError, ['currentPassword', 'newPassword'])
      if (text) setMessage({ tone: 'error', text })
    }
  })

  return (
    <Card className="p-5">
      <h2 className="mb-4 font-semibold">Đổi mật khẩu</h2>
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {message && <Alert tone={message.tone}>{message.text}</Alert>}
        <TextField label="Mật khẩu hiện tại" type="password" autoComplete="current-password" error={formState.errors.currentPassword?.message} {...register('currentPassword')} />
        <TextField label="Mật khẩu mới" type="password" autoComplete="new-password" hint="Ít nhất 8 ký tự" error={formState.errors.newPassword?.message} {...register('newPassword')} />
        <TextField label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" error={formState.errors.confirm?.message} {...register('confirm')} />
        <Button type="submit" loading={formState.isSubmitting} className="self-start">
          Đổi mật khẩu
        </Button>
      </form>
    </Card>
  )
}
