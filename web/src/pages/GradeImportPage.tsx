import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type RowStatus = 'Ok' | 'Warning' | 'Error'
type PreviewRow = {
  rowNumber: number; studentNumber: string; resolvedStudentName?: string
  letterGrade?: string; coursework?: number; finalExam?: number
  isNewGrade: boolean; status: RowStatus; messages: string[]
}
type ImportPreview = { rows: PreviewRow[]; okCount: number; warningCount: number; errorCount: number }
type CommitResult = { batchId: string; applied: number; skipped: number }

const NONE = ''

export default function GradeImportPage() {
  const [terms, setTerms] = useState<Lookup[]>([])
  const [courses, setCourses] = useState<Lookup[]>([])
  const [termId, setTermId] = useState<number>(0)
  const [courseId, setCourseId] = useState<number>(0)
  const [file, setFile] = useState<File | null>(null)
  const [columns, setColumns] = useState<string[]>([])
  const [map, setMap] = useState({ student: NONE, letter: NONE, coursework: NONE, finalExam: NONE })
  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const [result, setResult] = useState<CommitResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/courses').then(setCourses).catch(() => {}) }, [])

  const run = async (fn: () => Promise<void>) => {
    setBusy(true); setError(null)
    try { await fn() } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }

  const readColumns = (f: File) => run(async () => {
    setFile(f); setPreview(null); setResult(null)
    const fd = new FormData(); fd.append('file', f)
    setColumns(await api.form<string[]>('/api/gradeimport/columns', fd))
  })

  const buildForm = () => {
    const fd = new FormData()
    fd.append('file', file!); fd.append('termId', String(termId)); fd.append('courseId', String(courseId))
    fd.append('studentNumberColumn', map.student)
    if (map.letter) fd.append('letterGradeColumn', map.letter)
    if (map.coursework) fd.append('courseworkColumn', map.coursework)
    if (map.finalExam) fd.append('finalExamColumn', map.finalExam)
    return fd
  }

  const doPreview = () => run(async () => { setResult(null); setPreview(await api.form<ImportPreview>('/api/gradeimport/preview', buildForm())) })
  const doCommit  = () => run(async () => { setResult(await api.form<CommitResult>('/api/gradeimport/commit', buildForm())) })
  const undo = (batchId: string) => run(async () => {
    await api.post(`/api/enrollments/undo-batch/${batchId}`, {}); setResult(null); setPreview(null)
  })

  const canMap = file && columns.length > 0
  const canPreview = canMap && termId > 0 && courseId > 0 && map.student
  const sel = (v: string, set: (s: string) => void, allowNone = false) => (
    <select value={v} onChange={e => set(e.target.value)}>
      {allowNone && <option value={NONE}>— none —</option>}
      {columns.map(c => <option key={c} value={c}>{c}</option>)}
    </select>
  )

  return (
    <div>
      <h2>Grade Import</h2>
      <p className="muted-text">Upload an Excel (.xlsx) or CSV file, map its columns, preview, then commit.
        No named ranges required — this replaces the old 32-bit Excel importer.</p>

      <div className="card">
        <h3>1 · Term, course &amp; file</h3>
        <div className="row">
          <label>Term <select value={termId} onChange={e => setTermId(+e.target.value)}>
            <option value={0}>— select —</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select></label>
          <label>Course <select value={courseId} onChange={e => setCourseId(+e.target.value)}>
            <option value={0}>— select —</option>{courses.map(c => <option key={c.id} value={c.id}>{c.code} — {c.name}</option>)}
          </select></label>
          <label>File <input type="file" accept=".xlsx,.csv" onChange={e => e.target.files?.[0] && readColumns(e.target.files[0])} /></label>
        </div>
      </div>

      {canMap && (
        <div className="card">
          <h3>2 · Map columns</h3>
          <div className="row">
            <label>Student # {sel(map.student, v => setMap({ ...map, student: v }))}</label>
            <label>Letter grade {sel(map.letter, v => setMap({ ...map, letter: v }), true)}</label>
            <label>Coursework {sel(map.coursework, v => setMap({ ...map, coursework: v }), true)}</label>
            <label>Final exam {sel(map.finalExam, v => setMap({ ...map, finalExam: v }), true)}</label>
          </div>
          <button disabled={!canPreview || busy} onClick={doPreview}>Preview</button>
        </div>
      )}

      {error && <p className="error">{error}</p>}

      {preview && (
        <div className="card">
          <h3>3 · Preview</h3>
          <p><b className="ok">{preview.okCount} OK</b> · <b className="warn">{preview.warningCount} warnings</b> · <b className="err">{preview.errorCount} errors</b></p>
          <table className="grid">
            <thead><tr><th>Row</th><th>Student #</th><th>Name</th><th>Grade</th><th>CW</th><th>Final</th><th>Status</th><th>Notes</th></tr></thead>
            <tbody>
              {preview.rows.map(r => (
                <tr key={r.rowNumber} className={r.status.toLowerCase()}>
                  <td>{r.rowNumber}</td><td>{r.studentNumber}</td><td>{r.resolvedStudentName ?? '—'}</td>
                  <td>{r.letterGrade ?? '—'}</td><td>{r.coursework ?? '—'}</td><td>{r.finalExam ?? '—'}</td>
                  <td>{r.status}</td><td>{r.messages.join(' ')}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <button disabled={busy || preview.errorCount === preview.rows.length} onClick={doCommit}>
            Commit {preview.okCount + preview.warningCount} rows
          </button>
        </div>
      )}

      {result && (
        <div className="card success-box">
          <h3>Import complete</h3>
          <p>Applied <b>{result.applied}</b>, skipped <b>{result.skipped}</b>. Batch <code>{result.batchId}</code></p>
          <button onClick={() => undo(result.batchId)}>Undo this batch</button>
        </div>
      )}
    </div>
  )
}
