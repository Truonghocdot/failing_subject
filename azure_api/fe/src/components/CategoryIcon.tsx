import {
  Baby, Banknote, Beer, Bike, BookOpen, Briefcase, Bus, Car, ChartLine, CircleEllipsis, CirclePlus, Clapperboard, Coffee, Coins,
  CreditCard, Droplets, Dumbbell, Fuel, Gamepad2, Gift, GraduationCap, Hamburger, HandHeart, HeartPulse, House, Landmark, Laptop,
  Music, Palette, PawPrint, Pill, PiggyBank, Plane, Receipt, Scissors, Shirt, ShoppingBag, Smartphone, Sofa, Sparkles,
  Stethoscope, Store, Ticket, Train, TrendingUp, Tv, Utensils, Wallet, Wifi, Wrench, Zap, type LucideIcon,
} from 'lucide-react'

/** Bộ icon chọn được cho danh mục – tên khớp cột categories.icon (tên icon lucide). */
export const categoryIcons: Record<string, LucideIcon> = {
  utensils: Utensils,
  hamburger: Hamburger,
  coffee: Coffee,
  beer: Beer,
  'shopping-bag': ShoppingBag,
  shirt: Shirt,
  house: House,
  sofa: Sofa,
  receipt: Receipt,
  zap: Zap,
  droplets: Droplets,
  wifi: Wifi,
  smartphone: Smartphone,
  bus: Bus,
  car: Car,
  fuel: Fuel,
  train: Train,
  bike: Bike,
  plane: Plane,
  clapperboard: Clapperboard,
  'gamepad-2': Gamepad2,
  music: Music,
  ticket: Ticket,
  tv: Tv,
  'heart-pulse': HeartPulse,
  stethoscope: Stethoscope,
  pill: Pill,
  dumbbell: Dumbbell,
  'graduation-cap': GraduationCap,
  'book-open': BookOpen,
  laptop: Laptop,
  baby: Baby,
  'paw-print': PawPrint,
  scissors: Scissors,
  sparkles: Sparkles,
  palette: Palette,
  wrench: Wrench,
  gift: Gift,
  'hand-heart': HandHeart,
  banknote: Banknote,
  coins: Coins,
  briefcase: Briefcase,
  store: Store,
  'trending-up': TrendingUp,
  'chart-line': ChartLine,
  landmark: Landmark,
  'piggy-bank': PiggyBank,
  'credit-card': CreditCard,
  wallet: Wallet,
  'circle-plus': CirclePlus,
  'circle-ellipsis': CircleEllipsis,
}

/** Icon trong ô tròn màu của danh mục; icon lạ → dấu "…". */
export function CategoryIcon({ icon, color, size = 'md' }: { icon: string; color: string; size?: 'sm' | 'md' }) {
  const Icon = categoryIcons[icon] ?? CircleEllipsis
  return (
    <span
      className={`inline-flex shrink-0 items-center justify-center rounded-full ${size === 'sm' ? 'size-8' : 'size-10'}`}
      style={{ backgroundColor: `${color}22`, color }}
      aria-hidden
    >
      <Icon className={size === 'sm' ? 'size-4' : 'size-5'} />
    </span>
  )
}

export const colorChoices = [
  '#ef4444', '#f97316', '#eab308', '#84cc16', '#16a34a', '#14b8a6', '#0ea5e9', '#2563eb',
  '#6366f1', '#8b5cf6', '#d946ef', '#ec4899', '#f43f5e', '#92400e', '#64748b', '#0f172a',
]

export function ColorPicker({ value, onChange, label = 'Màu' }: { value: string; onChange: (color: string) => void; label?: string }) {
  return (
    <fieldset>
      <legend className="mb-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">{label}</legend>
      <div className="flex flex-wrap gap-2">
        {colorChoices.map((c) => (
          <button
            key={c}
            type="button"
            aria-label={`Màu ${c}`}
            aria-pressed={value === c}
            onClick={() => onChange(c)}
            className={`size-7 rounded-full ring-offset-2 transition-transform hover:scale-110 dark:ring-offset-slate-900 ${value === c ? 'ring-2 ring-slate-900 dark:ring-white' : ''}`}
            style={{ backgroundColor: c }}
          />
        ))}
      </div>
    </fieldset>
  )
}

export function IconPicker({ value, color, onChange }: { value: string; color: string; onChange: (icon: string) => void }) {
  return (
    <fieldset>
      <legend className="mb-1.5 text-sm font-medium text-slate-700 dark:text-slate-200">Biểu tượng</legend>
      <div className="grid grid-cols-8 gap-1.5">
        {Object.entries(categoryIcons).map(([name, Icon]) => (
          <button
            key={name}
            type="button"
            aria-label={name}
            aria-pressed={value === name}
            onClick={() => onChange(name)}
            className={`flex aspect-square items-center justify-center rounded-lg border transition-colors ${
              value === name ? 'border-transparent' : 'border-slate-200 text-slate-500 hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-800'
            }`}
            style={value === name ? { backgroundColor: `${color}22`, color, outline: `2px solid ${color}` } : undefined}
          >
            <Icon className="size-4" />
          </button>
        ))}
      </div>
    </fieldset>
  )
}
