import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import HomePage from './pages/HomePage'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import BrowsePage from './pages/BrowsePage'
import PublicBarPage from './pages/PublicBarPage'
import MarketplacePage from './pages/MarketplacePage'
import Footer from './components/Footer'

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000 } },
})

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-stone-900">
        <div className="text-stone-300">Loading...</div>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return children
}

function AppRoutes() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-stone-900">
        <div className="text-stone-300">Loading...</div>
      </div>
    )
  }

  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/browse" element={<BrowsePage />} />
      <Route path="/marketplace" element={<MarketplacePage />} />
      <Route path="/bar/:userId" element={<PublicBarPage />} />
      <Route path="/login" element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route path="/register" element={isAuthenticated ? <Navigate to="/" replace /> : <RegisterPage />} />
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <DashboardPage />
          </ProtectedRoute>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <img
            src="/bg-room.png"
            alt=""
            aria-hidden="true"
            style={{
              position: 'fixed',
              top: 0, left: 0,
              width: '100%', height: '100%',
              objectFit: 'cover',
              objectPosition: 'center center',
              zIndex: -2,
              pointerEvents: 'none',
            }}
          />
          <div style={{
            position: 'fixed',
            top: 0, left: 0,
            width: '100%', height: '100%',
            background: 'rgba(4, 2, 1, 0.38)',
            zIndex: -1,
            pointerEvents: 'none',
          }} />
          <div style={{ minHeight: '100vh' }}>
            <AppRoutes />
          </div>
          <Footer />
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  )
}

export default App
