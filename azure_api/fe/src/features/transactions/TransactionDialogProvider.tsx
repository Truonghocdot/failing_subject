import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { Transaction, TransactionKind } from '../../lib/types'
import { TransactionFormDialog } from './TransactionFormDialog'

type Ctx = { openCreate: (kind?: TransactionKind) => void; openEdit: (tx: Transaction) => void }

const TransactionDialogContext = createContext<Ctx | null>(null)

/** 1 hộp thoại giao dịch dùng chung: nút "+ Thêm giao dịch" ở mọi trang và bấm vào 1 dòng để sửa. */
export function TransactionDialogProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ open: boolean; transaction?: Transaction; kind?: TransactionKind }>({ open: false })

  const openCreate = useCallback((kind?: TransactionKind) => setState({ open: true, kind }), [])
  const openEdit = useCallback((transaction: Transaction) => setState({ open: true, transaction }), [])
  const value = useMemo(() => ({ openCreate, openEdit }), [openCreate, openEdit])

  return (
    <TransactionDialogContext.Provider value={value}>
      {children}
      <TransactionFormDialog
        open={state.open}
        transaction={state.transaction}
        defaultKind={state.kind}
        onClose={() => setState((s) => ({ ...s, open: false }))}
      />
    </TransactionDialogContext.Provider>
  )
}

export function useTransactionDialog(): Ctx {
  const ctx = useContext(TransactionDialogContext)
  if (!ctx) throw new Error('useTransactionDialog phải nằm trong <TransactionDialogProvider>')
  return ctx
}
