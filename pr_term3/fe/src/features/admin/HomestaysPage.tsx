import { Hotel, Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { Alert, EmptyState, LoadingBlock } from '../../components/Feedback'
import { errorMessage } from '../../lib/api'
import { Cover } from '../stays/StayCard'
import { catalogApi, useAdminHomestays, useCatalogMutation } from './api'
import { emptyHomestay } from './forms'
import { HomestayForm } from './HomestayForm'
import { PageHeader } from './shared'

/** /admin/homestays – danh sách (cả đang ẩn), thêm mới → sang trang sửa để thêm ảnh, phòng. */
export function HomestaysPage() {
  const homestays = useAdminHomestays()
  const [creating, setCreating] = useState(false)
  const navigate = useNavigate()
  const create = useCatalogMutation(catalogApi.createHomestay)

  return (
    <div>
      <PageHeader
        title="Homestay & phòng"
        description={homestays.data && `${homestays.data.length} homestay`}
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="size-4" aria-hidden />
            Thêm homestay
          </Button>
        }
      />
      {homestays.isPending ? (
        <LoadingBlock />
      ) : homestays.isError ? (
        <Alert>{errorMessage(homestays.error)}</Alert>
      ) : homestays.data.length === 0 ? (
        <EmptyState icon={Hotel} title="Chưa có homestay nào">
          Bấm "Thêm homestay" để bắt đầu.
        </EmptyState>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {homestays.data.map((h) => (
            <li key={h.id}>
              <Link
                to={`/admin/homestays/${h.id}`}
                className="flex h-full flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white hover:shadow-md focus-visible:outline-2 focus-visible:outline-brand-600 dark:border-slate-800 dark:bg-slate-900"
              >
                <Cover url={h.coverUrl} alt="" className="aspect-[16/9] w-full" />
                <div className="flex flex-1 flex-col gap-1 p-4">
                  <div className="flex items-start justify-between gap-2">
                    <span className="font-semibold">{h.name}</span>
                    {h.status === 'hidden' && <span className="rounded-full bg-slate-200 px-2 py-0.5 text-xs font-medium text-slate-800 dark:bg-slate-700 dark:text-slate-100">Đang ẩn</span>}
                  </div>
                  <span className="text-sm text-slate-600 dark:text-slate-300">{h.province}</span>
                  <span className="text-sm text-slate-600 dark:text-slate-300">
                    {h.activeRoomCount}/{h.roomCount} phòng đang mở bán
                  </span>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}

      <Dialog open={creating} onClose={() => setCreating(false)} title="Thêm homestay">
        <HomestayForm
          initial={emptyHomestay}
          submitLabel="Tạo homestay"
          pending={create.isPending}
          error={create.error}
          onSubmit={(input) => create.mutate(input, { onSuccess: (h) => navigate(`/admin/homestays/${h.id}`) })}
        />
      </Dialog>
    </div>
  )
}
