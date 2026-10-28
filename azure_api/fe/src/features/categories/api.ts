import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api'
import type { Category, CategoryKind } from '../../lib/types'
import { invalidateLedger } from '../wallets/api'

export const categoryKeys = {
  all: ['categories'] as const,
  list: (includeArchived: boolean) => ['categories', { includeArchived }] as const,
}

export const categoryKindLabels: Record<CategoryKind, string> = { expense: 'Chi', income: 'Thu' }

export const categoryKindOptions = [
  { value: 'expense', label: 'Khoản chi' },
  { value: 'income', label: 'Khoản thu' },
] as const satisfies readonly { value: CategoryKind; label: string }[]

export type CategoryInput = { name: string; kind: CategoryKind; icon: string; color: string; isArchived: boolean }

/** Lấy cả 2 loại 1 lần (≈ vài chục dòng) rồi lọc ở client – form giao dịch đổi tab Chi/Thu không phải chờ tải. */
export function useCategories(includeArchived = false) {
  return useQuery({
    queryKey: categoryKeys.list(includeArchived),
    queryFn: ({ signal }) => api<Category[]>(`/categories?includeArchived=${includeArchived}`, { signal }),
    staleTime: 5 * 60_000,
  })
}

export function useSaveCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id?: number; input: CategoryInput }) =>
      id ? api<Category>(`/categories/${id}`, { method: 'PUT', body: input }) : api<Category>('/categories', { method: 'POST', body: input }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: categoryKeys.all })
      await invalidateLedger(queryClient) // tên / màu danh mục hiện trong giao dịch, báo cáo
    },
  })
}

export function useDeleteCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api<void>(`/categories/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: categoryKeys.all }),
  })
}
