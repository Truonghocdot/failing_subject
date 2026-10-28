import { Archive, ArchiveRestore, Banknote, CreditCard, Landmark, Pencil, Plus, Smartphone, Trash2, Wallet as WalletIcon, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Button, IconButton } from '../../components/Button'
import { ConfirmDialog } from '../../components/Dialog'
import { Alert, Card, EmptyState, LoadingBlock } from '../../components/Feedback'
import { Money } from '../../components/Money'
import { PageHeader } from '../../components/PageHeader'
import { errorMessage } from '../../lib/api'
import type { Wallet, WalletType } from '../../lib/types'
import { useDeleteWallet, useSaveWallet, useWallets, walletTypeLabels } from './api'
import { WalletFormDialog } from './WalletFormDialog'

export const walletTypeIcons: Record<WalletType, LucideIcon> = {
  cash: Banknote,
  bank: Landmark,
  ewallet: Smartphone,
  credit: CreditCard,
}

type Pending = { kind: 'delete' | 'archive' | 'restore'; wallet: Wallet }

export function WalletsPage() {
  const [showArchived, setShowArchived] = useState(false)
  const { data, isPending, error } = useWallets(showArchived)
  const save = useSaveWallet()
  const remove = useDeleteWallet()
  const [editing, setEditing] = useState<{ wallet?: Wallet } | null>(null)
  const [pending, setPending] = useState<Pending | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const confirm = async () => {
    if (!pending) return
    setActionError(null)
    const { wallet } = pending
    try {
      if (pending.kind === 'delete') await remove.mutateAsync(wallet.id)
      else await save.mutateAsync({ id: wallet.id, input: { ...wallet, isArchived: pending.kind === 'archive' } })
      setPending(null)
    } catch (e) {
      setActionError(errorMessage(e))
    }
  }

  const active = data?.items.filter((w) => !w.isArchived) ?? []
  const archived = data?.items.filter((w) => w.isArchived) ?? []

  return (
    <>
      <PageHeader
        title="Ví"
        description="Tiền mặt, tài khoản ngân hàng, ví điện tử và thẻ"
        actions={
          <Button onClick={() => setEditing({})}>
            <Plus className="size-4" aria-hidden />
            Thêm ví
          </Button>
        }
      />

      {isPending ? (
        <LoadingBlock />
      ) : error ? (
        <Alert>{errorMessage(error)}</Alert>
      ) : (
        <div className="flex flex-col gap-6">
          <Card className="bg-gradient-to-br from-brand-700 to-brand-800 text-white dark:border-brand-700">
            <p className="text-sm text-brand-100">Tổng tài sản</p>
            <p className="mt-1 text-3xl font-semibold tabular-nums">
              <Money amount={data.total} className={data.total < 0 ? 'text-red-100' : ''} />
            </p>
            <p className="mt-1 text-sm text-brand-100">{active.length} ví đang dùng</p>
          </Card>

          <WalletGrid wallets={active} onEdit={(w) => setEditing({ wallet: w })} onAction={(kind, wallet) => setPending({ kind, wallet })} />

          <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
            <input type="checkbox" checked={showArchived} onChange={(e) => setShowArchived(e.target.checked)} className="size-4 accent-brand-600" />
            Hiện ví đã lưu trữ
          </label>
          {showArchived &&
            (archived.length ? (
              <WalletGrid wallets={archived} onEdit={(w) => setEditing({ wallet: w })} onAction={(kind, wallet) => setPending({ kind, wallet })} />
            ) : (
              <EmptyState icon={Archive} title="Chưa có ví nào được lưu trữ" />
            ))}
        </div>
      )}

      <WalletFormDialog open={editing !== null} wallet={editing?.wallet} onClose={() => setEditing(null)} />
      <ConfirmDialog
        open={pending !== null}
        title={pending?.kind === 'delete' ? 'Xoá ví?' : pending?.kind === 'archive' ? 'Lưu trữ ví?' : 'Dùng lại ví?'}
        message={
          pending?.kind === 'delete'
            ? `Xoá hẳn ví "${pending.wallet.name}". Chỉ xoá được ví chưa có giao dịch nào.`
            : pending?.kind === 'archive'
              ? `Ví "${pending.wallet.name}" sẽ ẩn khỏi ô chọn khi ghi giao dịch, nhưng vẫn giữ lịch sử và báo cáo.`
              : `Ví "${pending?.wallet.name}" sẽ hiện lại trong danh sách.`
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

function WalletGrid({ wallets, onEdit, onAction }: { wallets: Wallet[]; onEdit: (w: Wallet) => void; onAction: (kind: Pending['kind'], w: Wallet) => void }) {
  if (!wallets.length) return <EmptyState icon={WalletIcon} title="Chưa có ví nào" />
  return (
    <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {wallets.map((w) => {
        const Icon = walletTypeIcons[w.type]
        return (
          <li key={w.id}>
            <Card className={`relative overflow-hidden ${w.isArchived ? 'opacity-70' : ''}`}>
              <span className="absolute inset-y-0 left-0 w-1.5" style={{ backgroundColor: w.color }} aria-hidden />
              <div className="flex items-start gap-3 pl-2">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-full" style={{ backgroundColor: `${w.color}22`, color: w.color }} aria-hidden>
                  <Icon className="size-5" />
                </span>
                <div className="min-w-0 flex-1">
                  <Link to={`/transactions?walletId=${w.id}`} className="block truncate font-semibold hover:underline">
                    {w.name}
                  </Link>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {walletTypeLabels[w.type]}
                    {w.isArchived && ' · đã lưu trữ'}
                  </p>
                  <p className="mt-2 text-xl font-semibold">
                    <Money amount={w.balance} />
                  </p>
                </div>
                <div className="-mt-1 -mr-1 flex">
                  <IconButton label={`Sửa ví ${w.name}`} onClick={() => onEdit(w)}>
                    <Pencil className="size-4" />
                  </IconButton>
                  {w.isArchived ? (
                    <IconButton label={`Dùng lại ví ${w.name}`} onClick={() => onAction('restore', w)}>
                      <ArchiveRestore className="size-4" />
                    </IconButton>
                  ) : (
                    <IconButton label={`Lưu trữ ví ${w.name}`} onClick={() => onAction('archive', w)}>
                      <Archive className="size-4" />
                    </IconButton>
                  )}
                  <IconButton label={`Xoá ví ${w.name}`} onClick={() => onAction('delete', w)} className="hover:text-red-600">
                    <Trash2 className="size-4" />
                  </IconButton>
                </div>
              </div>
            </Card>
          </li>
        )
      })}
    </ul>
  )
}
