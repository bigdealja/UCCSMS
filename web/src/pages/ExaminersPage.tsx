import { useEffect, useState } from 'react'
import { api } from '../api'

type Examiner = { id: number; firstName: string; lastName: string; email?: string; institution?: string; status: string; conflictDeclarationRecorded: boolean; hasConflict: boolean }
type Assignment = { id: number; examinerId: number; programmeId?: number; termId?: number; stage: string }
const STAGES = ['Nominated','ConflictCheck','Appointed','ContractIssued','ContractSigned','PackagePreparation','PackageDispatched','UnderReview','ReportReceived','ActionPlan','Completed','Cancelled']

export default function ExaminersPage() {
  const [examiners, setExaminers] = useState<Examiner[]>([])
  const [selected, setSelected] = useState<number | null>(null)
  const [assignments, setAssignments] = useState<Assignment[]>([])
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', institution: '' })
  const [err, setErr] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Examiner[]>('/api/examiners').then(setExaminers).catch(e => setErr(e.message)) }, [reload])
  useEffect(() => { if (selected) api.get<Assignment[]>(`/api/examiners/${selected}/assignments`).then(setAssignments).catch(() => {}) }, [selected, reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); setMsg(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const register = () => go(() => api.post('/api/examiners', form).then(() => setForm({ firstName: '', lastName: '', email: '', institution: '' })))
  const conflict = (id: number, has: boolean) => { const d = has ? prompt('Conflict details?') : null; go(() => api.post(`/api/examiners/${id}/conflict`, { hasConflict: has, details: d })) }
  const addAssignment = (id: number) => go(() => api.post(`/api/examiners/${id}/assignments`, {}))
  const advance = (aid: number, stage: string) => go(() => api.post(`/api/examiners/assignments/${aid}/advance`, { toStage: stage }))
  const portal = (id: number) => go(() => api.post<any>(`/api/examiners/${id}/portal-token`, { validDays: 30 }).then(r => setMsg(`Portal link (valid to ${new Date(r.expiresUtc).toLocaleDateString()}): ${r.portalUrl}`)))
  const nextStage = (cur: string) => STAGES[Math.min(STAGES.indexOf(cur) + 1, STAGES.length - 2)]

  return (
    <div>
      <h2>External Examiners</h2>
      {err && <p className="error">{err}</p>}
      {msg && <p className="ok">{msg}</p>}

      <div className="card">
        <h3>Register an examiner</h3>
        <div className="row">
          <label>First <input value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} /></label>
          <label>Last <input value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} /></label>
          <label>Email <input value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /></label>
          <label>Institution <input value={form.institution} onChange={e => setForm({ ...form, institution: e.target.value })} /></label>
          <button disabled={!form.firstName || !form.lastName} onClick={register} style={{ alignSelf: 'flex-end' }}>Register</button>
        </div>
      </div>

      <div className="card">
        <h3>Examiner register</h3>
        <table className="grid">
          <thead><tr><th>Name</th><th>Institution</th><th>Status</th><th>Conflict</th><th>Actions</th></tr></thead>
          <tbody>
            {examiners.map(e => (<tr key={e.id} className={e.hasConflict ? 'warning' : ''}>
              <td>{e.firstName} {e.lastName}</td><td>{e.institution ?? '—'}</td><td>{e.status}</td>
              <td>{!e.conflictDeclarationRecorded ? 'not declared' : e.hasConflict ? 'CONFLICT' : 'none'}</td>
              <td className="actions">
                {!e.conflictDeclarationRecorded && <>
                  <button className="link" onClick={() => conflict(e.id, false)}>Declare: none</button>
                  <button className="link danger" onClick={() => conflict(e.id, true)}>Declare: conflict</button></>}
                <button className="link" onClick={() => { setSelected(e.id); addAssignment(e.id) }}>+ Assignment</button>
                <button className="link" onClick={() => portal(e.id)}>Portal link</button>
              </td>
            </tr>))}
            {examiners.length === 0 && <tr><td colSpan={5}>No examiners yet.</td></tr>}
          </tbody>
        </table>
      </div>

      {selected && (
        <div className="card">
          <h3>Assignments — examiner #{selected}</h3>
          <table className="grid">
            <thead><tr><th>#</th><th>Stage</th><th></th></tr></thead>
            <tbody>
              {assignments.map(a => (<tr key={a.id}>
                <td>{a.id}</td><td>{a.stage}</td>
                <td>{a.stage !== 'Completed' && a.stage !== 'Cancelled' &&
                  <button className="link" onClick={() => advance(a.id, nextStage(a.stage))}>→ {nextStage(a.stage)}</button>}</td>
              </tr>))}
              {assignments.length === 0 && <tr><td colSpan={3}>No assignments.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
