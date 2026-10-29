import { CalendarDays, MapPin, Search, Users } from 'lucide-react'
import { Link } from 'react-router'

const provinces = ['Đà Lạt', 'Sa Pa', 'Hội An', 'Phú Quốc', 'Hà Giang', 'Vũng Tàu']

/** Trang chủ khách – ô tìm kiếm thật làm ở Phase 4 (hiện chỉ là bố cục). */
export function HomePage() {
  return (
    <div className="flex flex-col gap-10">
      <section className="rounded-3xl bg-gradient-to-br from-brand-700 to-brand-800 px-6 py-10 text-white md:px-10 md:py-14">
        <h1 className="max-w-xl text-3xl font-semibold tracking-tight md:text-4xl">Tìm homestay ưng ý cho chuyến đi tới</h1>
        <p className="mt-3 max-w-xl text-brand-50">Giá rõ từng đêm, cọc trước 30%, huỷ miễn phí trước 7 ngày.</p>
        <div className="mt-6 grid gap-2 rounded-2xl bg-white p-2 text-slate-700 sm:grid-cols-[1.4fr_1fr_0.8fr_auto]" aria-label="Tìm kiếm (Phase 4)">
          <Fake icon={MapPin} text="Bạn muốn đi đâu?" />
          <Fake icon={CalendarDays} text="Nhận – trả phòng" />
          <Fake icon={Users} text="2 khách" />
          <Link to="/stays" className="flex items-center justify-center gap-2 rounded-xl bg-brand-700 px-5 py-3 font-medium text-white hover:bg-brand-800">
            <Search className="size-4" aria-hidden />
            Tìm
          </Link>
        </div>
      </section>

      <section>
        <h2 className="mb-4 text-xl font-semibold">Điểm đến phổ biến</h2>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          {provinces.map((p) => (
            <li key={p} className="rounded-2xl border border-slate-200 bg-white p-4 text-center font-medium dark:border-slate-800 dark:bg-slate-900">
              {p}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}

function Fake({ icon: Icon, text }: { icon: typeof MapPin; text: string }) {
  return (
    <div className="flex items-center gap-2 rounded-xl px-3 py-3 text-sm text-slate-600">
      <Icon className="size-4 text-brand-700" aria-hidden />
      {text}
    </div>
  )
}
