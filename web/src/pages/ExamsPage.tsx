import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type ExamDto = { id: number; title: string; courseId: number; termId: number; scheduledStart?: string
  durationMinutes: number; room?: string; totalMarks: number; status: string; questionCount: number }

export default function ExamsPage() {
  const [terms, setTerms] = useState<Lookup[]>([])
  const [courses, setCourses] = useState<Lookup[]>([])
  const [exams, setExams] = useState<ExamDto[]>([])
  const [form, setForm] = useState({ title: '', courseId: 0, termId: 0, scheduledStart: '', durationMinutes: 120, room: '' })
  const [msg, setMsg] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/courses').then(setCourses).catch(() => {}) }, [])
  useEffect(() => { api.get<ExamDto[]>('/api/exams').then(setExams).catch(e => setErr(e.message)) }, [reload])

  const create = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(null); setMsg(null)
    try {
      await api.post('/api/exams', { ...form, scheduledStart: form.scheduledStart || null })
      setMsg('Exam scheduled.'); setForm({ ...form, title: '', room: '' }); setReload(x => x + 1)
    } catch (e) { setErr((e as Error).message) }
  }
  const post = async (id: number) => {
    setErr(null); setMsg(null)
    try { const r = await api.post<any>(`/api/exams/${id}/post-to-gradebook`, {})
          setMsg(`Posted ${r.posted} result(s) to the gradebook (batch ${r.batchId}).`); setReload(x => x + 1) }
    catch (e) { setErr((e as Error).message) }
  }

  const ready = form.title && form.courseId > 0 && form.termId > 0
  return (
    <div>
      <h2>Examinations</h2>
      <form onSubmit={create} className="card">
        <h3>Schedule an exam</h3>
        <div className="row">
          <label>Title <input value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} required /></label>
          <label>Term <select value={form.termId} onChange={e => setForm({ ...form, termId: +e.target.value })}>
            <option value={0}>— select —</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>
          <label>Course <select value={form.courseId} onChange={e => setForm({ ...form, courseId: +e.target.value })}>
            <option value={0}>— select —</option>{courses.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}</select></label>
          <label>Date/time <input type="datetime-local" value={form.scheduledStart} onChange={e => setForm({ ...form, scheduledStart: e.target.value })} /></label>
          <label>Minutes <input type="number" value={form.durationMinutes} onChange={e => setForm({ ...form, durationMinutes: +e.target.value })} /></label>
          <label>Room <input value={form.room} onChange={e => setForm({ ...form, room: e.target.value })} /></label>
        </div>
        <button type="submit" disabled={!ready} style={{ marginTop: 12 }}>Schedule</button>
      </form>

      {msg && <p className="ok">{msg}</p>}
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Exams</h3>
        <table className="grid">
          <thead><tr><th>Title</th><th>When</th><th>Room</th><th>Qs</th><th>Total</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {exams.map(x => (
              <tr key={x.id}>
                <td>{x.title}</td>
                <td>{x.scheduledStart ? new Date(x.scheduledStart).toLocaleString() : '—'}</td>
                <td>{x.room ?? '—'}</td><td>{x.questionCount}</td><td>{x.totalMarks}</td><td>{x.status}</td>
                <td><button className="link" onClick={() => post(x.id)}>Post results → grades</button></td>
              </tr>
            ))}
            {exams.length === 0 && <tr><td colSpan={7}>No exams yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
