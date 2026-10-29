// Khớp DTO backend (backend/Contracts/*.cs) – enum dạng snake_case, ngày "YYYY-MM-DD", thời điểm ISO UTC.

export type IsoDate = string
export type IsoDateTime = string

export type UserRole = 'guest' | 'staff' | 'admin'

export type User = { id: string; email: string; fullName: string; phone: string | null; role: UserRole; createdAt: IsoDateTime }

export type AuthResponse = { accessToken: string; expiresIn: number; user: User }

// --- Tìm kiếm & chi tiết ---

export type Province = { province: string; homestays: number }

export type Paged<T> = { items: T[]; page: number; pageSize: number; total: number }

export type StaySort = 'relevance' | 'price' | 'rating'

export type StayCard = {
  id: number
  slug: string
  name: string
  province: string
  address: string
  coverUrl: string | null
  amenities: string[]
  rating: number | null
  reviewCount: number
  availableRooms: number | null
  priceFrom: number
}

export type StayPolicy = {
  checkInTime: string
  checkOutTime: string
  depositPercent: number
  holdHours: number
  freeCancelDays: number
  partialRefundDays: number
  partialRefundPercent: number
}

export type Photo = { id: number; url: string; width: number; height: number; sortOrder: number }

export type StayRoom = {
  id: number
  name: string
  description: string
  bedInfo: string | null
  areaM2: number | null
  baseGuests: number
  maxGuests: number
  minNights: number
  basePrice: number
  weekendPrice: number | null
  extraGuestFee: number
  cleaningFee: number
}

export type StayDetail = {
  id: number
  slug: string
  name: string
  province: string
  address: string
  description: string
  amenities: string[]
  policy: StayPolicy
  photos: Photo[]
  rooms: StayRoom[]
  rating: number | null
  reviewCount: number
}

export type PriceSource = 'override' | 'weekend' | 'base'

export type CalendarDay = { date: IsoDate; available: boolean; price: number; source: PriceSource }

export type StayCalendar = { from: IsoDate; to: IsoDate; rooms: { roomId: number; name: string; days: CalendarDay[] }[] }

export type NightPrice = { date: IsoDate; price: number; source: PriceSource }

export type Quote = {
  roomId: number
  homestayId: number
  checkIn: IsoDate
  checkOut: IsoDate
  guests: number
  nights: number
  nightPrices: NightPrice[]
  roomTotal: number
  extraGuestTotal: number
  cleaningFee: number
  total: number
  depositPercent: number
  depositAmount: number
  available: boolean
}

// --- Đặt phòng ---

export type BookingStatus = 'pending' | 'confirmed' | 'checked_in' | 'checked_out' | 'cancelled' | 'expired' | 'no_show'

export type BookingSource = 'web' | 'phone' | 'walk_in' | 'ota'

export type PaymentStatus = 'unpaid' | 'deposit_paid' | 'paid' | 'refunded'

export type Money = { paid: number; extras: number; refunded: number; amountDue: number; pendingRefund: number; status: PaymentStatus }

export type BookingSummary = {
  id: number
  code: string
  homestayId: number
  homestayName: string
  roomId: number
  roomName: string
  guestName: string
  guestPhone: string
  guests: number
  nights: number
  checkIn: IsoDate
  checkOut: IsoDate
  total: number
  depositAmount: number
  money: Money
  status: BookingStatus
  source: BookingSource
  holdUntil: IsoDateTime | null
  createdAt: IsoDateTime
}

export type TransferInfo = { bankName: string; accountNumber: string; accountName: string; content: string; amount: number }

export type BookingEvent = {
  id: number
  type: string
  fromStatus: BookingStatus | null
  toStatus: BookingStatus | null
  message: string | null
  actor: string | null
  createdAt: IsoDateTime
}

export type Booking = {
  id: number
  code: string
  homestayId: number
  homestayName: string
  homestaySlug: string
  homestayAddress: string
  roomId: number
  roomName: string
  guestName: string
  guestPhone: string
  guestEmail: string | null
  guests: number
  nights: number
  checkIn: IsoDate
  checkOut: IsoDate
  checkInTime: string
  checkOutTime: string
  nightPrices: { night: IsoDate; price: number }[]
  roomTotal: number
  extraGuestTotal: number
  cleaningFee: number
  total: number
  depositAmount: number
  refundAmount: number
  money: Money
  status: BookingStatus
  source: BookingSource
  guestNote: string | null
  cancelReason: string | null
  holdUntil: IsoDateTime | null
  confirmedAt: IsoDateTime | null
  checkedInAt: IsoDateTime | null
  checkedOutAt: IsoDateTime | null
  cancelledAt: IsoDateTime | null
  policy: StayPolicy
  transfer: TransferInfo | null
  events: BookingEvent[]
  createdAt: IsoDateTime
  /** Chỉ nhân viên thấy (null với khách). */
  internalNote?: string | null
  userId?: string | null
  payments?: Payment[]
  review: Review | null
  /** Khách được viết đánh giá (đã trả phòng ≤ 30 ngày, chưa đánh giá). */
  canReview: boolean
}

export type Review = { id: number; rating: number; comment: string | null; createdAt: IsoDateTime }

export type PublicReview = { id: number; rating: number; comment: string | null; author: string; roomName: string; stayedAt: IsoDate; createdAt: IsoDateTime }

export type CancelPreview = {
  canCancel: boolean
  reason: string | null
  daysBefore: number
  paid: number
  refundPercent: number
  refundAmount: number
}

// --- Quản trị ---

export type PaymentKind = 'deposit' | 'balance' | 'extra' | 'refund'
export type PaymentMethod = 'cash' | 'bank_transfer' | 'card' | 'ewallet'

export type Payment = { id: number; kind: PaymentKind; method: PaymentMethod; amount: number; paidAt: IsoDateTime; note: string | null; recordedBy: string | null }

export type HomestayStatus = 'active' | 'hidden'
export type RoomStatus = 'active' | 'inactive'

export type AdminHomestaySummary = {
  id: number
  slug: string
  name: string
  province: string
  status: HomestayStatus
  roomCount: number
  activeRoomCount: number
  coverUrl: string | null
  updatedAt: IsoDateTime
}

export type AdminRoom = {
  id: number
  homestayId: number
  name: string
  description: string
  bedInfo: string | null
  areaM2: number | null
  baseGuests: number
  maxGuests: number
  minNights: number
  basePrice: number
  weekendPrice: number | null
  extraGuestFee: number
  cleaningFee: number
  status: RoomStatus
  sortOrder: number
}

export type AdminHomestay = {
  id: number
  slug: string
  name: string
  province: string
  address: string
  description: string
  amenities: string[]
  checkInTime: string
  checkOutTime: string
  depositPercent: number
  holdHours: number
  freeCancelDays: number
  partialRefundDays: number
  partialRefundPercent: number
  status: HomestayStatus
  photos: Photo[]
  rooms: AdminRoom[]
  createdAt: IsoDateTime
  updatedAt: IsoDateTime
}

export type CalendarBooking = {
  id: number
  code: string
  guestName: string
  guests: number
  status: BookingStatus
  paymentStatus: PaymentStatus
  checkIn: IsoDate
  checkOut: IsoDate
  source: BookingSource
}

export type CalendarBlock = { id: number; from: IsoDate; to: IsoDate; reason: string }

export type AdminCalendarRoom = {
  roomId: number
  homestayId: number
  homestayName: string
  name: string
  status: RoomStatus
  prices: number[]
  bookings: CalendarBooking[]
  blocks: CalendarBlock[]
}

export type AdminCalendar = { from: IsoDate; to: IsoDate; rooms: AdminCalendarRoom[] }

export type OccupancyDay = { date: IsoDate; sold: number; sellable: number; rate: number }

export type Dashboard = {
  date: IsoDate
  arrivals: BookingSummary[]
  departures: BookingSummary[]
  inHouse: BookingSummary[]
  pendingHolds: BookingSummary[]
  next7Days: OccupancyDay[]
}

export type Block = { id: number; roomId: number; roomName: string; homestayId: number; from: IsoDate; to: IsoDate; reason: string; createdAt: IsoDateTime }

export type RateDay = { date: IsoDate; price: number; source: PriceSource; note: string | null }
export type RoomRates = { roomId: number; roomName: string; days: RateDay[] }

export type Staff = {
  id: string
  email: string
  fullName: string
  phone: string | null
  role: UserRole
  isActive: boolean
  lastLoginAt: IsoDateTime | null
  createdAt: IsoDateTime
}

export type Guest = {
  phone: string
  name: string
  email: string | null
  hasAccount: boolean
  bookings: number
  stays: number
  upcoming: number
  cancelled: number
  paid: number
  lastCheckIn: IsoDate
}

export type AdminReview = {
  id: number
  bookingId: number
  bookingCode: string
  homestayId: number
  homestayName: string
  guestName: string
  rating: number
  comment: string | null
  isHidden: boolean
  createdAt: IsoDateTime
}

export type ReportMetrics = { sellableNights: number; soldNights: number; occupancy: number; roomRevenue: number; adr: number; revPar: number }

export type ReportRow = { key: string; label: string; metrics: ReportMetrics; collected: number; bookings: number; cancelled: number }

export type SourceRow = { source: BookingSource; bookings: number; cancelled: number; soldNights: number; roomRevenue: number }

export type Report = {
  from: IsoDate
  to: IsoDate
  homestayId: number | null
  total: ReportMetrics
  collected: number
  refunded: number
  bookings: number
  cancelled: number
  noShow: number
  expired: number
  cancelRate: number
  months: ReportRow[]
  homestays: ReportRow[]
  sources: SourceRow[]
}
