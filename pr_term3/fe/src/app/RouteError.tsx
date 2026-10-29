import { useRouteError } from 'react-router'
import { Button } from '../components/Button'

/**
 * Lỗi khi hiển thị trang – hay gặp nhất: vừa cập nhật bản mới, tab cũ xin chunk JS cũ đã bị xoá → tải lại trang là xong.
 */
export function RouteError() {
  const error = useRouteError()
  const chunkFailed = error instanceof TypeError && /dynamically imported module|Importing a module script failed/i.test(error.message)
  return (
    <div role="alert" className="flex min-h-[60dvh] flex-col items-center justify-center gap-3 px-4 text-center">
      <p className="text-lg font-semibold">{chunkFailed ? 'Ứng dụng vừa được cập nhật' : 'Đã có lỗi khi hiển thị trang'}</p>
      <p className="text-sm text-slate-600 dark:text-slate-300">
        {chunkFailed ? 'Tải lại trang để dùng phiên bản mới.' : 'Thử tải lại trang; nếu vẫn lỗi hãy báo cho người quản trị.'}
      </p>
      <Button onClick={() => window.location.reload()}>Tải lại trang</Button>
    </div>
  )
}
