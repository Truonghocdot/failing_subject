import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { z } from 'zod'
import { Button } from '../../components/Button'
import { TextField } from '../../components/Field'
import { Alert, Card } from '../../components/Feedback'
import { applyServerErrors, normalizePhone, phoneRequired } from '../../lib/forms'
import type { Booking } from '../../lib/types'
import { useAuth } from '../auth/useAuth'
import { lookupBooking } from './api'
import { BookingView } from './BookingView'

const schema = z.object({
  code: z.string().trim().min(1, 'Vui lòng nhập mã đặt phòng').max(12, 'Mã đặt phòng dạng HS-XXXX-XXXX'),
  phone: phoneRequired,
})
type Values = z.infer<typeof schema>

/** /lookup – không cần đăng nhập: mã + số điện thoại đã dùng khi đặt. Kết quả chỉ để xem (huỷ cần đăng nhập). */
export function LookupPage() {
  const auth = useAuth()
  const [found, setFound] = useState<Booking | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, setError, formState } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { code: '', phone: '' } })

  const onSubmit = handleSubmit(async (v) => {
    setFormError(null)
    setFound(null)
    try {
      setFound(await lookupBooking(v.code.toUpperCase(), normalizePhone(v.phone)))
    } catch (e) {
      setFormError(applyServerErrors(e, setError, ['code', 'phone']))
    }
  })

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <div className="mx-auto flex w-full max-w-md flex-col gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Tra cứu đặt phòng</h1>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">Nhập mã đặt phòng và số điện thoại đã dùng khi đặt.</p>
        </div>
        <Card className="p-5">
          <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
            {formError && <Alert>{formError}</Alert>}
            <TextField label="Mã đặt phòng" placeholder="HS-XXXX-XXXX" autoCapitalize="characters" autoComplete="off" className="font-mono uppercase" error={formState.errors.code?.message} {...register('code')} />
            <TextField label="Số điện thoại" type="tel" inputMode="tel" autoComplete="tel" error={formState.errors.phone?.message} {...register('phone')} />
            <Button type="submit" loading={formState.isSubmitting}>
              Tra cứu
            </Button>
          </form>
        </Card>
      </div>

      {found && (
        <section aria-label="Kết quả tra cứu">
          <BookingView
            booking={found}
            actions={
              (found.status === 'pending' || found.status === 'confirmed') && (
                <p className="text-sm text-slate-600 dark:text-slate-300">
                  {auth.status === 'authenticated' ? (
                    <>
                      Muốn huỷ? Mở{' '}
                      <Link to={`/bookings/${found.code}`} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
                        trang đặt phòng
                      </Link>{' '}
                      (chỉ tài khoản đã đặt mới huỷ được) hoặc gọi chủ nhà.
                    </>
                  ) : (
                    <>
                      Muốn huỷ?{' '}
                      <Link to={`/login?next=${encodeURIComponent(`/bookings/${found.code}`)}`} className="font-medium text-brand-700 hover:underline dark:text-brand-100">
                        Đăng nhập
                      </Link>{' '}
                      bằng tài khoản đã đặt hoặc gọi chủ nhà.
                    </>
                  )}
                </p>
              )
            }
          />
        </section>
      )}
    </div>
  )
}
