import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useSearchParams } from 'react-router'
import { Button } from '../../components/Button'
import { TextField } from '../../components/Field'
import { Alert } from '../../components/Feedback'
import { applyServerErrors } from '../../lib/forms'
import { useAuth } from './AuthProvider'
import { AuthLayout } from './AuthLayout'
import { loginSchema, type LoginValues } from './schemas'

export function LoginPage() {
  const { login } = useAuth()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  // Đăng nhập xong <GuestOnly> tự chuyển về ?next=
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
    <AuthLayout
      title="Đăng nhập"
      subtitle="Ghi lại thu chi và theo dõi ngân sách mỗi ngày"
      footer={
        <>
          Chưa có tài khoản?{' '}
          <Link to={next ? `/register?next=${encodeURIComponent(next)}` : '/register'} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
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
    </AuthLayout>
  )
}
