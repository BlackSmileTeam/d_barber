import { BrowserRouter, Route, Routes } from 'react-router-dom';
import Header from './components/Header';
import { AuthProvider } from './context/AuthContext';
import { ModalProvider } from './context/ModalContext';
import LandingPage from './pages/LandingPage';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import CabinetPage from './pages/CabinetPage';
import AdminLoginPage from './pages/admin/AdminLoginPage';
import AdminPage from './pages/admin/AdminPage';

export default function App() {
  return (
    <AuthProvider>
      <ModalProvider>
        <BrowserRouter>
          <div className="app-shell">
            <Header />
            <Routes>
              <Route path="/" element={<LandingPage />} />
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/cabinet" element={<CabinetPage />} />
              <Route path="/admin/login" element={<AdminLoginPage />} />
              <Route path="/admin" element={<AdminPage />} />
              <Route path="*" element={<LandingPage />} />
            </Routes>
            <footer className="site-footer">
              <div className="container">Санкт-Петербург</div>
            </footer>
          </div>
        </BrowserRouter>
      </ModalProvider>
    </AuthProvider>
  );
}
