import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'

type Overview = {
  students: { total: number; byStatus: { status: string; count: number }[] }
  admissions: { leads: number; applications: number; enrolled: number; conversionRate: number }
  cases: { open: number; overdue: number }
  exams: { scheduled: number; clashes: number }
  finance: { ledgerBalance: number }
}
type Note = { id: number; title: string; body?: string; category: string; severity: string; isRead: boolean; createdUtc: string }

export default function DashboardPage() {
  const [m, setM] = useState<Overview | null>(null)
  const [notes, setNotes] = useState<Note[]>([])
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Overview>('/api/metrics/overview').then(setM).catch(e => setErr(e.message)) }, [reload])
  useEffect(() => { api.get<Note[]>('/api/notifications/inbox').then(setNotes).catch(() => {}) }, [reload])

  const runAlerts = async () => { try { await api.post('/api/notifications/run-alerts', {}); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const markRead = async (id: number) => { try { await api.post(`/api/notifications/${id}/read`, {}); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }

  const tile = (label: string, val: string | number, to?: string, cls = '') => {
    const inner = <div className="card" style={{ textAlign: 'center' }}>
      <div style={{ fontSize: 30, fontWeight: 700 }} className={cls}>{val}</div>
      <div className="muted-text">{label}</div></div>
    return to ? <Link to={to} style={{ textDecoration: 'none', color: 'inherit', flex: 1 }}>{inner}</Link> : <div style={{ flex: 1 }}>{inner}</div>
  }

  return (
    <div>
      <h2>Dashboard</h2>
      {err && <p className="error">{err}</p>}
      {m ? (
        <>
          <div className="row">
            {tile('Students', m.students.total, '/students')}
            {tile('Leads', m.admissions.leads, '/admissions')}
            {tile('Applications', m.admissions.applications, '/admissions')}
            {tile('Conversion', `${m.admissions.conversionRate}%`)}
          </div>
          <div className="row">
            {tile('Open cases', m.cases.open, '/cases')}
            {tile('Overdue cases', m.cases.overdue, '/cases', m.cases.overdue > 0 ? 'err' : '')}
            {tile('Exams scheduled', m.exams.scheduled, '/exam-scheduling')}
            {tile('Exam clashes', m.exams.clashes, '/exam-scheduling', m.exams.clashes > 0 ? 'err' : '')}
          </div>
        </>
      ) : <p>Loading metrics…</p>}

      <div className="card">
        <div className="row between">
          <h3>Notifications</h3>
          <button onClick={runAlerts}>Run alert check</button>
        </div>
        <p className="muted-text">Alerts are generated from records (overdue cases, exam clashes) — on a schedule in production, or on demand here.</p>
        <table className="grid">
          <thead><tr><th>Severity</th><th>Title</th><th>Detail</th><th>When</th><th></th></tr></thead>
          <tbody>
            {notes.map(n => (<tr key={n.id} className={n.severity === 'Error' ? 'error' : n.severity === 'Warning' ? 'warning' : ''} style={{ opacity: n.isRead ? 0.55 : 1 }}>
              <td>{n.severity}</td><td>{n.title}</td><td>{n.body}</td><td>{new Date(n.createdUtc).toLocaleString()}</td>
              <td>{!n.isRead && <button className="link" onClick={() => markRead(n.id)}>Mark read</button>}</td>
            </tr>))}
            {notes.length === 0 && <tr><td colSpan={5}>No notifications.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
