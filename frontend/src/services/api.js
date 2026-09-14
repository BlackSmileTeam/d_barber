import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5271/api',
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('dbarber_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export function setAuth(auth) {
  if (auth?.token) {
    localStorage.setItem('dbarber_token', auth.token);
    localStorage.setItem('dbarber_auth', JSON.stringify(auth));
  } else {
    localStorage.removeItem('dbarber_token');
    localStorage.removeItem('dbarber_auth');
  }
}

export function getAuth() {
  try {
    return JSON.parse(localStorage.getItem('dbarber_auth') || 'null');
  } catch {
    return null;
  }
}

export default api;
