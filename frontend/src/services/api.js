import axios from 'axios';

const api = axios.create({
  // Empty VITE_API_URL in Docker → same-origin /api (nginx proxy). Dev: Vite proxies /api.
  baseURL: import.meta.env.VITE_API_URL || '/api',
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('dbarber_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let unauthorizedHandler = null;
let handlingUnauthorized = false;

/** Register a single handler for API 401 (clears React auth + redirects). */
export function onUnauthorized(handler) {
  unauthorizedHandler = handler;
}

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status;
    const url = String(error.config?.url || '');
    const isAuthEndpoint = /\/auth\//.test(url);

    if (status === 401 && !isAuthEndpoint && localStorage.getItem('dbarber_token')) {
      if (!handlingUnauthorized) {
        handlingUnauthorized = true;
        setAuth(null);
        try {
          unauthorizedHandler?.();
        } finally {
          queueMicrotask(() => {
            handlingUnauthorized = false;
          });
        }
      }
    }

    return Promise.reject(error);
  },
);

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

/** Resolve uploaded or absolute media URLs for <img src>. */
export function mediaUrl(path) {
  if (!path) return '';
  if (/^https?:\/\//i.test(path)) return path;
  return path.startsWith('/') ? path : `/${path}`;
}

export default api;
