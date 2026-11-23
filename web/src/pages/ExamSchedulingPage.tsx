import { useEffect, useState } from 'react'
import { api, Lookup } from '../api'

type Room = { id: number; code: string; name: string; campusId: number; capacity: number }
type Clash = { type: string; severity: string; description: string; examId: number; otherExamId?: number; studentId?: number }

export default function ExamSchedulingPage() {
  const [rooms, setRooms] = useState<Room[]>([])
  const [terms, setTerms] = useState<Lookup[]>([])
  const [termId, setTermId] = useState(0)
  const [clashes, setClashes] = useState<Clash[] | null>(null)
  const [newRoom, setNewRoom] = useState({ code: '', name: '', campusId: 1, capacity: 30 })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Room[]>('/api/examscheduling/rooms').then(setRooms).catch(e => setErr(e.message)) }, [reload])
  useEffect(() => { api.get<Lookup[]>('/api/lookups/terms').then(setTerms).catch(() => {}) }, [])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn() } catch (e) { setErr((e as Error).message) } }
  const addRoom = () => go(() => api.post('/api/examscheduling/rooms', newRoom).then(() => { setNewRoom({ ...newRoom, code: '', name: '' }); setReload(x => x + 1) }))
  const checkClashes = () => go(() => api.get<Clash[]>(`/api/examscheduling/clashes/${termId}`).then(setClashes))

  return (
    <div>
      <h2>Exam Scheduling</h2>
      <p className="muted-text">Rooms, room allocation, invigilators and special arrangements feed automatic clash
        detection: student overlaps, double-booked rooms/invigilators, over-capacity, and too many exams a day
        (extra-time arrangements are accounted for).</p>
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Exam rooms</h3>
        <div className="row">
          <label>Code <input value={newRoom.code} onChange={e => setNewRoom({ ...newRoom, code: e.target.value })} /></label>
          <label>Name <input value={newRoom.name} onChange={e => setNewRoom({ ...newRoom, name: e.target.value })} /></label>
          <label>Campus ID <input type="number" value={newRoom.campusId} onChange={e => setNewRoom({ ...newRoom, campusId: +e.target.value })} /></label>
          <label>Capacity <input type="number" value={newRoom.capacity} onChange={e => setNewRoom({ ...newRoom, capacity: +e.target.value })} /></label>
          <button disabled={!newRoom.code || !newRoom.name} onClick={addRoom} style={{ alignSelf: 'flex-end' }}>+ Add room</button>
        </div>
        <table className="grid" style={{ marginTop: 12 }}>
          <thead><tr><th>Code</th><th>Name</th><th>Campus</th><th>Capacity</th></tr></thead>
          <tbody>
            {rooms.map(r => <tr key={r.id}><td>{r.code}</td><td>{r.name}</td><td>{r.campusId}</td><td>{r.capacity}</td></tr>)}
            {rooms.length === 0 && <tr><td colSpan={4}>No rooms yet.</td></tr>}
          </tbody>
        </table>
      </div>

      <div className="card">
        <h3>Clash check</h3>
        <div className="row">
          <label>Term <select value={termId} onChange={e => setTermId(+e.target.value)}>
            <option value={0}>— select —</option>{terms.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>
          <button disabled={!termId} onClick={checkClashes} style={{ alignSelf: 'flex-end' }}>Run clash check</button>
        </div>
        {clashes && (clashes.length === 0
          ? <p className="ok" style={{ marginTop: 12 }}>No clashes found ✓</p>
          : <table className="grid" style={{ marginTop: 12 }}>
              <thead><tr><th>Type</th><th>Severity</th><th>Detail</th></tr></thead>
              <tbody>{clashes.map((c, i) => <tr key={i} className={c.severity === 'Error' ? 'error' : 'warning'}>
                <td>{c.type}</td><td>{c.severity}</td><td>{c.description}</td></tr>)}</tbody>
            </table>)}
      </div>
    </div>
  )
}
