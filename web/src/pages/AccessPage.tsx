import { useEffect, useState } from 'react'
import { api } from '../api'

type Me = { user: string; roles: string[]; permissions: string[] }
type Role = { name: string; description?: string; permissions: string[] }
type Assignment = { id: number; username: string; roleName: string; campusId?: number }

export default function AccessPage() {
  const [me, setMe] = useState<Me | null>(null)
  const [roles, setRoles] = useState<Role[]>([])
  const [assignments, setAssignments] = useState<Assignment[]>([])
  const [form, setForm] = useState({ username: '', roleName: '' })
  const [err, setErr] = useState<string | null>(null)
  const [canManage, setCanManage] = useState(false)
  const [reload, setReload] = useState(0)

  useEffect(() => { api.get<Me>('/api/access/me').then(m => { setMe(m); setCanManage(m.permissions.includes('admin.manage')) }).catch(e => setErr(e.message)) }, [])
  useEffect(() => {
    if (!canManage) return
    api.get<Role[]>('/api/access/roles').then(setRoles).catch(() => {})
    api.get<Assignment[]>('/api/access/assignments').then(setAssignments).catch(() => {})
  }, [canManage, reload])

  const go = async (fn: () => Promise<any>) => { setErr(null); try { await fn(); setReload(x => x + 1) } catch (e) { setErr((e as Error).message) } }
  const assign = () => go(() => api.post('/api/access/assignments', form).then(() => setForm({ username: '', roleName: '' })))
  const remove = (id: number) => go(() => api.del(`/api/access/assignments/${id}`))

  return (
    <div>
      <h2>Access &amp; Roles</h2>
      {err && <p className="error">{err}</p>}

      <div className="card">
        <h3>You</h3>
        {me ? <>
          <p><b>{me.user}</b> — roles: {me.roles.join(', ') || '(none)'}</p>
          <p className="muted-text">Permissions ({me.permissions.length}): {me.permissions.join(' · ')}</p>
        </> : '…'}
      </div>

      {!canManage ? <p className="muted-text">You need the <code>admin.manage</code> permission to manage roles.</p> : <>
        <div className="card">
          <h3>Roles</h3>
          <table className="grid">
            <thead><tr><th>Role</th><th>Permissions</th></tr></thead>
            <tbody>{roles.map(r => <tr key={r.name}><td><b>{r.name}</b></td><td className="muted-text">{r.permissions.join(', ')}</td></tr>)}</tbody>
          </table>
        </div>
        <div className="card">
          <h3>User role assignments</h3>
          <div className="row">
            <label>Username <input value={form.username} onChange={e => setForm({ ...form, username: e.target.value })} /></label>
            <label>Role <select value={form.roleName} onChange={e => setForm({ ...form, roleName: e.target.value })}>
              <option value="">— select —</option>{roles.map(r => <option key={r.name}>{r.name}</option>)}</select></label>
            <button disabled={!form.username || !form.roleName} onClick={assign} style={{ alignSelf: 'flex-end' }}>Assign</button>
          </div>
          <table className="grid" style={{ marginTop: 12 }}>
            <thead><tr><th>User</th><th>Role</th><th></th></tr></thead>
            <tbody>
              {assignments.map(a => <tr key={a.id}><td>{a.username}</td><td>{a.roleName}</td>
                <td><button className="link danger" onClick={() => remove(a.id)}>Remove</button></td></tr>)}
              {assignments.length === 0 && <tr><td colSpan={3}>No assignments (roles may also come from the identity provider).</td></tr>}
            </tbody>
          </table>
        </div>
      </>}
    </div>
  )
}
