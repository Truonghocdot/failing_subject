import { KeyRound, Pencil, Plus } from 'lucide-react'
import { useState } from 'react'
import { Button, IconButton } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { SelectField, TextField } from '../../components/Field'
import { Alert, LoadingBlock } from '../../components/Feedback'
import { errorMessage } from '../../lib/api'
import { dateTime } from '../../lib/format'
import { isEmailish, isPhone, normalizePhone } from '../../lib/forms'
import type { Staff } from '../../lib/types'
import { useUser } from '../auth/useAuth'
import { staffApi, useStaff, useStaffMutation, type StaffInput } from './api'
import { serverFieldErrors } from './forms'
import { PageHeader } from './shared'

const roleLabels = { staff: 'Lễ tân', admin: 'Quản trị', guest: 'Khách' } as const

/** /admin/users – chỉ admin: tạo lễ tân / quản trị, đổi vai trò, khoá, đặt lại mật khẩu. */
export function UsersPage() {
  const me = useUser()
  const [q, setQ] = useState('')
  const staff = useStaff(q.trim())
  const [editing, setEditing] = useState<Staff | 'new' | null>(null)
  const [resetting, setResetting] = useState<Staff | null>(null)

  return (
    <div>
      <PageHeader
        title="Nhân viên"
        description="Khoá / đổi vai trò / đặt lại mật khẩu sẽ đăng xuất nhân viên đó khỏi mọi thiết bị."
        actions={
          <Button onClick={() => setEditing('new')}>
            <Plus className="size-4" aria-hidden />
            Thêm nhân viên
          </Button>
        }
      />
      <div className="mb-4 max-w-sm">
        <TextField label="Tìm email / tên / SĐT" type="search" value={q} onChange={(e) => setQ(e.target.value)} />
      </div>
      {staff.isPending ? (
        <LoadingBlock />
      ) : staff.isError ? (
        <Alert>{errorMessage(staff.error)}</Alert>
      ) : (
        <ul className="flex flex-col divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
          {staff.data.map((s) => (
            <li key={s.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
              <div className="min-w-0">
                <p className="font-medium">
                  {s.fullName}
                  {s.id === me.id && <span className="text-sm font-normal text-slate-500 dark:text-slate-400"> (bạn)</span>}
                </p>
                <p className="text-sm break-all text-slate-600 dark:text-slate-300">
                  {s.email}
                  {s.phone && ` · ${s.phone}`}
                </p>
                <p className="text-xs text-slate-500 dark:text-slate-400">{s.lastLoginAt ? `Đăng nhập gần nhất ${dateTime(s.lastLoginAt)}` : 'Chưa đăng nhập'}</p>
              </div>
              <div className="flex items-center gap-2">
                <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${s.role === 'admin' ? 'bg-brand-100 text-brand-800 dark:bg-brand-700/30 dark:text-brand-100' : 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-100'}`}>
                  {roleLabels[s.role]}
                </span>
                {!s.isActive && <span className="rounded-full bg-red-50 px-2.5 py-0.5 text-xs font-medium text-red-800 dark:bg-red-950 dark:text-red-200">Đã khoá</span>}
                <IconButton label={`Sửa ${s.fullName}`} onClick={() => setEditing(s)}>
                  <Pencil className="size-4" aria-hidden />
                </IconButton>
                <IconButton label={`Đặt lại mật khẩu ${s.fullName}`} onClick={() => setResetting(s)}>
                  <KeyRound className="size-4" aria-hidden />
                </IconButton>
              </div>
            </li>
          ))}
        </ul>
      )}
      {editing && <StaffDialog staff={editing === 'new' ? null : editing} self={editing !== 'new' && editing.id === me.id} onClose={() => setEditing(null)} />}
      {resetting && <ResetDialog staff={resetting} onClose={() => setResetting(null)} />}
    </div>
  )
}

function StaffDialog({ staff, self, onClose }: { staff: Staff | null; self: boolean; onClose: () => void }) {
  const create = useStaffMutation(staffApi.create)
  const update = useStaffMutation(staffApi.update)
  const mutation = staff ? update : create
  const [v, setV] = useState({
    email: staff?.email ?? '',
    password: '',
    fullName: staff?.fullName ?? '',
    phone: staff?.phone ?? '',
    role: (staff?.role === 'admin' ? 'admin' : 'staff') as StaffInput['role'],
    isActive: staff?.isActive ?? true,
  })
  const [local, setLocal] = useState<Record<string, string>>({})
  const errors = { ...serverFieldErrors(mutation.error), ...local }

  const submit = () => {
    const next: Record<string, string> = {}
    if (!staff && !isEmailish(v.email)) next.email = 'Email không hợp lệ'
    if (!staff && (v.password.length < 8 || v.password.length > 100)) next.password = 'Mật khẩu phải từ 8 đến 100 ký tự'
    if (!v.fullName.trim()) next.fullName = 'Vui lòng nhập họ tên'
    if (v.phone.trim() && !isPhone(v.phone)) next.phone = 'Số điện thoại không hợp lệ (VD: 0912 345 678)'
    setLocal(next)
    if (Object.keys(next).length > 0) return
    const base = { fullName: v.fullName.trim(), phone: v.phone.trim() ? normalizePhone(v.phone) : null, role: v.role }
    if (staff) update.mutate({ ...base, id: staff.id, isActive: v.isActive }, { onSuccess: onClose })
    else create.mutate({ ...base, email: v.email.trim(), password: v.password }, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={staff ? `Sửa ${staff.fullName}` : 'Thêm nhân viên'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Quay lại
          </Button>
          <Button loading={mutation.isPending} onClick={submit}>
            {staff ? 'Lưu' : 'Tạo tài khoản'}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {!!mutation.error && Object.keys(serverFieldErrors(mutation.error)).length === 0 && <Alert>{errorMessage(mutation.error)}</Alert>}
        {!staff && (
          <>
            <TextField label="Email" type="email" autoComplete="off" value={v.email} error={errors.email} onChange={(e) => setV({ ...v, email: e.target.value })} />
            <TextField label="Mật khẩu ban đầu" type="password" autoComplete="new-password" hint="Ít nhất 8 ký tự – gửi riêng cho nhân viên" value={v.password} error={errors.password} onChange={(e) => setV({ ...v, password: e.target.value })} />
          </>
        )}
        <TextField label="Họ tên" value={v.fullName} maxLength={100} error={errors.fullName} onChange={(e) => setV({ ...v, fullName: e.target.value })} />
        <TextField label="Số điện thoại" type="tel" value={v.phone} error={errors.phone} onChange={(e) => setV({ ...v, phone: e.target.value })} />
        <SelectField label="Vai trò" value={v.role} disabled={self} error={errors.role} hint={self ? 'Không tự đổi vai trò của chính mình' : undefined} onChange={(e) => setV({ ...v, role: e.target.value as StaffInput['role'] })}>
          <option value="staff">Lễ tân – vận hành đặt phòng</option>
          <option value="admin">Quản trị – thêm homestay, giá, nhân viên</option>
        </SelectField>
        {staff && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="size-4 accent-brand-700" disabled={self} checked={!v.isActive} onChange={(e) => setV({ ...v, isActive: !e.target.checked })} />
            Khoá tài khoản (đăng xuất khỏi mọi thiết bị)
          </label>
        )}
      </div>
    </Dialog>
  )
}

function ResetDialog({ staff, onClose }: { staff: Staff; onClose: () => void }) {
  const reset = useStaffMutation(staffApi.resetPassword)
  const [password, setPassword] = useState('')
  const [local, setLocal] = useState<string | null>(null)
  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      title={`Đặt lại mật khẩu – ${staff.fullName}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Quay lại
          </Button>
          <Button
            loading={reset.isPending}
            onClick={() => {
              if (password.length < 8 || password.length > 100) return setLocal('Mật khẩu phải từ 8 đến 100 ký tự')
              reset.mutate({ id: staff.id, password }, { onSuccess: onClose })
            }}
          >
            Đặt mật khẩu
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-3">
        <TextField label="Mật khẩu mới" type="password" autoComplete="new-password" value={password} error={local ?? undefined} onChange={(e) => setPassword(e.target.value)} />
        {reset.isError && <Alert>{errorMessage(reset.error)}</Alert>}
      </div>
    </Dialog>
  )
}
