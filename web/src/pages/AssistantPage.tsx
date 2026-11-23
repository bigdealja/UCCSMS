import { useState } from 'react'
import { api } from '../api'

type Answer = { answer: string; sources: string[]; usedAi: boolean }

export default function AssistantPage() {
  const [question, setQuestion] = useState('')
  const [studentId, setStudentId] = useState('')
  const [ans, setAns] = useState<Answer | null>(null)
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  const ask = async () => {
    if (!question.trim()) return
    setBusy(true); setErr(null); setAns(null)
    try { setAns(await api.post<Answer>('/api/assistant/ask', { question, studentId: studentId ? +studentId : null })) }
    catch (e) { setErr((e as Error).message) } finally { setBusy(false) }
  }

  const examples = [
    'How do I pass a course?', 'How is my GPA calculated?', 'What is a re-sit?',
    'What are the admission categories?', 'Can I defer my start?', 'What is my account balance?'
  ]

  return (
    <div>
      <h2>Assistant</h2>
      <p className="muted-text">Ask about policies or your own records. Answers are grounded in the policy library
        and your record facts only — it never invents policy, grades or decisions.</p>

      <div className="card">
        <label>Your question
          <textarea value={question} onChange={e => setQuestion(e.target.value)} rows={2}
            style={{ width: '100%', padding: 10, border: '1px solid var(--line)', borderRadius: 8 }} /></label>
        <div className="row" style={{ marginTop: 10 }}>
          <label>Student ID (optional, for personal facts) <input value={studentId} onChange={e => setStudentId(e.target.value)} style={{ width: 160 }} /></label>
          <button disabled={busy || !question.trim()} onClick={ask} style={{ alignSelf: 'flex-end' }}>{busy ? 'Thinking…' : 'Ask'}</button>
        </div>
        <div className="row" style={{ marginTop: 8, gap: 8 }}>
          {examples.map(e => <button key={e} className="secondary" onClick={() => setQuestion(e)} style={{ fontSize: 12 }}>{e}</button>)}
        </div>
      </div>

      {err && <p className="error">{err}</p>}
      {ans && (
        <div className="card">
          <div style={{ whiteSpace: 'pre-wrap' }}>{ans.answer}</div>
          {ans.sources.length > 0 && <p className="muted-text" style={{ marginTop: 10 }}>Sources: {ans.sources.join(' · ')}</p>}
          <p className="muted-text">{ans.usedAi ? 'Answered with AI (grounded in the above).' : 'Assembled from the policy library and records (AI disabled).'}</p>
        </div>
      )}
    </div>
  )
}
