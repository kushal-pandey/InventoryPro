import { lazy } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import './App.css'
import { AuthProvider } from './auth/AuthProvider'
import { RequireAuth } from './auth/RequireAuth'
import { RequireAdmin } from './auth/RequireAdmin'
import { Layout } from './components/Layout'
import { ThemeProvider } from './components/ThemeProvider'
import { ToastProvider } from './components/ToastProvider'
import LoginPage from './pages/LoginPage'

// Route-level code splitting keeps the first load small (the charting library only loads with the dashboard).
const DashboardPage = lazy(() => import('./pages/DashboardPage'))
const ReorderPlannerPage = lazy(() => import('./pages/ReorderPlannerPage'))
const ProductsPage = lazy(() => import('./pages/ProductsPage'))
const OrdersPage = lazy(() => import('./pages/OrdersPage'))
const CustomersPage = lazy(() => import('./pages/CustomersPage'))
const CategoriesPage = lazy(() => import('./pages/CategoriesPage'))
const TeamPage = lazy(() => import('./pages/TeamPage'))
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'))

export default function App() {
  return (
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <ToastProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<RequireAuth />}>
                <Route element={<Layout />}>
                  <Route index element={<DashboardPage />} />
                  <Route path="reorder" element={<ReorderPlannerPage />} />
                  <Route path="products" element={<ProductsPage />} />
                  <Route path="orders" element={<OrdersPage />} />
                  <Route path="customers" element={<CustomersPage />} />
                  <Route path="categories" element={<CategoriesPage />} />
                  <Route element={<RequireAdmin />}>
                    <Route path="team" element={<TeamPage />} />
                  </Route>
                  <Route path="*" element={<NotFoundPage />} />
                </Route>
              </Route>
            </Routes>
          </ToastProvider>
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  )
}
