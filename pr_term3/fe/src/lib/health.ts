import { useQuery } from '@tanstack/react-query'

export type Health = { status: 'ok' | 'degraded'; database: 'up' | 'down' }

/** /health nằm ngoài /api – trả 503 kèm body khi DB down nên vẫn đọc body. */
export async function fetchHealth(signal?: AbortSignal): Promise<Health> {
  const res = await fetch('/health', { signal })
  if (res.status !== 200 && res.status !== 503) throw new Error(`HTTP ${res.status}`)
  return (await res.json()) as Health
}

export function useHealth() {
  return useQuery({ queryKey: ['health'], queryFn: ({ signal }) => fetchHealth(signal), refetchInterval: 60_000 })
}
