import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route, Link, NavLink } from 'react-router-dom'
import StudentsPage from './pages/StudentsPage'
import StudentForm from './pages/StudentForm'
import GradeImportPage from './pages/GradeImportPage'
import MoodlePage from './pages/MoodlePage'
import './index.css'

function Shell() {
  return (
    <div className="app">
      <aside className="sidebar">
        <h1>SMS</h1>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/students">Students</NavLink>
          <NavLink to="/grade-import">Grade Import</NavLink>
          <a className="muted" title="Coming soon">Registration</a>
          <a className="muted" title="Coming soon">Examinations</a>
          <a className="muted" title="Coming soon">Documents</a>
          <NavLink to="/moodle">Moodle Sync</NavLink>
        </nav>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/students" element={<StudentsPage />} />
          <Route path="/students/new" element={<StudentForm />} />
          <Route path="/students/:id/edit" element={<StudentForm />} />
          <Route path="/grade-import" element={<GradeImportPage />} />
          <Route path="/moodle" element={<MoodlePage />} />
        </Routes>
      </main>
    </div>
  )
}

function Dashboard() {
  return (
    <div>
      <h2>Dashboard</h2>
      <p>Web-based Student Management System — Phase 1.</p>
      <ul>
        <li><Link to="/students">Students</Link> — full CRUD, paged &amp; searchable.</li>
        <li><Link to="/grade-import">Grade Import</Link> — Excel/CSV with column mapping, preview &amp; batch undo.</li>
        <li>Registration, Examinations, Documents and Moodle sync arrive in later phases.</li>
      </ul>
    </div>
  )
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode><BrowserRouter><Shell /></BrowserRouter></React.StrictMode>
)
