import { useEffect, useState } from 'react'
import { api } from '../api'

type Audit = { id: number; timestamp: string; actor: string; action: string; entityType: string; entityId?: string; before?: string; after?: string; reason?: string }
type Paged = { items: Audit[]; total: number; page: number; pageSize: number }

export default function AuditPage() {
  const [data, setData] = useState<Paged | null>(null)
  const [entityType, setEntityType] = useState('')
  const [page, setPage] = useState(1)
  const [err, setErr] = useState<string | null>(null)

  useEffect(() => {
    const q = new URLSearchParams({ page: String(page), pageSize: '50' })
    if (entityType) q.set('entityType', entityType)
    api.get<Paged>(`/api/audit?${q}`).then(setData).catch(e => setErr(e.message))
  }, [entityType, page])

  const badge = (a: string) => a === 'Delete' ? 'err' : a === 'Create' ? 'ok' : a === 'Download' || a === 'View' ? 'warn' : ''

  return (
    <div>
      <h2>Audit Trail</h2>
      <p className="muted-text">Append-only record of every change (who, what, when, before → after) and of
        downloads/views of restricted records. Read-only — history cannot be edited.</p>
      {err && <p className="error">Could not load audit log: {err} (needs the <code>audit.read</code> permission).</p>}

      <div className="card">
        <div className="row between">
          <label>Entity type <input value={entityType} onChange={e => { setPage(1); setEntityType(e.target.value) }} placeholder="e.g. Student, Enrollment" /></label>
          {data && <span className="muted-text">{data.total} events</span>}
        </div>
        <table className="grid">
          <thead><tr><th>When</th><th>Actor</th><th>Action</th><th>Entity</th><th>Before → After</th></tr></thead>
          <tbody>
            {data?.items.map(a => (
              <tr key={a.id}>
                <td>{new Date(a.timestamp).toLocaleString()}</td>
                <td>{a.actor}</td>
                <td><span className={badge(a.action)}>{a.action}</span></td>
                <td>{a.entityType}{a.entityId ? ` #${a.entityId}` : ''}</td>
                <td style={{ fontSize: 12, maxWidth: 480, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                    title={`${a.before ?? ''}  →  ${a.after ?? ''}`}>
                  {a.before ? <span className="muted-text">{a.before}</span> : ''}{a.before && a.after ? ' → ' : ''}{a.after ?? ''}{a.reason ? ` · ${a.reason}` : ''}
                </td>
              </tr>
            ))}
            {data && data.items.length === 0 && <tr><td colSpan={5}>No audit events.</td></tr>}
          </tbody>
        </table>
        {data && (
          <div className="pager">
            <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Prev</button>
            <span>Page {data.page} · {data.total} total</span>
            <button disabled={page * data.pageSize >= data.total} onClick={() => setPage(p => p + 1)}>Next</button>
          </div>
        )}
      </div>
    </div>
  )
}
