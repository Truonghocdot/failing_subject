import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiResponse } from '../../lib/api'
import type { PagedTransactions, Transaction, TransactionKind } from '../../lib/types'
import { invalidateLedger } from '../wallets/api'

export type TransactionFilters = {
  from: string
  to: string
  walletId?: number
  categoryId?: number
  kind?: TransactionKind
  q?: string
  page?: number
  pageSize?: number
}

export type TransactionInput = {
  kind: TransactionKind
  walletId: number
  toWalletId: number | null
  categoryId: number | null
  amount: number
  occurredOn: string
  note: string | null
}

export const transactionKindLabels: Record<TransactionKind, string> = { expense: 'Chi', income: 'Thu', transfer: 'Chuyển tiền' }

export function toQueryString(f: TransactionFilters): string {
  const p = new URLSearchParams({ from: f.from, to: f.to })
  if (f.walletId) p.set('walletId', String(f.walletId))
  if (f.categoryId) p.set('categoryId', String(f.categoryId))
  if (f.kind) p.set('kind', f.kind)
  if (f.q) p.set('q', f.q)
  if (f.page && f.page > 1) p.set('page', String(f.page))
  if (f.pageSize) p.set('pageSize', String(f.pageSize))
  return p.toString()
}

export function useTransactions(filters: TransactionFilters) {
  return useQuery({
    queryKey: ['transactions', 'list', filters],
    queryFn: ({ signal }) => api<PagedTransactions>(`/transactions?${toQueryString(filters)}`, { signal }),
    placeholderData: keepPreviousData, // đổi trang / bộ lọc không nháy trắng
  })
}

export function useSaveTransaction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id?: number; input: TransactionInput }) =>
      id
        ? api<Transaction>(`/transactions/${id}`, { method: 'PUT', body: input })
        : api<Transaction>('/transactions', { method: 'POST', body: input }),
    onSuccess: () => invalidateLedger(queryClient),
  })
}

export function useDeleteTransaction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api<void>(`/transactions/${id}`, { method: 'DELETE' }),
    onSuccess: () => invalidateLedger(queryClient),
  })
}

/** Tải CSV qua fetch (cần Bearer token) rồi kích hoạt tải file bằng link tạm. */
export async function downloadTransactionsCsv(filters: TransactionFilters) {
  const { page: _page, pageSize: _pageSize, ...rest } = filters
  const res = await apiResponse(`/transactions/export?${toQueryString(rest)}`)
  const disposition = res.headers.get('Content-Disposition') ?? ''
  const name = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1] ?? /filename="?([^";]+)/i.exec(disposition)?.[1] ?? 'giao-dich.csv'
  const url = URL.createObjectURL(await res.blob())
  const link = Object.assign(document.createElement('a'), { href: url, download: decodeURIComponent(name) })
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
