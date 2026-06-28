import { lazy, Suspense } from 'react'
import type { ReactNode } from 'react'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import { ChatProvider } from './contexts/ChatContext'
import Footer from './components/Footer'
import ChatWidget from './components/ChatWidget'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'

const HomePage = lazy(() => import('./pages/HomePage'))
const LoginPage = lazy(() => import('./pages/LoginPage'))
const RegisterPage = lazy(() => import('./pages/RegisterPage'))
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('./pages/ResetPasswordPage'))
const DashboardPage = lazy(() => import('./pages/DashboardPage'))
const BrowsePage = lazy(() => import('./pages/BrowsePage'))
const PublicBarPage = lazy(() => import('./pages/PublicBarPage'))
const MarketplacePage = lazy(() => import('./pages/MarketplacePage'))
const OffersPage = lazy(() => import('./pages/OffersPage'))
const ProfilePage = lazy(() => import('./pages/ProfilePage'))

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000 } },
})

function RouteFallback() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <div className="text-muted-foreground">Loading...</div>
    </div>
  )
}

function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) return <RouteFallback />
  if (!isAuthenticated) return <Navigate to="/login" replace />

  return children
}

function AppRoutes() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) return <RouteFallback />

  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/browse" element={<BrowsePage />} />
      <Route path="/marketplace" element={<MarketplacePage />} />
      <Route path="/bar/:userId" element={<PublicBarPage />} />
      <Route path="/login" element={isAuthenticated ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route path="/register" element={isAuthenticated ? <Navigate to="/" replace /> : <RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <DashboardPage />
          </ProtectedRoute>
        }
      />
      <Route path="/messages" element={<Navigate to="/" replace />} />
      <Route
        path="/offers"
        element={
          <ProtectedRoute>
            <OffersPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/profile"
        element={
          <ProtectedRoute>
            <ProfilePage />
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
          <ChatProvider>
            <TooltipProvider>
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
              <div className="min-h-screen">
                <Suspense fallback={<RouteFallback />}>
                  <AppRoutes />
                </Suspense>
              </div>
              <ChatWidget />
              <Footer />
              <Toaster />
            </TooltipProvider>
          </ChatProvider>
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  )
}

export default App
