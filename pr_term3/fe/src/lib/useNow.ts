import { useEffect, useState } from 'react'

/** Thời điểm hiện tại, cập nhật mỗi `intervalMs` – cho đồng hồ đếm ngược (không gọi Date.now() trong lúc render). */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs)
    return () => clearInterval(timer)
  }, [intervalMs])
  return now
}
