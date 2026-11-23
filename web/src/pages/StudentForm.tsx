import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, Lookup } from '../api'

type Upsert = {
  studentNumber: string; firstName: string; lastName: string
  email?: string; status: string; programmeId: number; dateOfBirth?: string
}
const STATUSES = ['Prospective','Applicant','Accepted','Active','Inactive','Graduated','Withdrawn','Suspended']

export default function StudentForm() {
  const { id } = useParams()
  const editing = !!id
  const nav = useNavigate()
  const [programmes, setProgrammes] = useState<Lookup[]>([])
  const [form, setForm] = useState<Upsert>({ studentNumber: '', firstName: '', lastName: '', email: '', status: 'Applicant', programmeId: 0 })
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { api.get<Lookup[]>('/api/lookups/programmes').then(setProgrammes).catch(() => {}) }, [])
  useEffect(() => {
    if (editing) api.get<any>(`/api/students/${id}`).then(s => setForm({
      studentNumber: s.studentNumber, firstName: s.firstName, lastName: s.lastName,
      email: s.email ?? '', status: s.status, programmeId: s.programmeId
    })).catch(e => setError(e.message))
  }, [id])

  const save = async (e: React.FormEvent) => {
    e.preventDefault(); setError(null)
    try {
      if (editing) await api.put(`/api/students/${id}`, form)
      else await api.post('/api/students', form)
      nav('/students')
    } catch (err) { setError((err as Error).message) }
  }

  const upd = (k: keyof Upsert) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm({ ...form, [k]: k === 'programmeId' ? +e.target.value : e.target.value })

  return (
    <form onSubmit={save} className="card" style={{ maxWidth: 520 }}>
      <h2>{editing ? 'Edit' : 'New'} student</h2>
      {error && <p className="error">{error}</p>}
      <label>Student # <input value={form.studentNumber} onChange={upd('studentNumber')} required disabled={editing} /></label>
      <label>First name <input value={form.firstName} onChange={upd('firstName')} required /></label>
      <label>Last name <input value={form.lastName} onChange={upd('lastName')} required /></label>
      <label>Email <input type="email" value={form.email} onChange={upd('email')} /></label>
      <label>Status <select value={form.status} onChange={upd('status')}>{STATUSES.map(s => <option key={s}>{s}</option>)}</select></label>
      <label>Programme <select value={form.programmeId} onChange={upd('programmeId')} required>
        <option value={0}>— select —</option>{programmes.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></label>
      <div className="row" style={{ marginTop: 12 }}>
        <button type="submit">Save</button>
        <button type="button" className="secondary" onClick={() => nav('/students')}>Cancel</button>
      </div>
    </form>
  )
}
