import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type Paper = { id: number; title: string; courseId: number; termId: number; status: string; submissionDeadline: string; authorLecturer?: string; externalReviewRequired: boolean; currentVersion: number; overdue: boolean; atRisk: boolean }
const NEXT: Record<string, string> = { Submitted: 'UnderVetting', UnderVetting: 'Approved', ExternalReview: 'Approved', Approved: 'Released', Released: 'Printed' }

export default function QuestionPapersPage() {
  const [papers, setPapers] = useState<Paper[]>([])
  const [courses, setCourses] = useState<Lookup[]>([])
  const [terms, setTerms] = useState<Lookup[]>([])
  const [form, setForm] = useState({ courseId: 0, termId: 0, title: '', submissionDeadline: '2026-11-30', authorLecturer: '', externalReviewRequired: false })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Paper[]>('/api/questionpapers').then(setPapers).catch(e => setErr(e.message)) }, [reload])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/courses').then(setCourses).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const register = () => go(() => api.post('/api/questionpapers', form).then(() => setForm({ ...form, title: '' })))
  const submitVersion = (id: number) => go(() => api.post(`/api/questionpapers/${id}/versions`, { fileName: `paper-v-next.pdf` }))
  const advance = (id: number, to: string) => go(() => api.post(`/api/questionpapers/${id}/advance`, { toStatus: to }))
  const extReview = (id: number) => go(() => api.post(`/api/questionpapers/${id}/advance`, { toStatus: 'ExternalReview' }))
  const withdraw = (id: number) => { const r = prompt('Withdrawal reason?'); if (r) go(() => api.post(`/api/questionpapers/${id}/withdraw`, { reason: r })) }

  return (
    <div>
      <h2>Question Papers</h2>
      <p className="muted-text">Secure lifecycle: expected → submitted → vetting → (external review) → approved → released → printed → dispatched → returned → destroyed. Emergency withdrawal at any stage.</p>
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Register an expected paper</h3>
        <div className="row">
          <label>Course <select value={form.courseId} onChange={e => setForm({ ...form, courseId: +e.target.value })}>
            <option value={0}>—</option>{courses.map(c => <option key={c.id} value={c.id}>{c.code}</option>)}</select></label>
          <label>Term <select value={form.termId} onChange={e => setForm({ ...form, termId: +e.target.value })}>
            <option value={0}>—</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>
          <label>Title <input value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} /></label>
          <label>Deadline <input type="date" value={form.submissionDeadline} onChange={e => setForm({ ...form, submissionDeadline: e.target.value })} /></label>
          <label>Author <input value={form.authorLecturer} onChange={e => setForm({ ...form, authorLecturer: e.target.value })} /></label>
          <label style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}><input type="checkbox" checked={form.externalReviewRequired} onChange={e => setForm({ ...form, externalReviewRequired: e.target.checked })} /> Ext. review</label>
          <button disabled={!form.courseId || !form.termId || !form.title} onClick={register} style={{ alignSelf: 'flex-end' }}>Register</button>
        </div>
      </div>

      <div className="card">
        <h3>Papers</h3>
        <table className="grid">
          <thead><tr><th>Title</th><th>Status</th><th>Deadline</th><th>Ver</th><th>Ext?</th><th>Actions</th></tr></thead>
          <tbody>
            {papers.map(p => (<tr key={p.id} className={p.overdue ? 'error' : p.atRisk ? 'warning' : ''}>
              <td>{p.title}</td><td>{p.status}{p.overdue ? ' ⚠ overdue' : p.atRisk ? ' · due soon' : ''}</td>
              <td>{p.submissionDeadline}</td><td>v{p.currentVersion}</td><td>{p.externalReviewRequired ? 'yes' : '—'}</td>
              <td className="actions">
                {(p.status === 'Expected' || p.status === 'Submitted') && <button className="link" onClick={() => submitVersion(p.id)}>+ Version</button>}
                {p.externalReviewRequired && p.status === 'UnderVetting' && <button className="link" onClick={() => extReview(p.id)}>→ ExternalReview</button>}
                {NEXT[p.status] && <button className="link" onClick={() => advance(p.id, NEXT[p.status])}>→ {NEXT[p.status]}</button>}
                {p.status !== 'Withdrawn' && p.status !== 'Destroyed' && <button className="link danger" onClick={() => withdraw(p.id)}>Withdraw</button>}
              </td>
            </tr>))}
            {papers.length === 0 && <tr><td colSpan={6}>No papers yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
