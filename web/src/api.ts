// Thin fetch wrapper. In dev, Vite proxies these to the API; in prod, nginx does.
async function http<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init)
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}: ${await res.text().catch(() => '')}`)
  return res.status === 204 ? (undefined as T) : res.json()
}
export const api = {
  get:  <T>(u: string) => http<T>(u),
  post: <T>(u: string, body: unknown) => http<T>(u, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  put:  <T>(u: string, body: unknown) => http<T>(u, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }),
  del:  (u: string) => http<void>(u, { method: 'DELETE' }),
  form: <T>(u: string, fd: FormData) => http<T>(u, { method: 'POST', body: fd }),
}
export type Lookup = { id: number; code: string; name: string }
