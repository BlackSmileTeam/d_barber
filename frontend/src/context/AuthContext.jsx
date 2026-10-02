import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getAuth, onUnauthorized, setAuth as persistAuth } from '../services/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [auth, setAuthState] = useState(() => getAuth());

  const setAuth = (value) => {
    persistAuth(value);
    setAuthState(value);
  };

  const logout = () => setAuth(null);

  const value = useMemo(() => ({ auth, setAuth, logout }), [auth]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** Must render inside BrowserRouter. Syncs 401 → clear auth + redirect. */
export function AuthSessionWatcher() {
  const { setAuth } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    onUnauthorized(() => {
      setAuth(null);
      const path = window.location.pathname;
      if (path.startsWith('/admin') && path !== '/admin/login') {
        navigate('/admin/login', { replace: true });
      } else if (path === '/cabinet') {
        navigate('/login', { replace: true });
      }
    });
    return () => onUnauthorized(null);
  }, [setAuth, navigate]);

  return null;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth outside provider');
  return ctx;
}
