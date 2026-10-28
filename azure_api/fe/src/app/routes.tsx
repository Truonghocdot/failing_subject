import type { RouteObject } from 'react-router'
import { GuestOnly, RequireAuth } from '../features/auth/guards'
import { LoginPage } from '../features/auth/LoginPage'
import { RegisterPage } from '../features/auth/RegisterPage'
import { AppLayout } from './layout/AppLayout'
import { page } from './lazyPage'
import { NotFoundPage } from './NotFoundPage'
import { RouteError } from './RouteError'

export const routes: RouteObject[] = [
  {
    errorElement: <RouteError />,
    children: [
      {
        element: <GuestOnly />,
        children: [
          { path: 'login', element: <LoginPage /> },
          { path: 'register', element: <RegisterPage /> },
        ],
      },
      {
        element: <RequireAuth />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { index: true, element: page(() => import('../features/dashboard/DashboardPage'), 'DashboardPage') },
              { path: 'transactions', element: page(() => import('../features/transactions/TransactionsPage'), 'TransactionsPage') },
              { path: 'wallets', element: page(() => import('../features/wallets/WalletsPage'), 'WalletsPage') },
              { path: 'categories', element: page(() => import('../features/categories/CategoriesPage'), 'CategoriesPage') },
              { path: 'budgets', element: page(() => import('../features/budgets/BudgetsPage'), 'BudgetsPage') },
              { path: 'reports', element: page(() => import('../features/reports/ReportsPage'), 'ReportsPage') },
              { path: 'settings', element: page(() => import('../features/settings/SettingsPage'), 'SettingsPage') },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
]
