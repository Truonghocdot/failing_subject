import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../lib/api'
import type { Budget, BudgetStatus } from '../../lib/types'

export const budgetKeys = {
  all: ['budgets'] as const,
  month: (month: string) => ['budgets', month] as const,
}

export const budgetStatusLabels: Record<BudgetStatus, string> = { ok: 'Trong hạn mức', warning: 'Sắp hết', over: 'Vượt ngân sách' }

export type BudgetInput = { month: string; categoryId: number; amount: number }

export function useBudgets(month: string) {
  return useQuery({
    queryKey: budgetKeys.month(month),
    queryFn: ({ signal }) => api<Budget[]>(`/budgets?month=${month}`, { signal }),
    placeholderData: keepPreviousData, // đổi tháng không nháy trắng
  })
}

export function useSaveBudget() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: BudgetInput) => api<Budget>('/budgets', { method: 'PUT', body: input }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: budgetKeys.all }),
  })
}

export function useDeleteBudget() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api<void>(`/budgets/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: budgetKeys.all }),
  })
}

export function useCopyBudgets() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { from: string; to: string }) => api<{ created: number }>('/budgets/copy', { method: 'POST', body }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: budgetKeys.all }),
  })
}
