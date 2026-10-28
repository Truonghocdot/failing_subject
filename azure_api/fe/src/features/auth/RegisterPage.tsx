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
import { registerSchema, type RegisterValues } from './schemas'

export function RegisterPage() {
  const auth = useAuth()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { displayName: '', email: '', password: '', confirm: '' },
  })

  const onSubmit = handleSubmit(async ({ displayName, email, password }) => {
    setFormError(null)
    try {
      await auth.register({ displayName, email, password })
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['displayName', 'email', 'password']))
    }
  })

  const next = params.get('next')
  const { errors } = formState
  return (
    <AuthLayout
      title="Tạo tài khoản"
      subtitle="Miễn phí – có sẵn ví Tiền mặt và các danh mục thường dùng"
      footer={
        <>
          Đã có tài khoản?{' '}
          <Link to={next ? `/login?next=${encodeURIComponent(next)}` : '/login'} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
            Đăng nhập
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
        {formError && <Alert>{formError}</Alert>}
        <TextField label="Tên hiển thị" autoComplete="name" autoFocus error={errors.displayName?.message} {...register('displayName')} />
        <TextField label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
        <TextField label="Mật khẩu" type="password" autoComplete="new-password" hint="Tối thiểu 8 ký tự" error={errors.password?.message} {...register('password')} />
        <TextField label="Nhập lại mật khẩu" type="password" autoComplete="new-password" error={errors.confirm?.message} {...register('confirm')} />
        <Button type="submit" loading={formState.isSubmitting} className="mt-2 w-full">
          Đăng ký
        </Button>
      </form>
    </AuthLayout>
  )
}
