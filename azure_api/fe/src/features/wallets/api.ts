import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api'
import type { Wallet, WalletList, WalletType } from '../../lib/types'

export const walletKeys = {
  all: ['wallets'] as const,
  list: (includeArchived: boolean) => ['wallets', { includeArchived }] as const,
}

export const walletTypeLabels: Record<WalletType, string> = {
  cash: 'Tiền mặt',
  bank: 'Ngân hàng',
  ewallet: 'Ví điện tử',
  credit: 'Thẻ tín dụng',
}

export type WalletInput = { name: string; type: WalletType; openingBalance: number; color: string; isArchived: boolean }

export function useWallets(includeArchived = false) {
  return useQuery({
    queryKey: walletKeys.list(includeArchived),
    queryFn: ({ signal }) => api<WalletList>(`/wallets?includeArchived=${includeArchived}`, { signal }),
  })
}

/** Số dư ví đổi theo mọi thay đổi giao dịch → các nơi ghi giao dịch cũng gọi hàm này. */
export function invalidateLedger(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: walletKeys.all }),
    queryClient.invalidateQueries({ queryKey: ['transactions'] }),
    queryClient.invalidateQueries({ queryKey: ['budgets'] }),
    queryClient.invalidateQueries({ queryKey: ['reports'] }),
  ])
}

export function useSaveWallet() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id?: number; input: WalletInput }) =>
      id ? api<Wallet>(`/wallets/${id}`, { method: 'PUT', body: input }) : api<Wallet>('/wallets', { method: 'POST', body: input }),
    // Đổi tên / màu ví hiện lại trong danh sách giao dịch → làm mới cả giao dịch
    onSuccess: () => invalidateLedger(queryClient),
  })
}

export function useDeleteWallet() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api<void>(`/wallets/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: walletKeys.all }),
  })
}
