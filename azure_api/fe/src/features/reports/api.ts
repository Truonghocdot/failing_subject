import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'
import type { CategoryKind, CategoryReportItem, ReportSummary, TrendPoint } from '../../lib/types'

export type DateRange = { from: string; to: string }
export type TrendGroup = 'day' | 'month'

const range = (r: DateRange) => `from=${r.from}&to=${r.to}`

// Mọi key bắt đầu bằng 'reports' → invalidateLedger làm mới hết khi giao dịch đổi
export function useReportSummary(r: DateRange) {
  return useQuery({
    queryKey: ['reports', 'summary', r],
    queryFn: ({ signal }) => api<ReportSummary>(`/reports/summary?${range(r)}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useCategoryReport(r: DateRange, kind: CategoryKind) {
  return useQuery({
    queryKey: ['reports', 'by-category', r, kind],
    queryFn: ({ signal }) => api<CategoryReportItem[]>(`/reports/by-category?${range(r)}&kind=${kind}`, { signal }),
    placeholderData: keepPreviousData,
  })
}

export function useTrend(r: DateRange, groupBy: TrendGroup) {
  return useQuery({
    queryKey: ['reports', 'trend', r, groupBy],
    queryFn: ({ signal }) => api<TrendPoint[]>(`/reports/trend?${range(r)}&groupBy=${groupBy}`, { signal }),
    placeholderData: keepPreviousData,
  })
}
