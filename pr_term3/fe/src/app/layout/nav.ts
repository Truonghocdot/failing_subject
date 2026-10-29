import {
  BedDouble,
  CalendarDays,
  ChartColumn,
  ClipboardList,
  Hotel,
  LayoutDashboard,
  MessageSquareText,
  Tag,
  UserCog,
  Users,
  type LucideIcon,
} from 'lucide-react'

export type NavItem = { to: string; label: string; icon: LucideIcon; adminOnly?: boolean }

/** Khu khách – thanh trên cùng. */
export const publicNav = [
  { to: '/stays', label: 'Tìm homestay' },
  { to: '/bookings', label: 'Đặt phòng của tôi' },
  { to: '/lookup', label: 'Tra cứu đặt phòng' },
] as const

/** Khu quản trị – adminOnly: chỉ vai trò admin (Phase 5 ẩn với lễ tân). */
export const adminNav: NavItem[] = [
  { to: '/admin', label: 'Hôm nay', icon: LayoutDashboard },
  { to: '/admin/calendar', label: 'Lịch phòng', icon: CalendarDays },
  { to: '/admin/bookings', label: 'Đặt phòng', icon: ClipboardList },
  { to: '/admin/guests', label: 'Khách', icon: Users },
  { to: '/admin/homestays', label: 'Homestay & phòng', icon: Hotel, adminOnly: true },
  { to: '/admin/rates', label: 'Giá theo ngày', icon: Tag, adminOnly: true },
  { to: '/admin/reviews', label: 'Đánh giá', icon: MessageSquareText, adminOnly: true },
  { to: '/admin/reports', label: 'Báo cáo', icon: ChartColumn, adminOnly: true },
  { to: '/admin/users', label: 'Nhân viên', icon: UserCog, adminOnly: true },
]

export const BrandIcon = BedDouble
