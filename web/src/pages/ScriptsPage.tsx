import { useEffect, useState } from 'react'
import { api } from '../api'

type Bundle = { id: number; code: string; examId: number; scriptCount: number; status: string; custodian?: string; retentionUntil?: string; disposalWitness?: string }
type Chain = { action: string; fromCustodian?: string; toCustodian?: string; by?: string; notes?: string; createdUtc: string }

export default function ScriptsPage() {
  const [bundles, setBundles] = useState<Bundle[]>([])
  const [form, setForm] = useState({ examId: 50, code: '', scriptCount: 40, custodian: '' })
  const [chain, setChain] = useState<Chain[] | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Bundle[]>('/api/scripts').then(setBundles).catch(e => setErr(e.message)) }, [reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const create = () => go(() => api.post('/api/scripts', form).then(() => setForm({ ...form, code: '', custodian: '' })))
  const handover = (id: number) => { const to = prompt('Hand over to?'); if (to) go(() => api.post(`/api/scripts/${id}/handover`, { to })) }
  const scan = (id: number) => { const c = prompt('Collection ref?'); go(() => api.post(`/api/scripts/${id}/scan`, { scannedCount: 40, collectionRef: c })) }
  const issue = (id: number) => { const m = prompt('Issue to marker?'); if (m) go(() => api.post(`/api/scripts/${id}/issue`, { marker: m })) }
  const marked = (id: number) => go(() => api.post(`/api/scripts/${id}/marked`, { fileName: 'marked.pdf' }))
  const retention = (id: number) => { const d = prompt('Retain until (YYYY-MM-DD)?'); if (d) go(() => api.post(`/api/scripts/${id}/retention`, { until: d })) }
  const dispose = (id: number) => { const w = prompt('Disposal witness?'); if (w) go(() => api.post(`/api/scripts/${id}/dispose`, { witness: w })) }
  const viewChain = (id: number) => { setSelected(id); api.get<Chain[]>(`/api/scripts/${id}/chain`).then(setChain).catch(e => setErr(e.message)) }

  return (
    <div>
      <h2>Scripts &amp; Custody</h2>
      <p className="muted-text">Script bundles with a full chain of custody: collect → hand over → scan → issue to markers → marked uploads → retain → witnessed disposal.</p>
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>Create a bundle</h3>
        <div className="row">
          <label>Exam ID <input type="number" value={form.examId} onChange={e => setForm({ ...form, examId: +e.target.value })} style={{ width: 90 }} /></label>
          <label>Code <input value={form.code} onChange={e => setForm({ ...form, code: e.target.value })} /></label>
          <label>Scripts <input type="number" value={form.scriptCount} onChange={e => setForm({ ...form, scriptCount: +e.target.value })} style={{ width: 90 }} /></label>
          <label>Custodian <input value={form.custodian} onChange={e => setForm({ ...form, custodian: e.target.value })} /></label>
          <button disabled={!form.code || !form.custodian} onClick={create} style={{ alignSelf: 'flex-end' }}>Create</button>
        </div>
      </div>

      <div className="card">
        <h3>Bundles</h3>
        <table className="grid">
          <thead><tr><th>Code</th><th>Scripts</th><th>Status</th><th>Custodian</th><th>Retention</th><th>Actions</th></tr></thead>
          <tbody>
            {bundles.map(b => (<tr key={b.id} className={b.status === 'Disposed' ? '' : ''}>
              <td>{b.code}</td><td>{b.scriptCount}</td><td>{b.status}</td><td>{b.custodian ?? '—'}</td><td>{b.retentionUntil ?? '—'}</td>
              <td className="actions">
                <button className="link" onClick={() => viewChain(b.id)}>Chain</button>
                {b.status !== 'Disposed' && <>
                  <button className="link" onClick={() => handover(b.id)}>Handover</button>
                  <button className="link" onClick={() => scan(b.id)}>Scan</button>
                  <button className="link" onClick={() => issue(b.id)}>Issue</button>
                  <button className="link" onClick={() => marked(b.id)}>Marked</button>
                  <button className="link" onClick={() => retention(b.id)}>Retain</button>
                  <button className="link danger" onClick={() => dispose(b.id)}>Dispose</button></>}
              </td>
            </tr>))}
            {bundles.length === 0 && <tr><td colSpan={6}>No bundles yet.</td></tr>}
          </tbody>
        </table>
      </div>

      {selected && chain && (
        <div className="card">
          <h3>Chain of custody — bundle #{selected}</h3>
          <table className="grid">
            <thead><tr><th>When</th><th>Action</th><th>From → To</th><th>By</th><th>Notes</th></tr></thead>
            <tbody>{chain.map((e, i) => <tr key={i}>
              <td>{new Date(e.createdUtc).toLocaleString()}</td><td>{e.action}</td>
              <td>{e.fromCustodian ?? '—'} → {e.toCustodian ?? '—'}</td><td>{e.by}</td><td>{e.notes}</td></tr>)}</tbody>
          </table>
        </div>
      )}
    </div>
  )
}
