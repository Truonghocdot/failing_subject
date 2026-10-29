import { ArrowLeft, ArrowRight, ExternalLink, GripVertical, ImagePlus, Pencil, Plus, Star, Trash2 } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { NotFoundPage } from '../../app/NotFoundPage'
import { Button, IconButton } from '../../components/Button'
import { ConfirmDialog, Dialog } from '../../components/Dialog'
import { SelectField, TextAreaField, TextField } from '../../components/Field'
import { Alert, Card, LoadingBlock, Spinner } from '../../components/Feedback'
import { MoneyInput } from '../../components/MoneyInput'
import { ApiError, errorMessage } from '../../lib/api'
import { money } from '../../lib/format'
import type { AdminHomestay, AdminRoom, Photo, RoomStatus } from '../../lib/types'
import { catalogApi, useAdminHomestay, useCatalogMutation, type RoomInput } from './api'
import { emptyRoom, serverFieldErrors, toHomestayInput } from './forms'
import { HomestayForm } from './HomestayForm'
import { PageHeader } from './shared'

const MAX_PHOTOS = 20
const MAX_BYTES = 5 * 1024 * 1024

/** /admin/homestays/:id – thông tin & chính sách, ảnh (kéo thả sắp xếp, ảnh đầu = bìa), phòng. */
export function HomestayEditPage() {
  const id = Number(useParams().id)
  const homestay = useAdminHomestay(id)
  if (homestay.isPending) return <LoadingBlock />
  if (homestay.isError)
    return homestay.error instanceof ApiError && homestay.error.status === 404 ? <NotFoundPage homeHref="/admin/homestays" homeLabel="Về danh sách homestay" /> : <Alert>{errorMessage(homestay.error)}</Alert>
  return <Editor h={homestay.data} />
}

function Editor({ h }: { h: AdminHomestay }) {
  const navigate = useNavigate()
  const update = useCatalogMutation(catalogApi.updateHomestay)
  const remove = useCatalogMutation(catalogApi.deleteHomestay)
  const [deleting, setDeleting] = useState(false)
  const [saved, setSaved] = useState(false)

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-4">
      <Link to="/admin/homestays" className="text-sm text-brand-700 hover:underline dark:text-brand-100">
        ← Homestay & phòng
      </Link>
      <PageHeader
        title={h.name}
        description={`/${h.slug} · ${h.status === 'active' ? 'Đang mở bán' : 'Đang ẩn'}`}
        actions={
          <>
            {h.status === 'active' && (
              <a href={`/stays/${h.slug}`} target="_blank" rel="noreferrer" className="inline-flex h-10 items-center gap-2 rounded-lg px-3 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-slate-800">
                <ExternalLink className="size-4" aria-hidden />
                Xem trang khách
              </a>
            )}
            <Button variant="ghost" className="text-red-700 dark:text-red-300" onClick={() => setDeleting(true)}>
              <Trash2 className="size-4" aria-hidden />
              Xoá
            </Button>
          </>
        }
      />

      <Card className="p-5">
        <h2 className="mb-4 font-semibold">Thông tin & chính sách</h2>
        {saved && !update.isError && (
          <div className="mb-4">
            <Alert tone="info">Đã lưu</Alert>
          </div>
        )}
        <HomestayForm
          key={h.updatedAt}
          initial={toHomestayInput(h)}
          submitLabel="Lưu thông tin"
          pending={update.isPending}
          error={update.error}
          onSubmit={(input) => {
            setSaved(false)
            update.mutate({ ...input, id: h.id }, { onSuccess: () => setSaved(true) })
          }}
        />
      </Card>

      <Photos h={h} />
      <Rooms h={h} />

      <ConfirmDialog
        open={deleting}
        title={`Xoá ${h.name}?`}
        message="Chỉ xoá được khi homestay chưa có đặt phòng nào – nếu đã có, hãy chuyển sang Ẩn. Ảnh và phòng bị xoá theo."
        confirmLabel="Xoá homestay"
        danger
        loading={remove.isPending}
        error={remove.isError ? errorMessage(remove.error) : undefined}
        onCancel={() => {
          setDeleting(false)
          remove.reset()
        }}
        onConfirm={() => remove.mutate(h.id, { onSuccess: () => navigate('/admin/homestays', { replace: true }) })}
      />
    </div>
  )
}

/** Ảnh: kéo thả (chuột / cảm ứng qua HTML5 DnD) hoặc nút ← → (bàn phím) để sắp xếp – thứ tự lưu ngay. */
function Photos({ h }: { h: AdminHomestay }) {
  const input = useRef<HTMLInputElement>(null)
  const upload = useCatalogMutation(catalogApi.uploadPhoto)
  const reorder = useCatalogMutation(catalogApi.reorderPhotos)
  const remove = useCatalogMutation(catalogApi.deletePhoto)
  const [dragging, setDragging] = useState<number | null>(null)
  const [order, setOrder] = useState<Photo[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<string | null>(null)
  const photos = order ?? h.photos

  const save = (next: Photo[]) => {
    setOrder(next)
    setError(null)
    reorder.mutate(
      { id: h.id, photoIds: next.map((p) => p.id) },
      { onSuccess: () => setStatus('Đã lưu thứ tự ảnh'), onError: (e) => setError(errorMessage(e)), onSettled: () => setOrder(null) },
    )
  }
  const move = (from: number, to: number) => {
    if (to < 0 || to >= photos.length || from === to) return
    const next = [...photos]
    const [item] = next.splice(from, 1)
    next.splice(to, 0, item)
    save(next)
  }

  const onFiles = async (files: FileList | null) => {
    if (!files?.length) return
    setError(null)
    const list = [...files]
    if (h.photos.length + list.length > MAX_PHOTOS) return setError(`Tối đa ${MAX_PHOTOS} ảnh mỗi homestay`)
    const tooBig = list.find((f) => f.size > MAX_BYTES)
    if (tooBig) return setError(`${tooBig.name}: ảnh tối đa 5 MB`)
    // Tải lần lượt – lỗi 1 ảnh (sai định dạng…) không chặn ảnh khác
    const failed: string[] = []
    for (const [i, file] of list.entries()) {
      setStatus(`Đang tải ảnh ${i + 1}/${list.length}…`)
      try {
        await upload.mutateAsync({ id: h.id, file })
      } catch (e) {
        failed.push(`${file.name}: ${errorMessage(e)}`)
      }
    }
    setStatus(failed.length < list.length ? `Đã tải ${list.length - failed.length} ảnh` : null)
    if (failed.length) setError(failed.join(' · '))
    if (input.current) input.current.value = ''
  }

  return (
    <Card className="p-5">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="font-semibold">
            Ảnh ({h.photos.length}/{MAX_PHOTOS})
          </h2>
          <p className="text-sm text-slate-600 dark:text-slate-300">Ảnh đầu tiên là ảnh bìa. Kéo thả hoặc dùng nút mũi tên để sắp xếp. JPG / PNG / WEBP ≤ 5 MB.</p>
        </div>
        <Button variant="secondary" onClick={() => input.current?.click()} loading={upload.isPending} disabled={h.photos.length >= MAX_PHOTOS}>
          <ImagePlus className="size-4" aria-hidden />
          Tải ảnh lên
        </Button>
        <input ref={input} type="file" accept="image/jpeg,image/png,image/webp" multiple className="sr-only" aria-label="Chọn ảnh tải lên" onChange={(e) => void onFiles(e.target.files)} />
      </div>
      {error && (
        <div className="mb-3">
          <Alert>{error}</Alert>
        </div>
      )}
      <p className="sr-only" role="status">
        {status}
      </p>
      {photos.length === 0 ? (
        <p className="text-sm text-slate-500 dark:text-slate-400">Chưa có ảnh.</p>
      ) : (
        <ol className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4" aria-busy={reorder.isPending}>
          {photos.map((p, i) => (
            <li
              key={p.id}
              draggable
              onDragStart={(e) => {
                setDragging(i)
                e.dataTransfer.effectAllowed = 'move'
              }}
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault()
                if (dragging !== null) move(dragging, i)
                setDragging(null)
              }}
              onDragEnd={() => setDragging(null)}
              className={`group relative overflow-hidden rounded-xl border-2 bg-slate-100 dark:bg-slate-800 ${dragging === i ? 'border-brand-600 opacity-60' : 'border-transparent'}`}
            >
              <img src={p.url} alt={`Ảnh ${i + 1}`} className="aspect-[4/3] w-full object-cover" draggable={false} />
              <span className="absolute top-1.5 left-1.5 flex items-center gap-1 rounded-md bg-slate-950/70 px-1.5 py-0.5 text-xs text-white">
                <GripVertical className="size-3" aria-hidden />
                {i === 0 ? (
                  <>
                    <Star className="size-3 fill-current" aria-hidden />
                    Ảnh bìa
                  </>
                ) : (
                  i + 1
                )}
              </span>
              <div className="flex items-center justify-between gap-1 bg-white px-1 py-1 dark:bg-slate-900">
                <span className="flex">
                  <IconButton label={`Đưa ảnh ${i + 1} lên trước`} disabled={i === 0} onClick={() => move(i, i - 1)} className="disabled:opacity-30">
                    <ArrowLeft className="size-4" aria-hidden />
                  </IconButton>
                  <IconButton label={`Đưa ảnh ${i + 1} ra sau`} disabled={i === photos.length - 1} onClick={() => move(i, i + 1)} className="disabled:opacity-30">
                    <ArrowRight className="size-4" aria-hidden />
                  </IconButton>
                </span>
                {i !== 0 && (
                  <IconButton label={`Đặt ảnh ${i + 1} làm ảnh bìa`} onClick={() => move(i, 0)}>
                    <Star className="size-4" aria-hidden />
                  </IconButton>
                )}
                <IconButton label={`Xoá ảnh ${i + 1}`} onClick={() => remove.mutate({ id: h.id, photoId: p.id }, { onError: (e) => setError(errorMessage(e)) })}>
                  {remove.isPending && remove.variables?.photoId === p.id ? <Spinner className="size-4" /> : <Trash2 className="size-4" aria-hidden />}
                </IconButton>
              </div>
            </li>
          ))}
        </ol>
      )}
    </Card>
  )
}

function Rooms({ h }: { h: AdminHomestay }) {
  const [editing, setEditing] = useState<AdminRoom | 'new' | null>(null)
  const [deleting, setDeleting] = useState<AdminRoom | null>(null)
  const remove = useCatalogMutation(catalogApi.deleteRoom)

  return (
    <Card className="p-5">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-semibold">Phòng ({h.rooms.length})</h2>
        <Button variant="secondary" onClick={() => setEditing('new')}>
          <Plus className="size-4" aria-hidden />
          Thêm phòng
        </Button>
      </div>
      {h.rooms.length === 0 ? (
        <p className="text-sm text-slate-500 dark:text-slate-400">Chưa có phòng – khách chưa đặt được homestay này.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-slate-100 dark:divide-slate-800">
          {h.rooms.map((r) => (
            <li key={r.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
              <div className="min-w-0">
                <p className="font-medium">
                  {r.name}
                  {r.status !== 'active' && <span className="ml-2 rounded-full bg-slate-200 px-2 py-0.5 text-xs text-slate-800 dark:bg-slate-700 dark:text-slate-100">Ngưng</span>}
                </p>
                <p className="text-sm text-slate-600 dark:text-slate-300">
                  {r.baseGuests}–{r.maxGuests} khách · {money(r.basePrice)}
                  {r.weekendPrice !== null && ` · T6, T7 ${money(r.weekendPrice)}`}
                  {r.extraGuestFee > 0 && ` · phụ thu ${money(r.extraGuestFee)}`}
                  {r.cleaningFee > 0 && ` · dọn ${money(r.cleaningFee)}`}
                  {r.minNights > 1 && ` · tối thiểu ${r.minNights} đêm`}
                </p>
              </div>
              <span className="flex">
                <IconButton label={`Sửa ${r.name}`} onClick={() => setEditing(r)}>
                  <Pencil className="size-4" aria-hidden />
                </IconButton>
                <IconButton label={`Xoá ${r.name}`} onClick={() => setDeleting(r)}>
                  <Trash2 className="size-4" aria-hidden />
                </IconButton>
              </span>
            </li>
          ))}
        </ul>
      )}
      {editing && <RoomDialog homestayId={h.id} room={editing === 'new' ? null : editing} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={deleting !== null}
        title={`Xoá ${deleting?.name}?`}
        message="Chỉ xoá được phòng chưa từng có đặt phòng / khoá phòng – nếu đã có, hãy đặt trạng thái Ngưng."
        confirmLabel="Xoá phòng"
        danger
        loading={remove.isPending}
        error={remove.isError ? errorMessage(remove.error) : undefined}
        onCancel={() => {
          setDeleting(null)
          remove.reset()
        }}
        onConfirm={() => deleting && remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })}
      />
    </Card>
  )
}

function RoomDialog({ homestayId, room, onClose }: { homestayId: number; room: AdminRoom | null; onClose: () => void }) {
  const create = useCatalogMutation(catalogApi.createRoom)
  const update = useCatalogMutation(catalogApi.updateRoom)
  const mutation = room ? update : create
  const [v, setV] = useState<RoomInput>(room ? { ...room } : emptyRoom)
  const [local, setLocal] = useState<Record<string, string>>({})
  const errors = { ...serverFieldErrors(mutation.error), ...local }
  const text = (k: keyof RoomInput) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value })
  const int = (k: keyof RoomInput) => (e: { target: { value: string } }) => setV({ ...v, [k]: e.target.value === '' ? 0 : Number(e.target.value) })

  const submit = () => {
    const next: Record<string, string> = {}
    if (!v.name.trim()) next.name = 'Vui lòng nhập tên phòng'
    if (!v.basePrice) next.basePrice = 'Vui lòng nhập giá cơ bản'
    if (v.maxGuests < v.baseGuests) next.maxGuests = 'Số khách tối đa phải ≥ số khách tiêu chuẩn'
    setLocal(next)
    if (Object.keys(next).length > 0) return
    const body = { ...v, name: v.name.trim(), bedInfo: v.bedInfo?.trim() || null, areaM2: v.areaM2 || null }
    if (room) update.mutate({ ...body, id: room.id }, { onSuccess: onClose })
    else create.mutate({ ...body, homestayId }, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={room ? `Sửa ${room.name}` : 'Thêm phòng'}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Quay lại
          </Button>
          <Button loading={mutation.isPending} onClick={submit}>
            {room ? 'Lưu phòng' : 'Thêm phòng'}
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        {!!mutation.error && Object.keys(serverFieldErrors(mutation.error)).length === 0 && (
          <div className="sm:col-span-2">
            <Alert>{errorMessage(mutation.error)}</Alert>
          </div>
        )}
        <TextField label="Tên phòng" maxLength={80} value={v.name} error={errors.name} onChange={text('name')} />
        <TextField label="Giường" maxLength={120} value={v.bedInfo ?? ''} onChange={text('bedInfo')} placeholder="VD: 1 giường đôi 1m8" />
        <TextField label="Khách tiêu chuẩn" type="number" min={1} max={20} value={v.baseGuests} error={errors.baseGuests} onChange={int('baseGuests')} />
        <TextField label="Khách tối đa" type="number" min={1} max={20} value={v.maxGuests} error={errors.maxGuests} onChange={int('maxGuests')} />
        <MoneyInput label="Giá cơ bản / đêm" value={v.basePrice || null} error={errors.basePrice} onChange={(n) => setV({ ...v, basePrice: n ?? 0 })} />
        <MoneyInput label="Giá T6, T7 (bỏ trống = giá cơ bản)" value={v.weekendPrice} error={errors.weekendPrice} onChange={(n) => setV({ ...v, weekendPrice: n })} />
        <MoneyInput label="Phụ thu / khách thêm / đêm" value={v.extraGuestFee} error={errors.extraGuestFee} onChange={(n) => setV({ ...v, extraGuestFee: n ?? 0 })} />
        <MoneyInput label="Phí dọn / lần" value={v.cleaningFee} error={errors.cleaningFee} onChange={(n) => setV({ ...v, cleaningFee: n ?? 0 })} />
        <TextField label="Diện tích (m²)" type="number" min={1} value={v.areaM2 ?? ''} error={errors.areaM2} onChange={(e) => setV({ ...v, areaM2: e.target.value ? Number(e.target.value) : null })} />
        <TextField label="Số đêm tối thiểu" type="number" min={1} max={30} value={v.minNights} error={errors.minNights} onChange={int('minNights')} />
        <SelectField label="Trạng thái" value={v.status} onChange={(e) => setV({ ...v, status: e.target.value as RoomStatus })}>
          <option value="active">Đang mở bán</option>
          <option value="inactive">Ngưng nhận đặt</option>
        </SelectField>
        <TextField label="Thứ tự hiển thị" type="number" min={0} max={1000} value={v.sortOrder} onChange={int('sortOrder')} />
        <div className="sm:col-span-2">
          <TextAreaField label="Mô tả" rows={3} maxLength={5000} value={v.description} onChange={text('description')} />
        </div>
      </div>
    </Dialog>
  )
}
