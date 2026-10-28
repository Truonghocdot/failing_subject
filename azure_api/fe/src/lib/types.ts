// Khớp DTO backend (docs/03-backend-dotnet.md §3)

export type User = { id: string; email: string; displayName: string; currency: string; createdAt: string }

export type AuthResponse = { accessToken: string; expiresIn: number; user: User }

export type WalletType = 'cash' | 'bank' | 'ewallet' | 'credit'

export type Wallet = {
  id: number
  name: string
  type: WalletType
  openingBalance: number
  balance: number
  color: string
  isArchived: boolean
  sortOrder: number
}

export type WalletList = { items: Wallet[]; total: number }

export type CategoryKind = 'expense' | 'income'

export type Category = {
  id: number
  kind: CategoryKind
  name: string
  icon: string
  color: string
  isArchived: boolean
  sortOrder: number
}

export type TransactionKind = 'expense' | 'income' | 'transfer'

export type WalletRef = { id: number; name: string; color: string }
export type CategoryRef = { id: number; name: string; icon: string; color: string }

export type Transaction = {
  id: number
  kind: TransactionKind
  amount: number
  occurredOn: string
  note: string | null
  wallet: WalletRef
  toWallet: WalletRef | null
  category: CategoryRef | null
  createdAt: string
}

export type PagedTransactions = {
  items: Transaction[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
  totals: { income: number; expense: number }
}

export type BudgetStatus = 'ok' | 'warning' | 'over'

export type Budget = {
  id: number
  month: string
  category: CategoryRef
  amount: number
  spent: number
  remaining: number
  percent: number
  status: BudgetStatus
}

export type ReportSummary = {
  from: string
  to: string
  income: number
  expense: number
  net: number
  transactionCount: number
  dailyAverageExpense: number
}

export type CategoryReportItem = { category: CategoryRef; total: number; percent: number; count: number }

export type TrendPoint = { period: string; income: number; expense: number }
