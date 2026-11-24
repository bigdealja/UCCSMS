import { useEffect, useState } from 'react'
import { api } from '../api'

type Rate = { id: number; workType: string; ratePerUnit: number; unit?: string; effectiveFrom: string }
type Claim = { id: number; claimantName: string; workType: string; units: number; ratePerUnit: number; amount: number; status: string; submittedBy?: string; approvedBy?: string; externalRef?: string }
type Health = { provider: string; external: boolean; reachable?: boolean | null }
const TYPES = ['Marking','Invigilation','ExternalExamining','Teaching','Other']

export default function PayrollPage() {
  const [health, setHealth] = useState<Health | null>(null)
  const [rates, setRates] = useState<Rate[]>([])
  const [claims, setClaims] = useState<Claim[]>([])
  const [rate, setRate] = useState({ workType: 'Marking', ratePerUnit: 0, unit: 'script', effectiveFrom: '2026-08-01' })
  const [claim, setClaim] = useState({ claimantName: '', workType: 'Marking', units: 0, date: '2026-11-20', evidence: '' })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Health>('/api/payroll/hr/health').then(setHealth).catch(() => {}) }, [])
  useEffect(() => {
    api.get<Rate[]>('/api/payroll/rates').then(setRates).catch(e => setErr(e.message))
    api.get<Claim[]>('/api/payroll/claims').then(setClaims).catch(e => setErr(e.message))
  }, [reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const addRate = () => go(() => api.post('/api/payroll/rates', rate).then(() => setRate({ ...rate, ratePerUnit: 0 })))
  const createClaim = () => go(() => api.post('/api/payroll/claims', claim).then(() => setClaim({ ...claim, claimantName: '', units: 0 })))
  const act = (id: number, a: string) => go(() => api.post(`/api/payroll/claims/${id}/${a}`, {}))
  const money = (n: number) => n.toLocaleString(undefined, { style: 'currency', currency: 'JMD' })

  return (
    <div>
      <h2>Exam-Work Payments</h2>
      <div className="card"><b>HR integration:</b> {health ? `${health.provider}${health.external ? (health.reachable ? ' ✓' : ' (configured)') : ' (internal only)'}` : '…'}
        <p className="muted-text">Claims are computed from dated rates, approved by a second person, then exported to HR/Finance. Provider is pluggable.</p></div>
      {err && <p className="error">{err}</p>}

      <div className="row">
        <div className="card" style={{ flex: 1 }}>
          <h3>Rate table</h3>
          <div className="row">
            <label>Work <select value={rate.workType} onChange={e => setRate({ ...rate, workType: e.target.value })}>{TYPES.map(t => <option key={t}>{t}</option>)}</select></label>
            <label>Rate <input type="number" value={rate.ratePerUnit} onChange={e => setRate({ ...rate, ratePerUnit: +e.target.value })} style={{ width: 90 }} /></label>
            <label>Unit <input value={rate.unit} onChange={e => setRate({ ...rate, unit: e.target.value })} style={{ width: 80 }} /></label>
            <label>From <input type="date" value={rate.effectiveFrom} onChange={e => setRate({ ...rate, effectiveFrom: e.target.value })} /></label>
            <button onClick={addRate} style={{ alignSelf: 'flex-end' }}>+ Rate</button>
          </div>
          <table className="grid" style={{ marginTop: 10 }}>
            <thead><tr><th>Work</th><th>Rate</th><th>Unit</th><th>From</th></tr></thead>
            <tbody>{rates.map(r => <tr key={r.id}><td>{r.workType}</td><td>{money(r.ratePerUnit)}</td><td>{r.unit}</td><td>{r.effectiveFrom}</td></tr>)}
              {rates.length === 0 && <tr><td colSpan={4}>No rates.</td></tr>}</tbody>
          </table>
        </div>
        <div className="card" style={{ flex: 1 }}>
          <h3>New claim</h3>
          <label>Claimant <input value={claim.claimantName} onChange={e => setClaim({ ...claim, claimantName: e.target.value })} /></label>
          <label>Work <select value={claim.workType} onChange={e => setClaim({ ...claim, workType: e.target.value })}>{TYPES.map(t => <option key={t}>{t}</option>)}</select></label>
          <label>Units <input type="number" value={claim.units} onChange={e => setClaim({ ...claim, units: +e.target.value })} /></label>
          <label>Date <input type="date" value={claim.date} onChange={e => setClaim({ ...claim, date: e.target.value })} /></label>
          <label>Evidence <input value={claim.evidence} onChange={e => setClaim({ ...claim, evidence: e.target.value })} /></label>
          <button disabled={!claim.claimantName || claim.units <= 0} onClick={createClaim} style={{ marginTop: 10 }}>Create</button>
        </div>
      </div>

      <div className="card">
        <h3>Claims</h3>
        <table className="grid">
          <thead><tr><th>Claimant</th><th>Work</th><th>Units</th><th>Amount</th><th>Status</th><th>Actions</th></tr></thead>
          <tbody>
            {claims.map(c => (<tr key={c.id}>
              <td>{c.claimantName}</td><td>{c.workType}</td><td>{c.units}</td><td>{money(c.amount)}</td>
              <td>{c.status}{c.externalRef ? ` (${c.externalRef})` : ''}</td>
              <td className="actions">
                {c.status === 'Draft' && <button className="link" onClick={() => act(c.id, 'submit')}>Submit</button>}
                {c.status === 'Submitted' && <><button className="link" onClick={() => act(c.id, 'approve')}>Approve</button>
                  <button className="link danger" onClick={() => act(c.id, 'reject')}>Reject</button></>}
                {c.status === 'Approved' && <button className="link" onClick={() => act(c.id, 'export')}>Export to HR</button>}
              </td>
            </tr>))}
            {claims.length === 0 && <tr><td colSpan={6}>No claims.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
