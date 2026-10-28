import { Archive, ArchiveRestore, Pencil, Plus, Tags, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { Button, IconButton } from '../../components/Button'
import { CategoryIcon } from '../../components/CategoryIcon'
import { ConfirmDialog } from '../../components/Dialog'
import { Alert, Card, EmptyState, LoadingBlock } from '../../components/Feedback'
import { PageHeader } from '../../components/PageHeader'
import { Segmented } from '../../components/Segmented'
import { errorMessage } from '../../lib/api'
import type { Category, CategoryKind } from '../../lib/types'
import { categoryKindOptions, useCategories, useDeleteCategory, useSaveCategory } from './api'
import { CategoryFormDialog } from './CategoryFormDialog'

type Pending = { kind: 'delete' | 'archive' | 'restore'; category: Category }

export function CategoriesPage() {
  const [params, setParams] = useSearchParams()
  const kind: CategoryKind = params.get('kind') === 'income' ? 'income' : 'expense'
  const { data, isPending, error } = useCategories(true)
  const save = useSaveCategory()
  const remove = useDeleteCategory()
  const [editing, setEditing] = useState<{ category?: Category } | null>(null)
  const [pending, setPending] = useState<Pending | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const ofKind = data?.filter((c) => c.kind === kind) ?? []
  const active = ofKind.filter((c) => !c.isArchived)
  const archived = ofKind.filter((c) => c.isArchived)

  const confirm = async () => {
    if (!pending) return
    setActionError(null)
    const { category } = pending
    try {
      if (pending.kind === 'delete') await remove.mutateAsync(category.id)
      else await save.mutateAsync({ id: category.id, input: { ...category, isArchived: pending.kind === 'archive' } })
      setPending(null)
    } catch (e) {
      setActionError(errorMessage(e))
    }
  }

  return (
    <>
      <PageHeader
        title="Danh mục"
        description="Phân loại khoản thu, khoản chi để xem báo cáo"
        actions={
          <Button onClick={() => setEditing({})}>
            <Plus className="size-4" aria-hidden />
            Thêm danh mục
          </Button>
        }
      />

      <div className="mb-4">
        <Segmented
          label="Loại danh mục"
          options={categoryKindOptions}
          value={kind}
          onChange={(k) => setParams(k === 'expense' ? {} : { kind: k }, { replace: true })}
        />
      </div>

      {isPending ? (
        <LoadingBlock />
      ) : error ? (
        <Alert>{errorMessage(error)}</Alert>
      ) : (
        <div className="flex flex-col gap-6">
          {active.length ? (
            <CategoryList categories={active} onEdit={(c) => setEditing({ category: c })} onAction={(k, c) => setPending({ kind: k, category: c })} />
          ) : (
            <EmptyState icon={Tags} title="Chưa có danh mục nào" />
          )}
          {archived.length > 0 && (
            <section>
              <h2 className="mb-2 text-sm font-medium text-slate-500 dark:text-slate-400">Đã lưu trữ ({archived.length})</h2>
              <CategoryList categories={archived} onEdit={(c) => setEditing({ category: c })} onAction={(k, c) => setPending({ kind: k, category: c })} />
            </section>
          )}
        </div>
      )}

      <CategoryFormDialog open={editing !== null} kind={kind} category={editing?.category} onClose={() => setEditing(null)} />
      <ConfirmDialog
        open={pending !== null}
        title={pending?.kind === 'delete' ? 'Xoá danh mục?' : pending?.kind === 'archive' ? 'Lưu trữ danh mục?' : 'Dùng lại danh mục?'}
        message={
          pending?.kind === 'delete'
            ? `Xoá hẳn "${pending.category.name}". Chỉ xoá được danh mục chưa có giao dịch hoặc ngân sách.`
            : pending?.kind === 'archive'
              ? `"${pending.category.name}" sẽ ẩn khỏi ô chọn khi ghi giao dịch, vẫn hiện trong lịch sử và báo cáo.`
              : `"${pending?.category.name}" sẽ hiện lại khi ghi giao dịch.`
        }
        confirmLabel={pending?.kind === 'delete' ? 'Xoá' : pending?.kind === 'archive' ? 'Lưu trữ' : 'Dùng lại'}
        danger={pending?.kind === 'delete'}
        loading={save.isPending || remove.isPending}
        error={actionError ?? undefined}
        onConfirm={() => void confirm()}
        onCancel={() => {
          setPending(null)
          setActionError(null)
        }}
      />
    </>
  )
}

function CategoryList({ categories, onEdit, onAction }: { categories: Category[]; onEdit: (c: Category) => void; onAction: (k: Pending['kind'], c: Category) => void }) {
  return (
    <Card className="p-0">
      <ul className="divide-y divide-slate-100 dark:divide-slate-800">
        {categories.map((c) => (
          <li key={c.id} className={`flex items-center gap-3 px-4 py-2.5 ${c.isArchived ? 'opacity-60' : ''}`}>
            <CategoryIcon icon={c.icon} color={c.color} size="sm" />
            <span className="min-w-0 flex-1 truncate font-medium">{c.name}</span>
            <IconButton label={`Sửa ${c.name}`} onClick={() => onEdit(c)}>
              <Pencil className="size-4" />
            </IconButton>
            {c.isArchived ? (
              <IconButton label={`Dùng lại ${c.name}`} onClick={() => onAction('restore', c)}>
                <ArchiveRestore className="size-4" />
              </IconButton>
            ) : (
              <IconButton label={`Lưu trữ ${c.name}`} onClick={() => onAction('archive', c)}>
                <Archive className="size-4" />
              </IconButton>
            )}
            <IconButton label={`Xoá ${c.name}`} onClick={() => onAction('delete', c)} className="hover:text-red-600">
              <Trash2 className="size-4" />
            </IconButton>
          </li>
        ))}
      </ul>
    </Card>
  )
}
