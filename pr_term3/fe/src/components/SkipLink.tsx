/** Link đầu trang cho người dùng bàn phím: ẩn tới khi được focus (Tab đầu tiên), nhảy qua thanh điều hướng vào nội dung chính. */
export function SkipLink() {
  return (
    <a
      href="#main"
      className="sr-only z-50 rounded-lg bg-brand-700 px-4 py-2 font-medium text-white focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
    >
      Bỏ qua tới nội dung chính
    </a>
  )
}
