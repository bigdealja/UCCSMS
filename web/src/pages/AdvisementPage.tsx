import { useState } from 'react'
import { api } from '../api'

type Course = { courseId: number; code: string; title: string; credits: number; grade?: string; passed: boolean; attempted: boolean }
type Progress = { studentId: number; programme: string; level: string; creditsRequired: number; creditsEarned: number; gpa: number; completed: Course[]; outstanding: Course[]; failed: Course[] }
type Verdict = { eligible: boolean; gpa: number; minGpa: number; creditsEarned: number; creditsRequired: number; outstandingCount: number; reasons: string[] }

export default function AdvisementPage() {
  const [studentId, setStudentId] = useState('1')
  const [p, setP] = useState<Progress | null>(null)
  const [v, setV] = useState<Verdict | null>(null)
  const [err, setErr] = useState<string | null>(null)

  const load = async () => {
    setErr(null); setP(null); setV(null)
    try {
      setP(await api.get<Progress>(`/api/advisement/students/${studentId}/progress`))
      setV(await api.get<Verdict>(`/api/advisement/students/${studentId}/graduation-check`))
    } catch (e) { setErr((e as Error).message) }
  }
  const courseTable = (list: Course[], label: string) => (
    <div className="card">
      <h3>{label} ({list.length})</h3>
      <table className="grid">
        <thead><tr><th>Code</th><th>Title</th><th>Credits</th><th>Grade</th></tr></thead>
        <tbody>{list.map(c => <tr key={c.courseId} className={c.passed ? '' : c.attempted ? 'warning' : ''}>
          <td>{c.code}</td><td>{c.title}</td><td>{c.credits}</td><td>{c.grade ?? (c.attempted ? '—' : 'not taken')}</td></tr>)}
          {list.length === 0 && <tr><td colSpan={4}>None.</td></tr>}</tbody>
      </table>
    </div>
  )

  return (
    <div>
      <h2>Advisement &amp; Graduation</h2>
      <div className="card">
        <div className="row">
          <label>Student ID <input value={studentId} onChange={e => setStudentId(e.target.value)} style={{ width: 120 }} /></label>
          <button onClick={load} style={{ alignSelf: 'flex-end' }}>Check</button>
        </div>
      </div>
      {err && <p className="error">{err}</p>}

      {p && (
        <div className="row">
          <div className="card" style={{ flex: 1, textAlign: 'center' }}><div style={{ fontSize: 28, fontWeight: 700 }}>{p.creditsEarned}/{p.creditsRequired}</div><div className="muted-text">Credits</div></div>
          <div className="card" style={{ flex: 1, textAlign: 'center' }}><div style={{ fontSize: 28, fontWeight: 700 }}>{p.gpa.toFixed(2)}</div><div className="muted-text">GPA</div></div>
          <div className="card" style={{ flex: 1, textAlign: 'center' }}><div style={{ fontSize: 28, fontWeight: 700 }} className={p.outstanding.length ? 'warn' : 'ok'}>{p.outstanding.length}</div><div className="muted-text">Outstanding</div></div>
        </div>
      )}

      {v && (
        <div className={`card ${v.eligible ? 'success-box' : ''}`}>
          <h3>Graduation check — {p?.programme} ({p?.level})</h3>
          {v.eligible
            ? <p className="ok"><b>Eligible to graduate ✓</b> — {v.creditsEarned}/{v.creditsRequired} credits, GPA {v.gpa.toFixed(2)} (min {v.minGpa.toFixed(2)}).</p>
            : <><p className="err"><b>Not yet eligible.</b></p><ul>{v.reasons.map((r, i) => <li key={i}>{r}</li>)}</ul></>}
        </div>
      )}

      {p && <>{courseTable(p.completed, 'Completed')}{courseTable(p.outstanding, 'Outstanding')}</>}
    </div>
  )
}
