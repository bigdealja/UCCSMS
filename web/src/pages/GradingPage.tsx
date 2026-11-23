import { useEffect, useState } from 'react'
import { api } from '../api'

type Band = { letter: string; minPercent?: number; maxPercent?: number; qualityPoints: number; isPass: boolean; countsInGpa: boolean; isSpecial: boolean }
type Scheme = { id: number; name: string; level: string; effectiveFrom: string; overallPassMark: number; courseworkPassMark: number; eosaPassMark: number; bands: Band[] }

export default function GradingPage() {
  const [schemes, setSchemes] = useState<Scheme[]>([])
  const [err, setErr] = useState<string | null>(null)
  useEffect(() => { api.get<Scheme[]>('/api/grading/schemes').then(setSchemes).catch(e => setErr(e.message)) }, [])

  return (
    <div>
      <h2>Grade Schemes</h2>
      <p className="muted-text">The UCC Examination Policy grading scales — configurable, versioned with effective dates.
        Pass rule: coursework ≥ minimum AND end-of-semester ≥ minimum AND combined ≥ overall pass mark.</p>
      {err && <p className="error">{err}</p>}
      {schemes.map(s => (
        <div className="card" key={s.id}>
          <h3>{s.name} <span className="muted-text" style={{ fontWeight: 400, fontSize: 13 }}>
            ({s.level}, effective {s.effectiveFrom} · pass {s.overallPassMark}% · coursework ≥{s.courseworkPassMark}% · EOSA ≥{s.eosaPassMark}%)</span></h3>
          <table className="grid">
            <thead><tr><th>Letter</th><th>Range</th><th>Quality points</th><th>Pass</th><th>In GPA</th></tr></thead>
            <tbody>
              {s.bands.map((b, i) => (
                <tr key={i} className={b.isSpecial ? 'warning' : ''}>
                  <td><b>{b.letter}</b></td>
                  <td>{b.isSpecial ? 'special' : `${b.minPercent}–${b.maxPercent}%`}</td>
                  <td>{b.isSpecial ? '—' : b.qualityPoints.toFixed(2)}</td>
                  <td>{b.isPass ? 'yes' : 'no'}</td>
                  <td>{b.countsInGpa ? 'yes' : 'no'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  )
}
