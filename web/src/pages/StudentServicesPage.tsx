import { useState } from 'react'
import { api } from '../api'

type Housing = { id: number; hall: string; room: string; status: string }
type ChapelSummary = { studentId: number; attended: number; total: number; attendanceRate: number }

export default function StudentServicesPage() {
  const [studentId, setStudentId] = useState('1')
  const [hall, setHall] = useState('North Hall')
  const [room, setRoom] = useState('204')
  const [sport, setSport] = useState('Soccer')
  const [season, setSeason] = useState('2026')
  const [eventName, setEventName] = useState('Chapel Week 1')
  const [eventDate, setEventDate] = useState('2026-09-01')
  const [present, setPresent] = useState(true)
  const [reason, setReason] = useState('')
  const [notes, setNotes] = useState('')
  const [housing, setHousing] = useState<Housing | null>(null)
  const [summary, setSummary] = useState<ChapelSummary | null>(null)
  const [msg, setMsg] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)

  const run = async (fn: () => Promise<void>) => { setErr(null); setMsg(null); try { await fn() } catch (e) { setErr((e as Error).message) } }

  const assign = () => run(async () => {
    const h = await api.post<Housing>('/api/student-services/housing', { studentId: +studentId, hall, room, termId: null })
    setHousing(h); setMsg(`Housing assigned (#${h.id}).`)
  })
  const checkIn = () => run(async () => { if (housing) { await api.post(`/api/student-services/housing/${housing.id}/check-in`, {}); setMsg('Checked in.') } })
  const checkOut = () => run(async () => { if (housing) { await api.post(`/api/student-services/housing/${housing.id}/check-out`, {}); setMsg('Checked out.') } })
  const addSport = () => run(async () => { await api.post('/api/student-services/sports', { studentId: +studentId, sport, team: null, role: null, season }); setMsg(`Added ${sport} participation.`) })
  const recordChapel = () => run(async () => { await api.post('/api/student-services/chapel', { studentId: +studentId, eventName, eventDate, present }); setMsg('Chapel attendance recorded.') })
  const loadSummary = () => run(async () => { setSummary(await api.get<ChapelSummary>(`/api/student-services/chapel/${studentId}/summary`)) })
  const recordVisit = () => run(async () => { await api.post('/api/health/visits', { studentId: +studentId, reason, notes, seenBy: null }); setMsg('Health visit recorded (confidential).'); setReason(''); setNotes('') })

  return (
    <div>
      <h2>Student Services</h2>
      <div className="card">
        <label>Student ID <input value={studentId} onChange={e => setStudentId(e.target.value)} style={{ width: 120 }} /></label>
      </div>
      {err && <p className="error">{err}</p>}
      {msg && <p className="ok">{msg}</p>}

      <div className="card">
        <h3>Housing</h3>
        <div className="row">
          <label>Hall <input value={hall} onChange={e => setHall(e.target.value)} /></label>
          <label>Room <input value={room} onChange={e => setRoom(e.target.value)} style={{ width: 100 }} /></label>
          <button onClick={assign} style={{ alignSelf: 'flex-end' }}>Assign</button>
          <button onClick={checkIn} disabled={!housing} style={{ alignSelf: 'flex-end' }}>Check in</button>
          <button onClick={checkOut} disabled={!housing} style={{ alignSelf: 'flex-end' }}>Check out</button>
        </div>
        {housing && <p className="muted-text">Assignment #{housing.id}: {housing.hall} {housing.room} — {housing.status}</p>}
      </div>

      <div className="card">
        <h3>Sports</h3>
        <div className="row">
          <label>Sport <input value={sport} onChange={e => setSport(e.target.value)} /></label>
          <label>Season <input value={season} onChange={e => setSeason(e.target.value)} style={{ width: 100 }} /></label>
          <button onClick={addSport} style={{ alignSelf: 'flex-end' }}>Add participation</button>
        </div>
      </div>

      <div className="card">
        <h3>Chapel attendance</h3>
        <div className="row">
          <label>Event <input value={eventName} onChange={e => setEventName(e.target.value)} /></label>
          <label>Date <input type="date" value={eventDate} onChange={e => setEventDate(e.target.value)} /></label>
          <label style={{ alignSelf: 'flex-end' }}><input type="checkbox" checked={present} onChange={e => setPresent(e.target.checked)} /> Present</label>
          <button onClick={recordChapel} style={{ alignSelf: 'flex-end' }}>Record</button>
          <button onClick={loadSummary} style={{ alignSelf: 'flex-end' }}>Summary</button>
        </div>
        {summary && <p>Attendance: <b>{summary.attended}/{summary.total}</b> ({summary.attendanceRate}%)</p>}
      </div>

      <div className="card">
        <h3>Health visit <span className="muted-text">— confidential; access is audit-logged</span></h3>
        <div className="row">
          <label>Reason <input value={reason} onChange={e => setReason(e.target.value)} /></label>
          <label>Notes <input value={notes} onChange={e => setNotes(e.target.value)} style={{ width: 240 }} /></label>
          <button onClick={recordVisit} disabled={!reason} style={{ alignSelf: 'flex-end' }}>Record visit</button>
        </div>
      </div>
    </div>
  )
}
