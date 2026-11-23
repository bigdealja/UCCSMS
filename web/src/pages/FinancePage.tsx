import { useEffect, useState } from 'react'
import { api } from '../api'

type FeeType = { id: number; code: string; name: string; defaultAmount: number; distributionCode?: string }
type Line = { id: number; date: string; type: string; description?: string; amount: number; runningBalance: number; accountingStatus: string; accountingRef?: string }
type Statement = { studentId: number; balance: number; lines: Line[] }
type Health = { provider: string; external: boolean; reachable?: boolean | null }
const METHODS = ['Cash','Card','BankTransfer','Cheque','Scholarship','Waiver','Other']

export default function FinancePage() {
  const [health, setHealth] = useState<Health | null>(null)
  const [fees, setFees] = useState<FeeType[]>([])
  const [studentId, setStudentId] = useState('1')
  const [stmt, setStmt] = useState<Statement | null>(null)
  const [charge, setCharge] = useState({ feeTypeId: 0, amount: 0, description: '' })
  const [pay, setPay] = useState({ amount: 0, method: 'Card', reference: '' })
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Health>('/api/finance/accounting/health').then(setHealth).catch(() => {}) }, [])
  useEffect(() => { api.get<FeeType[]>('/api/finance/fee-types').then(setFees).catch(() => {}) }, [])
  const load = () => { if (studentId) api.get<Statement>(`/api/finance/students/${studentId}/statement`).then(setStmt).catch(e => setErr(e.message)) }
  useEffect(load, [studentId, reload])

  const addCharge = async () => {
    setErr(null)
    try { await api.post(`/api/finance/students/${studentId}/charges`, charge); setReload(x => x + 1) }
    catch (e) { setErr((e as Error).message) }
  }
  const addPayment = async () => {
    setErr(null)
    try { await api.post(`/api/finance/students/${studentId}/payments`, pay); setReload(x => x + 1) }
    catch (e) { setErr((e as Error).message) }
  }
  const money = (n: number) => n.toLocaleString(undefined, { style: 'currency', currency: 'JMD' })

  return (
    <div>
      <h2>Student Finance</h2>
      <div className="card">
        <b>Accounting integration:</b>{' '}
        {health ? <span>{health.provider}{health.external ? (health.reachable ? ' ✓ reachable' : ' ✗ unreachable') : ' (internal ledger only)'}</span> : '…'}
        <p className="muted-text">Provider is pluggable — switch to QuickBooks, Xero, Sage 300, etc. via config without changing finance logic.</p>
      </div>

      <div className="card">
        <label>Student ID <input value={studentId} onChange={e => setStudentId(e.target.value)} style={{ width: 120 }} /></label>
        {stmt && <h3 style={{ marginTop: 12 }}>Balance: {money(stmt.balance)}</h3>}
      </div>

      {err && <p className="error">{err}</p>}

      <div className="row">
        <div className="card" style={{ flex: 1 }}>
          <h3>Post a charge</h3>
          <label>Fee type <select value={charge.feeTypeId} onChange={e => { const f = fees.find(x => x.id === +e.target.value); setCharge({ ...charge, feeTypeId: +e.target.value, amount: f?.defaultAmount ?? charge.amount, description: f?.name ?? '' }) }}>
            <option value={0}>— none —</option>{fees.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
          <label>Amount <input type="number" value={charge.amount} onChange={e => setCharge({ ...charge, amount: +e.target.value })} /></label>
          <label>Description <input value={charge.description} onChange={e => setCharge({ ...charge, description: e.target.value })} /></label>
          <button onClick={addCharge} style={{ marginTop: 10 }}>Post charge</button>
        </div>
        <div className="card" style={{ flex: 1 }}>
          <h3>Record a payment</h3>
          <label>Amount <input type="number" value={pay.amount} onChange={e => setPay({ ...pay, amount: +e.target.value })} /></label>
          <label>Method <select value={pay.method} onChange={e => setPay({ ...pay, method: e.target.value })}>{METHODS.map(m => <option key={m}>{m}</option>)}</select></label>
          <label>Reference <input value={pay.reference} onChange={e => setPay({ ...pay, reference: e.target.value })} /></label>
          <button onClick={addPayment} style={{ marginTop: 10 }}>Record payment</button>
        </div>
      </div>

      <div className="card">
        <h3>Statement</h3>
        <table className="grid">
          <thead><tr><th>Date</th><th>Type</th><th>Description</th><th>Amount</th><th>Balance</th><th>Accounting</th></tr></thead>
          <tbody>
            {stmt?.lines.map(l => (
              <tr key={l.id}>
                <td>{new Date(l.date).toLocaleDateString()}</td><td>{l.type}</td><td>{l.description ?? '—'}</td>
                <td>{money(l.amount)}</td><td>{money(l.runningBalance)}</td>
                <td>{l.accountingStatus}{l.accountingRef ? ` (${l.accountingRef})` : ''}</td>
              </tr>
            ))}
            {stmt && stmt.lines.length === 0 && <tr><td colSpan={6}>No transactions.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  )
}
