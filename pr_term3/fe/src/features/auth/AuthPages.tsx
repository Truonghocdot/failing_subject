import { zodResolver } from '@hookform/resolvers/zod'
import { useState, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useSearchParams } from 'react-router'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { TextField } from '../../components/Field'
import { Alert, Card } from '../../components/Feedback'
import { applyServerErrors, email, fullName, newPassword, normalizePhone, phoneOptional } from '../../lib/forms'
import { useAuth } from './useAuth'

const loginSchema = z.object({ email, password: z.string().min(1, 'Vui lòng nhập mật khẩu') })
type LoginValues = z.infer<typeof loginSchema>

const registerSchema = z
  .object({ fullName, email, phone: phoneOptional, password: newPassword, confirm: z.string() })
  .refine((v) => v.password === v.confirm, { path: ['confirm'], message: 'Mật khẩu nhập lại không khớp' })
type RegisterValues = z.infer<typeof registerSchema>

function AuthCard({ title, subtitle, footer, children }: { title: string; subtitle: string; footer: ReactNode; children: ReactNode }) {
  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
        <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{subtitle}</p>
      </div>
      <Card className="p-5">{children}</Card>
      <p className="text-center text-sm text-slate-600 dark:text-slate-300">{footer}</p>
    </div>
  )
}

const linkClass = 'font-medium text-brand-700 hover:underline dark:text-brand-100'

function withNext(path: string, next: string | null) {
  return next ? `${path}?next=${encodeURIComponent(next)}` : path
}

// Đăng nhập / đăng ký xong <GuestOnly> tự chuyển về ?next=

export function LoginPage() {
  const { login } = useAuth()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await login(values.email, values.password)
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['email', 'password']))
    }
  })

  const next = params.get('next')
  return (
    <AuthCard
      title="Đăng nhập"
      subtitle={next?.startsWith('/book/') ? 'Đăng nhập để hoàn tất đặt phòng – ngày và số khách bạn chọn được giữ nguyên.' : 'Xem và quản lý các đặt phòng của bạn.'}
      footer={
        <>
          Chưa có tài khoản?{' '}
          <Link to={withNext('/register', next)} className={linkClass}>
            Đăng ký
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError && <Alert>{formError}</Alert>}
        <TextField label="Email" type="email" autoComplete="email" autoFocus error={formState.errors.email?.message} {...register('email')} />
        <TextField label="Mật khẩu" type="password" autoComplete="current-password" error={formState.errors.password?.message} {...register('password')} />
        <Button type="submit" loading={formState.isSubmitting} className="mt-2 w-full">
          Đăng nhập
        </Button>
      </form>
    </AuthCard>
  )
}

export function RegisterPage() {
  const { register: signUp } = useAuth()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', phone: '', password: '', confirm: '' },
  })

  const onSubmit = handleSubmit(async (v) => {
    setFormError(null)
    try {
      await signUp({ fullName: v.fullName, email: v.email, phone: v.phone ? normalizePhone(v.phone) : null, password: v.password })
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['fullName', 'email', 'phone', 'password']))
    }
  })

  const next = params.get('next')
  return (
    <AuthCard
      title="Tạo tài khoản"
      subtitle="Đặt phòng nhanh hơn – thông tin liên hệ được điền sẵn."
      footer={
        <>
          Đã có tài khoản?{' '}
          <Link to={withNext('/login', next)} className={linkClass}>
            Đăng nhập
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError && <Alert>{formError}</Alert>}
        <TextField label="Họ tên" autoComplete="name" autoFocus error={formState.errors.fullName?.message} {...register('fullName')} />
        <TextField label="Email" type="email" autoComplete="email" error={formState.errors.email?.message} {...register('email')} />
        <TextField
          label="Số điện thoại (không bắt buộc)"
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          error={formState.errors.phone?.message}
          {...register('phone')}
        />
        <TextField label="Mật khẩu" type="password" autoComplete="new-password" hint="Ít nhất 8 ký tự" error={formState.errors.password?.message} {...register('password')} />
        <TextField label="Nhập lại mật khẩu" type="password" autoComplete="new-password" error={formState.errors.confirm?.message} {...register('confirm')} />
        <Button type="submit" loading={formState.isSubmitting} className="mt-2 w-full">
          Đăng ký
        </Button>
      </form>
    </AuthCard>
  )
}
