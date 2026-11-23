import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type Change = { id: number; enrollmentId: number; originalLetterGrade?: string; proposedLetterGrade?: string; reasonCode: string; status: string; proposedBy?: string; decidedBy?: string }
type Issue = { kind: string; description: string; enrollmentId: number; studentId: number }
type Report = { termId: number; readyToPublish: boolean; issues: Issue[] }
const REASONS = ['ClericalError','RemarkOutcome','AppealOutcome','LateCoursework','IncompleteCleared','IntegrityOutcome','Other']

export default function GradeGovernancePage() {
  const [changes, setChanges] = useState<Change[]>([])
  const [terms, setTerms] = useState<Lookup[]>([])
  const [termId, setTermId] = useState(0)
  const [report, setReport] = useState<Report | null>(null)
  const [form, setForm] = useState({ enrollmentId: '', proposedLetterGrade: '', reasonCode: 'RemarkOutcome', reason: '' })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Change[]>('/api/gradegovernance/changes').then(setChanges).catch(e => setErr(e.message)) }, [reload])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const propose = () => go(() => api.post('/api/gradegovernance/changes', { ...form, enrollmentId: +form.enrollmentId })
    .then(() => setForm({ ...form, enrollmentId: '', proposedLetterGrade: '', reason: '' })))
  const approve = (id: number) => go(() => api.post(`/api/gradegovernance/changes/${id}/approve`, {}))
  const reject = (id: number) => { const r = prompt('Reject reason?'); go(() => api.post(`/api/gradegovernance/changes/${id}/reject`, { reason: r })) }
  const check = () => go(() => api.get<Report>(`/api/gradegovernance/pre-publication/${termId}`).then(setReport))

  return (
    <div>
      <h2>Grade Governance</h2>
      <p className="muted-text">Grade changes carry a reason code, preserve the original value, and need approval by a
        second person (not the proposer). A pre-publication check reconciles a term before results are released.</p>
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Propose a grade change</h3>
        <div className="row">
          <label>Enrollment ID <input value={form.enrollmentId} onChange={e => setForm({ ...form, enrollmentId: e.target.value })} style={{ width: 120 }} /></label>
          <label>New letter grade <input value={form.proposedLetterGrade} onChange={e => setForm({ ...form, proposedLetterGrade: e.target.value })} style={{ width: 90 }} /></label>
          <label>Reason code <select value={form.reasonCode} onChange={e => setForm({ ...form, reasonCode: e.target.value })}>{REASONS.map(r => <option key={r}>{r}</option>)}</select></label>
          <label>Reason <input value={form.reason} onChange={e => setForm({ ...form, reason: e.target.value })} /></label>
          <button disabled={!form.enrollmentId} onClick={propose} style={{ alignSelf: 'flex-end' }}>Propose</button>
        </div>
      </div>

      <div className="card">
        <h3>Change register</h3>
        <table className="grid">
          <thead><tr><th>#</th><th>Enrol</th><th>From</th><th>To</th><th>Reason</th><th>Status</th><th>Proposed by</th><th></th></tr></thead>
          <tbody>
            {changes.map(c => (<tr key={c.id}>
              <td>{c.id}</td><td>{c.enrollmentId}</td><td>{c.originalLetterGrade ?? '—'}</td><td>{c.proposedLetterGrade ?? '—'}</td>
              <td>{c.reasonCode}</td><td>{c.status}</td><td>{c.proposedBy}</td>
              <td className="actions">{c.status === 'Proposed' && <>
                <button className="link" onClick={() => approve(c.id)}>Approve</button>
                <button className="link danger" onClick={() => reject(c.id)}>Reject</button></>}</td>
            </tr>))}
            {changes.length === 0 && <tr><td colSpan={8}>No grade-change requests.</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3>Pre-publication check</h3>
        <div className="row">
          <label>Term <select value={termId} onChange={e => setTermId(+e.target.value)}>
            <option value={0}>— select —</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>
          <button disabled={!termId} onClick={check} style={{ alignSelf: 'flex-end' }}>Run check</button>
        </div>
        {report && (report.readyToPublish
          ? <p className="ok" style={{ marginTop: 12 }}>Ready to publish ✓</p>
          : <table className="grid" style={{ marginTop: 12 }}>
              <thead><tr><th>Issue</th><th>Detail</th><th>Enrol</th><th>Student</th></tr></thead>
              <tbody>{report.issues.map((i, k) => <tr key={k} className="warning"><td>{i.kind}</td><td>{i.description}</td><td>{i.enrollmentId}</td><td>{i.studentId}</td></tr>)}</tbody>
            </table>)}
      </div>
    </div>
  )
}
