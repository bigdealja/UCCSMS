import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route, Link, NavLink } from 'react-router-dom'
import StudentsPage from './pages/StudentsPage'
import AdmissionsPage from './pages/AdmissionsPage'
import DashboardPage from './pages/DashboardPage'
import AssistantPage from './pages/AssistantPage'
import AccessPage from './pages/AccessPage'
import AuditPage from './pages/AuditPage'
import StudentForm from './pages/StudentForm'
import AdvisementPage from './pages/AdvisementPage'
import StudentServicesPage from './pages/StudentServicesPage'
import GradeImportPage from './pages/GradeImportPage'
import MoodlePage from './pages/MoodlePage'
import ExamsPage from './pages/ExamsPage'
import ExamSchedulingPage from './pages/ExamSchedulingPage'
import ExaminersPage from './pages/ExaminersPage'
import QuestionPapersPage from './pages/QuestionPapersPage'
import ScriptsPage from './pages/ScriptsPage'
import AssessmentOpsPage from './pages/AssessmentOpsPage'
import CasesPage from './pages/CasesPage'
import DocumentsPage from './pages/DocumentsPage'
import FinancePage from './pages/FinancePage'
import PayrollPage from './pages/PayrollPage'
import GradingPage from './pages/GradingPage'
import GradebookPage from './pages/GradebookPage'
import GradeGovernancePage from './pages/GradeGovernancePage'
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
          <NavLink to="/advisement">Advisement</NavLink>
          <NavLink to="/gradebook">Gradebook</NavLink>
          <NavLink to="/grade-import">Grade Import</NavLink>
          <a className="muted" title="Coming soon">Registration</a>
          <NavLink to="/exams">Examinations</NavLink>
          <NavLink to="/exam-scheduling">Exam Scheduling</NavLink>
          <NavLink to="/examiners">External Examiners</NavLink>
          <NavLink to="/question-papers">Question Papers</NavLink>
          <NavLink to="/scripts">Scripts &amp; Custody</NavLink>
          <NavLink to="/assessment-ops">Assessment Ops</NavLink>
          <NavLink to="/grading">Grade Schemes</NavLink>
          <NavLink to="/grade-governance">Grade Governance</NavLink>
          <NavLink to="/assistant">Assistant</NavLink>
          <NavLink to="/access">Access &amp; Roles</NavLink>
          <NavLink to="/audit">Audit Trail</NavLink>
          <NavLink to="/cases">Cases</NavLink>
          <NavLink to="/documents">Documents</NavLink>
          <NavLink to="/student-services">Student Services</NavLink>
          <NavLink to="/finance">Finance</NavLink>
          <NavLink to="/payroll">Exam-Work Pay</NavLink>
          <NavLink to="/moodle">Moodle Sync</NavLink>
        </nav>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/admissions" element={<AdmissionsPage />} />
          <Route path="/students" element={<StudentsPage />} />
          <Route path="/students/new" element={<StudentForm />} />
          <Route path="/students/:id/edit" element={<StudentForm />} />
          <Route path="/advisement" element={<AdvisementPage />} />
          <Route path="/gradebook" element={<GradebookPage />} />
          <Route path="/grade-import" element={<GradeImportPage />} />
          <Route path="/moodle" element={<MoodlePage />} />
          <Route path="/exams" element={<ExamsPage />} />
          <Route path="/exam-scheduling" element={<ExamSchedulingPage />} />
          <Route path="/examiners" element={<ExaminersPage />} />
          <Route path="/question-papers" element={<QuestionPapersPage />} />
          <Route path="/scripts" element={<ScriptsPage />} />
          <Route path="/assessment-ops" element={<AssessmentOpsPage />} />
          <Route path="/grading" element={<GradingPage />} />
          <Route path="/grade-governance" element={<GradeGovernancePage />} />
          <Route path="/assistant" element={<AssistantPage />} />
          <Route path="/access" element={<AccessPage />} />
          <Route path="/audit" element={<AuditPage />} />
          <Route path="/cases" element={<CasesPage />} />
          <Route path="/documents" element={<DocumentsPage />} />
          <Route path="/student-services" element={<StudentServicesPage />} />
          <Route path="/finance" element={<FinancePage />} />
          <Route path="/payroll" element={<PayrollPage />} />
        </Routes>
      </main>
    </div>
  )
}


ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode><BrowserRouter><Shell /></BrowserRouter></React.StrictMode>
)
