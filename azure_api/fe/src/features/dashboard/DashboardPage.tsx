import { ChevronRight, PiggyBank, Plus, ReceiptText } from 'lucide-react'
import { useMemo, type ReactNode } from 'react'
import { Link } from 'react-router'
import { Button } from '../../components/Button'
import { Alert, Card, EmptyState, LoadingBlock } from '../../components/Feedback'
import { Money } from '../../components/Money'
import { PageHeader } from '../../components/PageHeader'
import { StatTile } from '../../components/StatTile'
import { errorMessage } from '../../lib/api'
import { currentMonth, formatMonth, monthRange, shiftMonth } from '../../lib/format'
import { useBudgets } from '../budgets/api'
import { BudgetMeter } from '../budgets/BudgetMeter'
import { useCategoryReport, useReportSummary, useTrend } from '../reports/api'
import { CategoryDonut, TrendChart } from '../reports/charts'
import { useTransactions } from '../transactions/api'
import { useTransactionDialog } from '../transactions/TransactionDialogProvider'
import { TransactionRow } from '../transactions/TransactionRow'
import { useWallets } from '../wallets/api'

const RECENT = 5
const BUDGETS_SHOWN = 4

export function DashboardPage() {
  const month = currentMonth()
  const range = useMemo(() => monthRange(month), [month])
  // 6 tháng gần nhất (gồm tháng này) / 12 tháng cho "giao dịch gần nhất" – đều ≤ 366 ngày
  const sixMonths = useMemo(() => ({ from: monthRange(shiftMonth(month, -5)).from, to: range.to }), [month, range])
  const recentFilters = useMemo(() => ({ from: monthRange(shiftMonth(month, -11)).from, to: range.to, pageSize: RECENT }), [month, range])

  const summary = useReportSummary(range)
  const wallets = useWallets()
  const byCategory = useCategoryReport(range, 'expense')
  const trend = useTrend(sixMonths, 'month')
  const recent = useTransactions(recentFilters)
  const budgets = useBudgets(month)
  const { openCreate, openEdit } = useTransactionDialog()

  // Dashboard: ngân sách gần chạm / đã vượt lên đầu (docs/02 §5)
  const topBudgets = useMemo(() => [...(budgets.data ?? [])].sort((a, b) => b.percent - a.percent).slice(0, BUDGETS_SHOWN), [budgets.data])
  const error = summary.error ?? wallets.error

  return (
    <>
      <PageHeader
        title="Tổng quan"
        description={formatMonth(month)}
        actions={
          <Button onClick={() => openCreate('expense')}>
            <Plus className="size-4" aria-hidden />
            Thêm giao dịch
          </Button>
        }
      />

      {error ? (
        <Alert>{errorMessage(error)}</Alert>
      ) : !summary.data || !wallets.data ? (
        <LoadingBlock />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatTile label="Thu tháng này">
              <Money amount={summary.data.income} kind="income" />
            </StatTile>
            <StatTile label="Chi tháng này">
              <Money amount={summary.data.expense} kind="expense" />
            </StatTile>
            <StatTile label="Chênh lệch">
              <Money amount={summary.data.net} />
            </StatTile>
            <StatTile label="Tổng số dư">
              <Link to="/wallets" className="hover:underline">
                <Money amount={wallets.data.total} />
              </Link>
            </StatTile>
          </div>

          {summary.data.transactionCount === 0 && recent.data?.totalItems === 0 && (
            <EmptyState icon={ReceiptText} title="Chưa có giao dịch – thêm khoản chi đầu tiên">
              <p>Ghi lại mỗi khoản chi, thu để xem chi tiêu theo danh mục và so với ngân sách.</p>
              <Button size="sm" className="mt-3" onClick={() => openCreate('expense')}>
                Thêm khoản chi
              </Button>
            </EmptyState>
          )}

          <div className="grid gap-4 lg:grid-cols-2">
            <Section title="Chi theo danh mục" link={{ to: '/reports', label: 'Báo cáo' }}>
              {byCategory.isPending ? (
                <LoadingBlock />
              ) : byCategory.data?.length ? (
                <CategoryDonut items={byCategory.data} label="Tổng chi" />
              ) : (
                <p className="py-8 text-center text-sm text-slate-500 dark:text-slate-400">Tháng này chưa có khoản chi nào</p>
              )}
            </Section>

            <Section title="Ngân sách tháng này" link={{ to: '/budgets', label: 'Tất cả' }}>
              {budgets.isPending ? (
                <LoadingBlock />
              ) : topBudgets.length ? (
                <ul className="space-y-4">
                  {topBudgets.map((b) => (
                    <li key={b.id}>
                      <BudgetMeter budget={b} compact />
                    </li>
                  ))}
                </ul>
              ) : (
                <div className="flex flex-col items-center gap-2 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  <PiggyBank className="size-8 text-slate-400" aria-hidden />
                  <p>Chưa đặt ngân sách cho tháng này</p>
                  <Link to="/budgets" className="font-medium text-brand-700 hover:underline dark:text-brand-100">
                    Đặt ngân sách
                  </Link>
                </div>
              )}
            </Section>
          </div>

          <Section title="Thu chi 6 tháng">{trend.data ? <TrendChart points={trend.data} height={220} /> : <LoadingBlock />}</Section>

          <Section title="Giao dịch gần nhất" link={{ to: '/transactions', label: 'Xem tất cả' }} flush>
            {recent.isPending ? (
              <LoadingBlock />
            ) : recent.data?.items.length ? (
              <ul className="divide-y divide-slate-100 dark:divide-slate-800">
                {recent.data.items.map((t) => (
                  <li key={t.id}>
                    <TransactionRow tx={t} onClick={() => openEdit(t)} />
                  </li>
                ))}
              </ul>
            ) : (
              <p className="px-4 py-8 text-center text-sm text-slate-500 dark:text-slate-400">Chưa có giao dịch nào</p>
            )}
          </Section>
        </div>
      )}
    </>
  )
}

function Section({ title, link, flush, children }: { title: string; link?: { to: string; label: string }; flush?: boolean; children: ReactNode }) {
  return (
    <Card className={flush ? 'p-0' : ''}>
      <div className={`flex items-center justify-between gap-3 ${flush ? 'border-b border-slate-100 px-4 py-3 dark:border-slate-800' : 'mb-3'}`}>
        <h2 className="font-semibold">{title}</h2>
        {link && (
          <Link to={link.to} className="flex items-center text-sm text-brand-700 hover:underline dark:text-brand-100">
            {link.label}
            <ChevronRight className="size-4" aria-hidden />
          </Link>
        )}
      </div>
      {children}
    </Card>
  )
}
