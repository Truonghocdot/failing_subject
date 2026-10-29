import { addDays, nightsBetween, today } from '../lib/format'
import type { AdminHomestay, AdminHomestaySummary, AuthResponse, Booking, BookingSummary, Quote, StayCard, StayDetail } from '../lib/types'

export const guestUser = { id: 'u1', email: 'an@example.com', fullName: 'Nguyễn An', phone: '0912345678', role: 'guest', createdAt: '2026-01-01T00:00:00Z' } as const

export const session: AuthResponse = { accessToken: 'token-1', expiresIn: 900, user: { ...guestUser } }

export const policy = { checkInTime: '14:00', checkOutTime: '12:00', depositPercent: 30, holdHours: 24, freeCancelDays: 7, partialRefundDays: 3, partialRefundPercent: 50 }

export const stayCard: StayCard = {
  id: 1,
  slug: 'doi-thong',
  name: 'Đồi Thông Homestay',
  province: 'Lâm Đồng',
  address: '12 Hoa Hồng, Đà Lạt',
  coverUrl: null,
  amenities: ['wifi', 'parking'],
  rating: 4.6,
  reviewCount: 8,
  availableRooms: null,
  priceFrom: 800_000,
}

export const stay: StayDetail = {
  id: 1,
  slug: 'doi-thong',
  name: 'Đồi Thông Homestay',
  province: 'Lâm Đồng',
  address: '12 Hoa Hồng, Đà Lạt',
  description: 'Nhà gỗ giữa đồi thông.',
  amenities: ['wifi', 'parking'],
  policy,
  photos: [],
  rooms: [
    { id: 11, name: 'Phòng Gác Mái', description: '', bedInfo: '1 giường đôi', areaM2: 25, baseGuests: 2, maxGuests: 4, minNights: 1, basePrice: 800_000, weekendPrice: 1_000_000, extraGuestFee: 150_000, cleaningFee: 100_000 },
    { id: 12, name: 'Phòng Vườn', description: '', bedInfo: null, areaM2: null, baseGuests: 2, maxGuests: 2, minNights: 1, basePrice: 700_000, weekendPrice: null, extraGuestFee: 0, cleaningFee: 0 },
  ],
  rating: 4.6,
  reviewCount: 8,
}

export const checkIn = addDays(today(), 10)
export const checkOut = addDays(today(), 12)

export function quote(total = 1_700_000, overrides: Partial<Quote> = {}): Quote {
  return {
    roomId: 11,
    homestayId: 1,
    checkIn,
    checkOut,
    guests: 2,
    nights: nightsBetween(checkIn, checkOut),
    nightPrices: [
      { date: checkIn, price: 800_000, source: 'base' },
      { date: addDays(checkIn, 1), price: 800_000, source: 'base' },
    ],
    roomTotal: total - 100_000,
    extraGuestTotal: 0,
    cleaningFee: 100_000,
    total,
    depositPercent: 30,
    depositAmount: Math.ceil((total * 0.3) / 1000) * 1000,
    available: true,
    ...overrides,
  }
}

export function booking(overrides: Partial<Booking> = {}): Booking {
  return {
    id: 100,
    code: 'HS-AB12-CD34',
    homestayId: 1,
    homestayName: stay.name,
    homestaySlug: stay.slug,
    homestayAddress: stay.address,
    roomId: 11,
    roomName: 'Phòng Gác Mái',
    guestName: guestUser.fullName,
    guestPhone: guestUser.phone,
    guestEmail: guestUser.email,
    guests: 2,
    nights: 2,
    checkIn,
    checkOut,
    checkInTime: '14:00',
    checkOutTime: '12:00',
    nightPrices: [],
    roomTotal: 1_600_000,
    extraGuestTotal: 0,
    cleaningFee: 100_000,
    total: 1_700_000,
    depositAmount: 510_000,
    refundAmount: 0,
    money: { paid: 0, extras: 0, refunded: 0, amountDue: 1_700_000, pendingRefund: 0, status: 'unpaid' },
    status: 'pending',
    source: 'web',
    guestNote: null,
    cancelReason: null,
    holdUntil: new Date(Date.now() + 2 * 3600_000).toISOString(),
    confirmedAt: null,
    checkedInAt: null,
    checkedOutAt: null,
    cancelledAt: null,
    policy,
    transfer: { bankName: 'Vietcombank', accountNumber: '0123456789', accountName: 'CONG TY HOMESTAY DEMO', content: 'HS-AB12-CD34', amount: 510_000 },
    events: [{ id: 1, type: 'created', fromStatus: null, toStatus: 'pending', message: null, actor: null, createdAt: new Date().toISOString() }],
    createdAt: new Date().toISOString(),
    review: null,
    canReview: false,
    ...overrides,
  }
}

/** Mọi trang khách đều gọi /health (chân trang) và refresh lúc khởi động. */
export const base = {
  'GET /health': [200, { status: 'ok', database: 'up' }],
  'GET /api/provinces': [200, [{ province: 'Lâm Đồng', homestays: 1 }]],
} as const

export const loggedIn = { ...base, 'POST /api/auth/refresh': [200, session] } as const

// --- Khu quản trị ---

export const adminSession: AuthResponse = { accessToken: 'token-admin', expiresIn: 900, user: { ...guestUser, id: 'a1', email: 'admin@example.com', fullName: 'Quản Trị', role: 'admin' } }
export const staffSession: AuthResponse = { accessToken: 'token-staff', expiresIn: 900, user: { ...guestUser, id: 's1', email: 'le.tan@example.com', fullName: 'Lễ Tân', role: 'staff' } }

export const asAdmin = { ...base, 'POST /api/auth/refresh': [200, adminSession] } as const
export const asStaff = { ...base, 'POST /api/auth/refresh': [200, staffSession] } as const

export const adminHomestay: AdminHomestay = {
  id: 1,
  slug: 'doi-thong',
  name: 'Đồi Thông Homestay',
  province: 'Lâm Đồng',
  address: '12 Hoa Hồng, Đà Lạt',
  description: '',
  amenities: ['wifi'],
  checkInTime: '14:00',
  checkOutTime: '12:00',
  depositPercent: 30,
  holdHours: 24,
  freeCancelDays: 7,
  partialRefundDays: 3,
  partialRefundPercent: 50,
  status: 'active',
  photos: [
    { id: 1, url: '/uploads/1/a.jpg', width: 800, height: 600, sortOrder: 0 },
    { id: 2, url: '/uploads/1/b.jpg', width: 800, height: 600, sortOrder: 1 },
    { id: 3, url: '/uploads/1/c.jpg', width: 800, height: 600, sortOrder: 2 },
  ],
  rooms: stay.rooms.map((r, i) => ({ ...r, homestayId: 1, status: 'active', sortOrder: i })),
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

export const homestaySummaries: AdminHomestaySummary[] = [
  { id: 1, slug: 'doi-thong', name: 'Đồi Thông Homestay', province: 'Lâm Đồng', status: 'active', roomCount: 2, activeRoomCount: 2, coverUrl: null, updatedAt: '2026-01-01T00:00:00Z' },
]

export function summary(overrides: Partial<BookingSummary> = {}): BookingSummary {
  const b = booking()
  return {
    id: b.id, code: b.code, homestayId: 1, homestayName: b.homestayName, roomId: 11, roomName: b.roomName, guestName: b.guestName, guestPhone: b.guestPhone,
    guests: 2, nights: 2, checkIn, checkOut, total: b.total, depositAmount: b.depositAmount, money: b.money, status: 'pending', source: 'web',
    holdUntil: b.holdUntil, createdAt: b.createdAt, ...overrides,
  }
}
