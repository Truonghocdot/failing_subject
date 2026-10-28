# 04 – Frontend React

SPA trong `fe/`: **React 19 + Vite 8 + TypeScript 7**, giao diện tiếng Việt, responsive (sidebar trên máy tính, thanh tab dưới trên điện thoại).

## 1. Package

| Package | Dùng cho |
|---|---|
| `react`, `react-dom` | UI |
| `react-router` | Định tuyến, route được bảo vệ |
| `@tanstack/react-query` | Gọi API + cache + invalidate sau khi ghi |
| `react-hook-form`, `zod`, `@hookform/resolvers` | Form + validate (quy tắc khớp [02](02-nghiep-vu.md)) |
| `tailwindcss`, `@tailwindcss/vite` | CSS tiện ích |
| `recharts` | Biểu đồ tròn / cột |
| `lucide-react` | Icon (cũng là bộ icon của danh mục) |
| `dayjs` | Ngày tháng, locale `vi` |
| dev: `vitest`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `jsdom` | Test |
| dev: `oxlint` | Lint (mặc định của template Vite) |

Không dùng axios: `fetch` + wrapper nhỏ trong `src/lib/api.ts` là đủ.

## 2. Trang & route

| Route | Trang | Nội dung chính |
|---|---|---|
| `/login`, `/register` | Đăng nhập / Đăng ký | Form, lỗi tiếng Việt, chuyển về trang định vào trước đó |
| `/` | Tổng quan | Thẻ: thu / chi / chênh lệch tháng này, tổng số dư; biểu đồ tròn chi theo danh mục; cột thu-chi 6 tháng; 5 giao dịch gần nhất; ngân sách sắp vượt |
| `/transactions` | Giao dịch | Thanh lọc (tháng / khoảng ngày, ví, danh mục, loại, tìm ghi chú), danh sách nhóm theo ngày, tổng thu/chi của bộ lọc, phân trang, xuất CSV |
| `/wallets` | Ví | Thẻ từng ví + số dư, tổng tài sản, thêm / sửa / lưu trữ / xoá |
| `/categories` | Danh mục | 2 tab Chi / Thu, chọn icon + màu |
| `/budgets` | Ngân sách | Chọn tháng (`?month=2026-11`), thanh tiến độ từng danh mục, sao chép tháng trước |
| `/reports` | Báo cáo | Chọn khoảng (`?range=last-month\|3-months\|this-year\|custom&from&to`, `?kind=income`); tròn theo danh mục (chi/thu), cột xu hướng ngày/tháng, bảng chi tiết |
| `/settings` | Cài đặt | Tên hiển thị, tiền tệ, đổi mật khẩu, đăng xuất |

Mọi route trừ `/login`, `/register` nằm trong `<RequireAuth>` + `<AppLayout>`.
Nút **"+ Thêm giao dịch"** luôn hiện (góc dưới phải trên điện thoại) → mở `TransactionFormDialog`.

## 3. Cấu trúc thư mục

```
fe/
├── index.html
├── vite.config.ts          # plugin react + tailwind, proxy /api → API_PROXY_TARGET, cấu hình vitest
├── .env / .env.example
└── src/
    ├── main.tsx            # createRoot + <App/>
    ├── index.css           # @import "tailwindcss"; token màu
    ├── app/
    │   ├── App.tsx         # QueryClientProvider + AuthProvider + RouterProvider
    │   ├── router.tsx      # khai báo route
    │   ├── env.ts          # đọc import.meta.env
    │   └── layout/         # AppLayout, Sidebar, BottomNav
    ├── lib/
    │   ├── api.ts          # fetch wrapper: base /api, Bearer, refresh single-flight, ApiError(ProblemDetails)
    │   ├── format.ts       # formatMoney, formatDate, monthRange…
    │   └── queryClient.ts
    ├── components/         # Button, Input, Select, Dialog, Card, EmptyState, Spinner, Money, ConfirmDialog, CategoryIcon
    └── features/
        ├── auth/           # AuthProvider (access token trong RAM), LoginPage, RegisterPage, RequireAuth
        ├── dashboard/
        ├── transactions/   # api.ts (hooks), TransactionsPage, TransactionFormDialog, TransactionFilters
        ├── wallets/
        ├── categories/
        ├── budgets/
        ├── reports/
        └── settings/
```

Mỗi feature có `api.ts` chứa **query key + hook** (`useWallets`, `useCreateTransaction`…), component chỉ gọi hook.

## 4. Gọi API & xác thực

```ts
// src/lib/api.ts (rút gọn)
let accessToken: string | null = null;
let refreshing: Promise<boolean> | null = null;     // single-flight

export async function api<T>(path: string, init?: RequestInit & { auth?: boolean }): Promise<T> {
  const res = await send(path, init);
  if (res.status === 401 && init?.auth !== false && (await refreshOnce())) return parse<T>(await send(path, init));
  return parse<T>(res);                              // !ok → throw ApiError { status, title, errors }
}
```

- Access token chỉ nằm trong RAM → F5 trang thì `AuthProvider` gọi `POST /api/auth/refresh` (cookie) để lấy lại; trong lúc chờ hiện màn hình chờ.
- Refresh thất bại → xoá user, `queryClient.clear()`, chuyển về `/login?next=<trang hiện tại>`.
- `ApiError.errors` (400) được map vào lỗi từng ô của react-hook-form (`setError`).

## 5. State & cache (TanStack Query)

| Query key | Invalidate khi |
|---|---|
| `['wallets']` | tạo/sửa/xoá ví **và** mọi thay đổi giao dịch (số dư đổi) |
| `['categories', {includeArchived}]` | tạo/sửa/xoá danh mục (lấy cả 2 loại 1 lần, lọc Chi/Thu ở client) |
| `['transactions', filters]` | tạo/sửa/xoá giao dịch |
| `['budgets', month]` | thay đổi ngân sách hoặc giao dịch |
| `['reports', 'summary' \| 'by-category' \| 'trend', range, …]` | thay đổi giao dịch (và danh mục – tên / màu) |

Quy ước: sau mutation giao dịch gọi `invalidateQueries` cho `transactions`, `wallets`, `budgets`, `reports`
(hàm dùng chung `invalidateLedger(queryClient)` trong `features/wallets/api.ts`).

Tách code: mỗi trang sau đăng nhập là 1 chunk (`app/lazyPage.tsx` – `React.lazy`, `AppLayout` bọc `<Suspense>`); đăng nhập / đăng ký nạp sẵn.
`vite.config.ts` tách thêm chunk `react` (react, react-dom, react-router) và `vendor` (TanStack Query, zod, react-hook-form, dayjs) – ít đổi nên trình duyệt giữ cache;
Recharts chỉ tải khi mở Tổng quan / Báo cáo. Tải chunk lỗi (vừa cập nhật bản mới) → `RouteError` "Tải lại trang".

Bộ lọc trang Giao dịch nằm trên URL (`?month=2026-10&kind=expense&walletId=2&q=phở` hoặc `?from=…&to=…`).
Cập nhật URL luôn dựa trên **URL mới nhất** (ref), không dùng `setSearchParams(prev => …)`: `prev` là URL lúc render tạo ra hàm,
gọi trễ (ô tìm kiếm chờ 300 ms) sẽ ghi đè bộ lọc vừa đổi.
`staleTime` mặc định 30 giây; `retry` 1 lần, không retry lỗi 4xx.

## 6. Hiển thị

- Tiền: `Intl.NumberFormat('vi-VN', { style: 'currency', currency: user.currency })` → `45.000 ₫`; chi màu đỏ có dấu `−`, thu màu xanh `+`.
- Ô nhập tiền: gõ số, tự chèn dấu chấm phân cách nghìn; gửi lên backend là `number`.
- Ngày: `dddd, DD/MM/YYYY` khi nhóm danh sách; `DD/MM` trong biểu đồ.
- Màu danh mục / ví dùng làm màu biểu đồ. Có chế độ tối theo `prefers-color-scheme`.
- Tương phản chữ ≥ 4,5:1 (WCAG AA, kiểm bằng axe-core): nút chính / link dùng `brand-700` (`brand-600` chỉ 3,8:1 với chữ trắng),
  chữ khoản thu `--color-income: #047857`; chữ phụ trên nền trắng tối thiểu `slate-500`.
- Hộp thoại: focus ô nhập đầu tiên (hoặc ô `autoFocus`), Esc / bấm nền để đóng, **trả focus về nút đã mở** khi đóng.

### Biểu đồ (`features/reports/charts.tsx`, Recharts)

- **Tròn** cơ cấu theo danh mục: tối đa 6 mảng (top 5 + "Khác"), 2px khe màu nền giữa các mảng; chú thích bên cạnh có số tiền + %
  (đọc được không cần màu / tooltip); trang Báo cáo có thêm bảng chi tiết.
- **Cột** thu – chi: 1 trục, 2 cột cạnh nhau, đầu cột bo 4px, lưới ngang mờ; tooltip theo cả dải kỳ (dễ trỏ); "Xem dạng bảng".
  Kỳ trống vẫn có (backend trả đủ kỳ).
- Màu thu / chi là biến CSS `--chart-income` / `--chart-expense` (đổi ở chế độ tối), đã chạy kiểm tra mù màu (deutan ΔE ≥ 8);
  chữ / trục dùng màu chữ, không dùng màu series. Không chạy hiệu ứng vẽ để đổi bộ lọc không nháy.
- Ngân sách: thanh tiến độ màu trạng thái (xanh / vàng / đỏ) luôn kèm icon + chữ ("85% · Sắp hết"), dừng ở 100% nhưng chữ ghi đúng %.
- Test: jsdom không có `ResizeObserver` → `src/test/setup.ts` có stub; biểu đồ lấy kích thước từ `initialDimension`.

## 7. Cấu hình `.env` (Vite)

| Biến | Mặc định | Ghi chú |
|---|---|---|
| `VITE_APP_NAME` | `Sổ Chi Tiêu` | Tiêu đề trang |
| `VITE_API_BASE_URL` | `/api` | Để `/api` khi dùng proxy hoặc khi backend phục vụ FE |
| `API_PROXY_TARGET` | `http://localhost:5090` | Chỉ `vite.config.ts` đọc (không có tiền tố `VITE_` → không vào bundle) |

Biến `VITE_*` **được nhúng vào bundle** → tuyệt đối không đặt bí mật ở đây.
