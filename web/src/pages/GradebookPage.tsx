import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type Component = { id: number; name: string; category: string; weight: number; maxMark: number }
type Row = { studentId: number; studentNumber: string; name: string; marks: Record<string, number | null>; coursework?: number; final?: number; letter?: string; passed?: boolean }
type Sheet = { components: Component[]; rows: Row[] }

export default function GradebookPage() {
  const [courses, setCourses] = useState<Lookup[]>([])
  const [terms, setTerms] = useState<Lookup[]>([])
  const [courseId, setCourseId] = useState(0)
  const [termId, setTermId] = useState(0)
  const [sheet, setSheet] = useState<Sheet | null>(null)
  const [weightTotal, setWeightTotal] = useState(0)
  const [comp, setComp] = useState({ name: '', category: 'Coursework', weight: 0, maxMark: 100 })
  const [err, setErr] = useState<string | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Lookup[]>('/api/lookups/courses').then(setCourses).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])
  useEffect(() => {
    if (!courseId || !termId) { setSheet(null); return }
    api.get<Sheet>(`/api/gradebook/sheet?courseId=${courseId}&termId=${termId}`).then(setSheet).catch(e => setErr(e.message))
    api.get<any>(`/api/gradebook/components?courseId=${courseId}&termId=${termId}`).then(r => setWeightTotal(r.weightTotal)).catch(() => {})
  }, [courseId, termId, reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); setMsg(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const addComp = () => go(() => api.post('/api/gradebook/components', { courseId, termId, ...comp }).then(() => setComp({ ...comp, name: '', weight: 0 })))
  const enter = (componentId: number, studentId: number, v: string) => {
    const mark = parseFloat(v); if (isNaN(mark)) return
    go(() => api.post('/api/gradebook/marks', { componentId, studentId, mark }))
  }
  const post = () => go(() => api.post<any>('/api/gradebook/post', { courseId, termId }).then(r => setMsg(`Posted ${r.written} grade(s) to the record (batch ${r.batchId}). Skipped ${r.skipped}.`)))

  const ready = courseId > 0 && termId > 0
  return (
    <div>
      <h2>Gradebook</h2>
      <p className="muted-text">Enter assessment components and marks directly in the system; it calculates coursework%,
        final% and the letter grade per policy (coursework ≥50 AND end-of-semester ≥50 AND overall pass mark). No external
        grade sheet. (Bulk import remains available under <b>Grade Import</b> for exceptional cases.)</p>
      {err && <p className="error">{err}</p>}
      {msg && <p className="ok">{msg}</p>}

      <div className="card">
        <div className="row">
          <label>Course <select value={courseId} onChange={e => setCourseId(+e.target.value)}>
            <option value={0}>—</option>{courses.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}</select></label>
          <label>Term <select value={termId} onChange={e => setTermId(+e.target.value)}>
            <option value={0}>—</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>
        </div>
      </div>

      {ready && (<>
        <div className="card">
          <h3>Components <span className="muted-text" style={{ fontSize: 13, fontWeight: 400 }}>(weights total {weightTotal}%{weightTotal !== 100 ? ' — should be 100' : ' ✓'})</span></h3>
          <div className="row">
            <label>Name <input value={comp.name} onChange={e => setComp({ ...comp, name: e.target.value })} /></label>
            <label>Category <select value={comp.category} onChange={e => setComp({ ...comp, category: e.target.value })}>
              <option>Coursework</option><option>EndOfSemester</option></select></label>
            <label>Weight % <input type="number" value={comp.weight} onChange={e => setComp({ ...comp, weight: +e.target.value })} style={{ width: 80 }} /></label>
            <label>Max mark <input type="number" value={comp.maxMark} onChange={e => setComp({ ...comp, maxMark: +e.target.value })} style={{ width: 90 }} /></label>
            <button disabled={!comp.name || comp.weight <= 0} onClick={addComp} style={{ alignSelf: 'flex-end' }}>+ Component</button>
          </div>
        </div>

        <div className="card">
          <div className="row between"><h3>Grade sheet</h3>
            <button disabled={!sheet || sheet.rows.length === 0} onClick={post}>Post to record</button></div>
          {sheet && (
            <table className="grid">
              <thead><tr>
                <th>Student</th>
                {sheet.components.map(c => <th key={c.id} title={`${c.category} · ${c.weight}% · /${c.maxMark}`}>{c.name}<br /><span className="muted-text" style={{ fontWeight: 400 }}>{c.weight}%</span></th>)}
                <th>CW%</th><th>Final%</th><th>Grade</th>
              </tr></thead>
              <tbody>
                {sheet.rows.map(r => (<tr key={r.studentId} className={r.passed === false ? 'warning' : ''}>
                  <td>{r.name}<br /><span className="muted-text" style={{ fontSize: 11 }}>{r.studentNumber}</span></td>
                  {sheet.components.map(c => (
                    <td key={c.id}><input defaultValue={r.marks[String(c.id)] ?? ''} onBlur={e => enter(c.id, r.studentId, e.target.value)}
                      style={{ width: 56, padding: '4px 6px' }} /></td>))}
                  <td>{r.coursework ?? '—'}</td><td>{r.final ?? '—'}</td>
                  <td><b>{r.letter ?? '—'}</b>{r.passed === false ? ' ✗' : ''}</td>
                </tr>))}
                {sheet.rows.length === 0 && <tr><td colSpan={sheet.components.length + 4}>No registered students.</td></tr>}
              </tbody>
            </table>
          )}
        </div>
      </>)}
    </div>
  )
}
