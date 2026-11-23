import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../api'

type StudentDto = { id: number; studentNumber: string; firstName: string; lastName: string; email?: string; status: string; programmeId: number }
type Paged<T> = { items: T[]; total: number; page: number; pageSize: number }

export default function StudentsPage() {
  const [q, setQ] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<Paged<StudentDto> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const nav = useNavigate()

  useEffect(() => {
    const ctrl = new AbortController()
    fetch(`/api/students?q=${encodeURIComponent(q)}&page=${page}&pageSize=25`, { signal: ctrl.signal })
      .then(r => r.ok ? r.json() : Promise.reject(new Error(`API ${r.status}`)))
      .then(setData).catch(e => { if (e.name !== 'AbortError') setError(e.message) })
    return () => ctrl.abort()
  }, [q, page, reload])

  const remove = async (id: number, name: string) => {
    if (!confirm(`Delete ${name}? (soft-delete, recoverable)`)) return
    try { await api.del(`/api/students/${id}`); setReload(x => x + 1) } catch (e) { setError((e as Error).message) }
  }

  return (
    <div>
      <div className="row between">
        <h2>Students</h2>
        <button onClick={() => nav('/students/new')}>+ New student</button>
      </div>
      <input placeholder="Search name or student number…" value={q}
        onChange={e => { setPage(1); setQ(e.target.value) }} className="search" />
      {error && <p className="error">Could not load students: {error} (is the API running?)</p>}
      <table className="grid">
        <thead><tr><th>Student #</th><th>Name</th><th>Email</th><th>Status</th><th></th></tr></thead>
        <tbody>
          {data?.items.map(s => (
            <tr key={s.id}>
              <td>{s.studentNumber}</td>
              <td>{s.lastName}, {s.firstName}</td>
              <td>{s.email ?? '—'}</td>
              <td>{s.status}</td>
              <td className="actions">
                <Link to={`/students/${s.id}/edit`}>Edit</Link>
                <a href={`/api/pdf/transcript/${s.id}`} target="_blank" rel="noreferrer">Transcript</a>
                <button className="link danger" onClick={() => remove(s.id, `${s.firstName} ${s.lastName}`)}>Delete</button>
              </td>
            </tr>
          ))}
          {data && data.items.length === 0 && <tr><td colSpan={5}>No students found.</td></tr>}
        </tbody>
      </table>
      {data && (
        <div className="pager">
          <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Prev</button>
          <span>Page {data.page} · {data.total} total</span>
          <button disabled={page * data.pageSize >= data.total} onClick={() => setPage(p => p + 1)}>Next</button>
        </div>
      )}
    </div>
  )
}
