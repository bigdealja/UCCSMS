import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route, Link, NavLink } from 'react-router-dom'
import StudentsPage from './pages/StudentsPage'
import AdmissionsPage from './pages/AdmissionsPage'
import StudentForm from './pages/StudentForm'
import GradeImportPage from './pages/GradeImportPage'
import MoodlePage from './pages/MoodlePage'
import ExamsPage from './pages/ExamsPage'
import ExamSchedulingPage from './pages/ExamSchedulingPage'
import CasesPage from './pages/CasesPage'
import DocumentsPage from './pages/DocumentsPage'
import FinancePage from './pages/FinancePage'
import GradingPage from './pages/GradingPage'
import './index.css'

function Shell() {
  return (
    <div className="app">
      <aside className="sidebar">
        <h1>SMS</h1>
        <nav>
          <NavLink to="/" end>Dashboard</NavLink>
          <NavLink to="/admissions">Admissions &amp; CRM</NavLink>
          <NavLink to="/students">Students</NavLink>
          <NavLink to="/grade-import">Grade Import</NavLink>
          <a className="muted" title="Coming soon">Registration</a>
          <NavLink to="/exams">Examinations</NavLink>
          <NavLink to="/exam-scheduling">Exam Scheduling</NavLink>
          <NavLink to="/grading">Grade Schemes</NavLink>
          <NavLink to="/cases">Cases</NavLink>
          <NavLink to="/documents">Documents</NavLink>
          <NavLink to="/finance">Finance</NavLink>
          <NavLink to="/moodle">Moodle Sync</NavLink>
        </nav>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/admissions" element={<AdmissionsPage />} />
          <Route path="/students" element={<StudentsPage />} />
          <Route path="/students/new" element={<StudentForm />} />
          <Route path="/students/:id/edit" element={<StudentForm />} />
          <Route path="/grade-import" element={<GradeImportPage />} />
          <Route path="/moodle" element={<MoodlePage />} />
          <Route path="/exams" element={<ExamsPage />} />
          <Route path="/exam-scheduling" element={<ExamSchedulingPage />} />
          <Route path="/grading" element={<GradingPage />} />
          <Route path="/cases" element={<CasesPage />} />
          <Route path="/documents" element={<DocumentsPage />} />
          <Route path="/finance" element={<FinancePage />} />
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
