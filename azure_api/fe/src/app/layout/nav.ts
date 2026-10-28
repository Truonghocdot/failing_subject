import { ArrowLeftRight, ChartPie, LayoutDashboard, PiggyBank, Settings, Tags, Wallet, type LucideIcon } from 'lucide-react'

export type NavItem = { to: string; label: string; icon: LucideIcon }

export const navItems: NavItem[] = [
  { to: '/', label: 'Tổng quan', icon: LayoutDashboard },
  { to: '/transactions', label: 'Giao dịch', icon: ArrowLeftRight },
  { to: '/wallets', label: 'Ví', icon: Wallet },
  { to: '/categories', label: 'Danh mục', icon: Tags },
  { to: '/budgets', label: 'Ngân sách', icon: PiggyBank },
  { to: '/reports', label: 'Báo cáo', icon: ChartPie },
  { to: '/settings', label: 'Cài đặt', icon: Settings },
]
