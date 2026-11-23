import { useEffect, useState } from 'react'
import { api } from '../api'

type DocumentDto = { id: number; title: string; category: string; studentId?: number; description?: string; currentVersion: number }
const CATEGORIES = ['Transcript','AdmissionDocument','IdDocument','Letter','Certificate','Receipt','Other']

export default function DocumentsPage() {
  const [docs, setDocs] = useState<DocumentDto[]>([])
  const [title, setTitle] = useState('')
  const [category, setCategory] = useState('Other')
  const [studentId, setStudentId] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<DocumentDto[]>('/api/documents').then(setDocs).catch(e => setErr(e.message)) }, [reload])

  const upload = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(null); setMsg(null)
    if (!file) { setErr('Choose a file.'); return }
    const fd = new FormData()
    fd.append('file', file); fd.append('title', title); fd.append('category', category)
    if (studentId) fd.append('studentId', studentId)
    try {
      await api.form('/api/documents', fd)
      setMsg('Uploaded.'); setTitle(''); setFile(null); setReload(x => x + 1)
    } catch (e) { setErr((e as Error).message) }
  }
  const del = async (id: number, t: string) => {
    if (!confirm(`Delete "${t}"? (soft-delete)`)) return
    try { await api.del(`/api/documents/${id}`); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) }
  }

  const ready = title && file
  return (
    <div>
      <h2>Documents</h2>
      <form onSubmit={upload} className="card">
        <h3>Upload a document</h3>
        <div className="row">
          <label>Title <input value={title} onChange={e => setTitle(e.target.value)} required /></label>
          <label>Category <select value={category} onChange={e => setCategory(e.target.value)}>
            {CATEGORIES.map(c => <option key={c}>{c}</option>)}</select></label>
          <label>Student ID (optional) <input value={studentId} onChange={e => setStudentId(e.target.value)} /></label>
          <label>File <input type="file" onChange={e => setFile(e.target.files?.[0] ?? null)} required /></label>
        </div>
        <button type="submit" disabled={!ready} style={{ marginTop: 12 }}>Upload</button>
      </form>

      {msg && <p className="ok">{msg}</p>}
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Document library</h3>
        <table className="grid">
          <thead><tr><th>Title</th><th>Category</th><th>Student</th><th>Version</th><th></th></tr></thead>
          <tbody>
            {docs.map(d => (
              <tr key={d.id}>
                <td>{d.title}</td><td>{d.category}</td><td>{d.studentId ?? '—'}</td><td>v{d.currentVersion}</td>
                <td className="actions">
                  <a href={`/api/documents/${d.id}/download`} target="_blank" rel="noreferrer">Download</a>
                  <button className="link danger" onClick={() => del(d.id, d.title)}>Delete</button>
                </td>
              </tr>
            ))}
            {docs.length === 0 && <tr><td colSpan={5}>No documents yet.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
