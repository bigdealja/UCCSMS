import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

export default function MoodlePage() {
  const [health, setHealth] = useState<{ configured: boolean; reachable: boolean } | null>(null)
  const [terms, setTerms] = useState<Lookup[]>([])
  const [courses, setCourses] = useState<Lookup[]>([])
  const [termId, setTermId] = useState(0)
  const [courseId, setCourseId] = useState(0)
  const [msg, setMsg] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => { api.get<any>('/api/moodle/health').then(setHealth).catch(() => setHealth({ configured: false, reachable: false })) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/courses').then(setCourses).catch(() => {}) }, [])

  const run = (fn: () => Promise<any>, label: string) => async () => {
    setBusy(true); setErr(null); setMsg(null)
    try { const r = await fn(); setMsg(`${label}: ${JSON.stringify(r)}`) }
    catch (e) { setErr((e as Error).message) } finally { setBusy(false) }
  }

  const ready = termId > 0 && courseId > 0
  return (
    <div>
      <h2>Moodle Sync</h2>
      <div className="card">
        <h3>Connection</h3>
        {health === null ? <p>Checking…</p> :
          !health.configured ? <p className="warn">Moodle is not configured yet (set Moodle:BaseUrl and Moodle:Token).</p> :
          health.reachable ? <p className="ok">Connected to Moodle ✓</p> :
          <p className="err">Configured, but Moodle is not reachable (check URL/token).</p>}
      </div>

      <div className="card">
        <h3>Sync a course</h3>
        <div className="row">
          <label>Term <select value={termId} onChange={e => setTermId(+e.target.value)}>
            <option value={0}>— select —</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select></label>
          <label>Course <select value={courseId} onChange={e => setCourseId(+e.target.value)}>
            <option value={0}>— select —</option>{courses.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
          </select></label>
        </div>
        <div className="row" style={{ marginTop: 12 }}>
          <button disabled={!ready || busy} onClick={run(() => api.post(`/api/moodle/provision/${courseId}/${termId}`, {}), 'Provisioned')}>
            Provision &amp; enrol (SMS → Moodle)
          </button>
          <button disabled={!ready || busy} className="secondary"
            onClick={run(() => api.post(`/api/moodle/pull-grades/${courseId}/${termId}`, {}), 'Grades pulled')}>
            Pull grades (Moodle → SMS)
          </button>
        </div>
      </div>

      {msg && <div className="card success-box"><code>{msg}</code></div>}
      {err && <p className="error">{err}</p>}
    </div>
  )
}
