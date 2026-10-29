/** Chỉ nhận đường dẫn nội bộ ("/x"), chặn "//evil.com" và URL tuyệt đối → không bị lợi dụng chuyển hướng ra ngoài. */
export function safeNext(next: string | null): string {
  return next && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\') ? next : '/'
}

export function loginHref(next: string) {
  return next === '/' ? '/login' : `/login?next=${encodeURIComponent(next)}`
}
