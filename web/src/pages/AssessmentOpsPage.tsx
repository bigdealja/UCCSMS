import { useEffect, useState } from 'react'
import { api } from '../api'

type Sitting = { id: number; examId: number; dueDate: string; stage: string; status: string; exceptionNote?: string; lateReason?: string }
const MILESTONES = ['ScriptsCollected','Marked','GradeSheetReturned','Entered','SentToDean','Vetted','Published']
const NEXT = (stage: string) => { const i = MILESTONES.indexOf(stage); return i < 0 ? MILESTONES[0] : MILESTONES[Math.min(i + 1, MILESTONES.length - 1)] }

export default function AssessmentOpsPage() {
  const [rows, setRows] = useState<Sitting[]>([])
  const [attentionOnly, setAttentionOnly] = useState(false)
  const [form, setForm] = useState({ examId: 50, dueDate: '2026-12-20' })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    const url = attentionOnly ? '/api/assessmentops/needs-attention' : '/api/assessmentops'
    api.get<Sitting[]>(url).then(setRows).catch(e => setErr(e.message))
  }, [attentionOnly, reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const create = () => go(() => api.post('/api/assessmentops', form))
  const milestone = (id: number, m: string) => go(() => api.post(`/api/assessmentops/${id}/milestone`, { milestone: m }))
  const exception = (id: number) => { const n = prompt('Exception note (blank to clear)?') ?? ''; go(() => api.post(`/api/assessmentops/${id}/exception`, { note: n || null })) }
  const cls = (s: string) => s === 'Overdue' || s === 'Blocked' ? 'err' : s === 'AtRisk' ? 'warn' : s === 'Complete' ? 'ok' : ''

  return (
    <div>
      <h2>Assessment Operations</h2>
      <p className="muted-text">One record per sitting. Stage and status (Ready / At-risk / Overdue / Blocked / Complete) are
        worked out from the milestones and the due date — not typed in.</p>
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Register a sitting</h3>
        <div className="row">
          <label>Exam ID <input type="number" value={form.examId} onChange={e => setForm({ ...form, examId: +e.target.value })} style={{ width: 90 }} /></label>
          <label>Due date <input type="date" value={form.dueDate} onChange={e => setForm({ ...form, dueDate: e.target.value })} /></label>
          <button onClick={create} style={{ alignSelf: 'flex-end' }}>Create</button>
        </div>
      </div>

      <div className="card">
        <div className="row between">
          <h3>Register</h3>
          <label style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <input type="checkbox" checked={attentionOnly} onChange={e => setAttentionOnly(e.target.checked)} /> Needs attention only</label>
        </div>
        <table className="grid">
          <thead><tr><th>Exam</th><th>Due</th><th>Stage</th><th>Status</th><th>Note</th><th>Actions</th></tr></thead>
          <tbody>
            {rows.map(r => (<tr key={r.id} className={r.status === 'Overdue' || r.status === 'Blocked' ? 'error' : r.status === 'AtRisk' ? 'warning' : ''}>
              <td>#{r.examId}</td><td>{r.dueDate}</td><td>{r.stage}</td>
              <td><span className={cls(r.status)}>{r.status}</span></td>
              <td>{r.exceptionNote ?? r.lateReason ?? '—'}</td>
              <td className="actions">
                {r.stage !== 'Published' && <button className="link" onClick={() => milestone(r.id, NEXT(r.stage))}>✓ {NEXT(r.stage)}</button>}
                <button className="link" onClick={() => exception(r.id)}>Exception</button>
              </td>
            </tr>))}
            {rows.length === 0 && <tr><td colSpan={6}>No sittings{attentionOnly ? ' needing attention' : ''}.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
