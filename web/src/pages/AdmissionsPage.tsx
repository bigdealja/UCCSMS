import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type Lead = { id: number; firstName: string; lastName: string; email?: string; phone?: string; source?: string; status: string }
type App = { id: number; applicationNumber: string; firstName: string; lastName: string; status: string; decision: string; offerResponse: string; assignedOfficer?: string; studentId?: number }
type Health = { provider: string; external: boolean; reachable?: boolean | null }

const CATEGORIES = ['FullAcceptance','FullAcceptanceToDoCore','ConditionalClass1','ConditionalClass2Mature','MatureEntrant','Provisional','ConditionalGraduate']

export default function AdmissionsPage() {
  const [health, setHealth] = useState<Health | null>(null)
  const [leads, setLeads] = useState<Lead[]>([])
  const [apps, setApps] = useState<App[]>([])
  const [programmes, setProgrammes] = useState<Lookup[]>([])
  const [terms, setTerms] = useState<Lookup[]>([])
  const [newLead, setNewLead] = useState({ firstName: '', lastName: '', email: '', source: 'Website' })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [aiRec, setAiRec] = useState<string | null>(null)

  useEffect(() => { api.get<Health>('/api/admissions/crm/health').then(setHealth).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/programmes').then(setProgrammes).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])
  useEffect(() => {
    api.get<Lead[]>('/api/admissions/leads').then(setLeads).catch(e => setErr(e.message))
    api.get<App[]>('/api/admissions/applications').then(setApps).catch(e => setErr(e.message))
  }, [reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const createLead = () => go(() => api.post('/api/admissions/leads', newLead).then(() => setNewLead({ firstName: '', lastName: '', email: '', source: 'Website' })))
  const convertLead = (id: number) => { const p = prompt(`Programme id? (${programmes.map(x => `${x.id}:${x.name}`).join(', ')})`); const t = terms[0]?.id
    if (p && t) go(() => api.post(`/api/admissions/leads/${id}/convert`, { programmeId: +p, intakeTermId: t })) }
  const submit = (id: number) => go(() => api.post(`/api/admissions/applications/${id}/submit`, {}))
  const review = (id: number) => go(() => api.post(`/api/admissions/applications/${id}/review`, {}))
  const decide = (id: number) => { const c = prompt(`Decision category:\n${CATEGORIES.join('\n')}`); if (c) go(() => api.post(`/api/admissions/applications/${id}/decision`, { category: c })) }
  const respond = (id: number, r: string) => go(() => api.post(`/api/admissions/applications/${id}/offer-response`, { response: r }))
  const aiRecommend = (id: number) => go(() => api.post<any>(`/api/admissions/applications/${id}/ai-recommend`, {}).then(r =>
    setAiRec(`AI/Rules suggests: ${r.recommendedCategory} (confidence ${(r.confidence*100).toFixed(0)}%, via ${r.source}). ${r.rationale ?? ''} — a human still approves the decision.`)))
  const toStudent = (id: number) => { const n = prompt('New student number?'); if (n) go(() => api.post(`/api/admissions/applications/${id}/convert-to-student`, { studentNumber: n })) }

  return (
    <div>
      <h2>Admissions &amp; CRM</h2>
      <div className="card">
        <b>CRM:</b> {health ? `${health.provider}${health.external ? (health.reachable ? ' ✓' : ' (configured)') : ' (built-in)'}` : '…'}
        <p className="muted-text">Lead → application → review → decision (second-person) → offer response → student.
          Your website posts leads to <code>POST /api/public/lead</code> (no Mautic needed).</p>
      </div>
      {err && <p className="error">{err}</p>}
      {aiRec && <div className="card success-box"><b>Recommendation (advisory):</b> {aiRec}</div>}

      <div className="card">
        <h3>Leads</h3>
        <div className="row">
          <label>First <input value={newLead.firstName} onChange={e => setNewLead({ ...newLead, firstName: e.target.value })} /></label>
          <label>Last <input value={newLead.lastName} onChange={e => setNewLead({ ...newLead, lastName: e.target.value })} /></label>
          <label>Email <input value={newLead.email} onChange={e => setNewLead({ ...newLead, email: e.target.value })} /></label>
          <button disabled={!newLead.firstName || !newLead.lastName} onClick={createLead} style={{ alignSelf: 'flex-end' }}>+ Add lead</button>
        </div>
        <table className="grid" style={{ marginTop: 12 }}>
          <thead><tr><th>Name</th><th>Email</th><th>Source</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {leads.map(l => (<tr key={l.id}>
              <td>{l.firstName} {l.lastName}</td><td>{l.email ?? '—'}</td><td>{l.source ?? '—'}</td><td>{l.status}</td>
              <td>{l.status !== 'Converted' && l.status !== 'ApplicationStarted' && <button className="link" onClick={() => convertLead(l.id)}>→ Application</button>}</td>
            </tr>))}
            {leads.length === 0 && <tr><td colSpan={5}>No leads yet.</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3>Applications</h3>
        <table className="grid">
          <thead><tr><th>App #</th><th>Applicant</th><th>Status</th><th>Decision</th><th>Offer</th><th>Actions</th></tr></thead>
          <tbody>
            {apps.map(a => (<tr key={a.id}>
              <td>{a.applicationNumber}</td><td>{a.firstName} {a.lastName}</td><td>{a.status}</td>
              <td>{a.decision === 'None' ? '—' : a.decision}</td><td>{a.offerResponse}</td>
              <td className="actions">
                {a.status === 'Draft' && <button className="link" onClick={() => submit(a.id)}>Submit</button>}
                {a.status === 'Submitted' && <button className="link" onClick={() => review(a.id)}>Review</button>}
                {a.status === 'UnderReview' && <button className="link" onClick={() => aiRecommend(a.id)}>AI suggest</button>}
                {a.status === 'UnderReview' && <button className="link" onClick={() => decide(a.id)}>Decide</button>}
                {a.status === 'Accepted' && a.offerResponse === 'Pending' && <button className="link" onClick={() => respond(a.id, 'Accepted')}>Offer: Accept</button>}
                {a.status === 'Accepted' && !a.studentId && <button className="link" onClick={() => toStudent(a.id)}>→ Student</button>}
                {a.studentId && <span className="ok">Student #{a.studentId}</span>}
              </td>
            </tr>))}
            {apps.length === 0 && <tr><td colSpan={6}>No applications yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
