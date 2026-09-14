import { createContext, useContext, useMemo, useState } from 'react';
import { getAuth, setAuth as persistAuth } from '../services/api';

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

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth outside provider');
  return ctx;
}
