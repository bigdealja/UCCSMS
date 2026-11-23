import { useEffect, useState } from 'react'
import { api } from '../api'

type CaseType = { id: number; code: string; name: string; turnaroundWorkingDays: number; requiresSecondPersonApproval: boolean }
type CaseRow = { id: number; caseNumber: string; type: string; title: string; status: string; priority: string; assignedTo?: string; raisedBy?: string; dueUtc: string; overdue: boolean; dueSoon: boolean }
type Dash = { open: number; overdue: number; dueSoon: number; pendingApproval: number; byType: { type: string; count: number }[] }

export default function CasesPage() {
  const [types, setTypes] = useState<CaseType[]>([])
  const [cases, setCases] = useState<CaseRow[]>([])
  const [dash, setDash] = useState<Dash | null>(null)
  const [statusFilter, setStatusFilter] = useState('')
  const [form, setForm] = useState({ caseTypeId: 0, title: '', priority: 'Normal' })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<CaseType[]>('/api/cases/types').then(setTypes).catch(e => setErr(e.message)) }, [])
  useEffect(() => { api.get<Dash>('/api/cases/dashboard').then(setDash).catch(() => {}) }, [reload])
  useEffect(() => {
    const q = statusFilter ? `?status=${statusFilter}` : ''
    api.get<CaseRow[]>(`/api/cases${q}`).then(setCases).catch(e => setErr(e.message))
  }, [statusFilter, reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const create = () => go(() => api.post('/api/cases', form).then(() => setForm({ ...form, title: '' })))
  const approve = (id: number) => go(() => api.post(`/api/cases/${id}/approve`, {}))
  const resolve = (id: number) => { const r = prompt('Resolution?'); if (r) go(() => api.post(`/api/cases/${id}/resolve`, { resolution: r })) }
  const cancel = (id: number) => { const r = prompt('Cancel reason?'); if (r) go(() => api.post(`/api/cases/${id}/cancel`, { reason: r })) }

  const tile = (label: string, val: number, cls = '') => <div className="card" style={{ flex: 1, textAlign: 'center' }}>
    <div style={{ fontSize: 28, fontWeight: 700 }} className={cls}>{val}</div><div className="muted-text">{label}</div></div>

  return (
    <div>
      <h2>Cases</h2>
      {err && <p className="error">{err}</p>}
      {dash && <div className="row">{tile('Open', dash.open)}{tile('Overdue', dash.overdue, 'err')}{tile('Due soon', dash.dueSoon, 'warn')}{tile('Pending approval', dash.pendingApproval)}</div>}

      <div className="card">
        <h3>Raise a case</h3>
        <div className="row">
          <label>Type <select value={form.caseTypeId} onChange={e => setForm({ ...form, caseTypeId: +e.target.value })}>
            <option value={0}>— select —</option>{types.map(t => <option key={t.id} value={t.id}>{t.name}{t.requiresSecondPersonApproval ? ' ⟳' : ''}</option>)}</select></label>
          <label>Title <input value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
          <label>Priority <select value={form.priority} onChange={e => setForm({ ...form, priority: e.target.value })}>
            {['Low','Normal','High','Urgent'].map(p => <option key={p}>{p}</option>)}</select></label>
          <button disabled={!form.caseTypeId || !form.title} onClick={create} style={{ alignSelf: 'flex-end' }}>Create</button>
        </div>
        <p className="muted-text">⟳ = requires second-person approval.</p>
      </div>

      <div className="card">
        <div className="row between"><h3>Case list</h3>
          <label>Status <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}>
            <option value="">All</option>{['Open','InProgress','PendingApproval','Approved','Resolved','Cancelled'].map(s => <option key={s}>{s}</option>)}</select></label>
        </div>
        <table className="grid">
          <thead><tr><th>Case #</th><th>Type</th><th>Title</th><th>Status</th><th>Priority</th><th>Due</th><th>Actions</th></tr></thead>
          <tbody>
            {cases.map(c => (<tr key={c.id} className={c.overdue ? 'error' : c.dueSoon ? 'warning' : ''}>
              <td>{c.caseNumber}</td><td>{c.type}</td><td>{c.title}</td><td>{c.status}</td><td>{c.priority}</td>
              <td>{new Date(c.dueUtc).toLocaleDateString()}{c.overdue ? ' ⚠' : ''}</td>
              <td className="actions">
                <button className="link" onClick={() => approve(c.id)}>Approve</button>
                <button className="link" onClick={() => resolve(c.id)}>Resolve</button>
                <button className="link danger" onClick={() => cancel(c.id)}>Cancel</button>
              </td>
            </tr>))}
            {cases.length === 0 && <tr><td colSpan={7}>No cases.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
