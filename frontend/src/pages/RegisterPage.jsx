import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage } from '../context/ModalContext';
import TelegramLoginWidget from '../components/TelegramLoginWidget';

function digitsPhone(value) {
  return (value || '').replace(/\D/g, '');
}

function validatePhone(value) {
  const d = digitsPhone(value);
  if (!d) return 'Укажите телефон';
  if (d.length < 10 || d.length > 12) return 'Введите номер полностью, например +7…';
  return '';
}

function validateName(value) {
  const v = (value || '').trim();
  if (!v) return 'Укажите имя';
  if (v.length < 2) return 'Слишком короткое имя';
  return '';
}

function validatePassword(value) {
  if (!value) return 'Укажите пароль';
  if (value.length < 6) return 'Минимум 6 символов';
  return '';
}

export default function RegisterPage() {
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState({});
  const [busy, setBusy] = useState(false);
  const [tgHint, setTgHint] = useState('');
  const { setAuth } = useAuth();
  const navigate = useNavigate();

  const clearField = (field) => setErrors((e) => {
    if (!e[field]) return e;
    const next = { ...e };
    delete next[field];
    return next;
  });

  const mapApiError = (err) => {
    const msg = apiErrorMessage(err);
    const status = err?.response?.status;
    if (status === 409 || /телефон|зарегистрирован|уже есть/i.test(msg)) {
      return { phone: msg };
    }
    if (/имя/i.test(msg)) return { name: msg };
    if (/парол/i.test(msg)) return { password: msg };
    return { phone: msg };
  };

  const onTelegramAuth = useCallback((data) => {
    setAuth(data);
    navigate('/cabinet');
  }, [setAuth, navigate]);

  const onTelegramError = useCallback((message) => {
    setTgHint(message || 'Не удалось войти через Telegram');
  }, []);

  const submit = async (e) => {
    e.preventDefault();
    setTgHint('');
    const next = {
      name: validateName(name),
      phone: validatePhone(phone),
      password: validatePassword(password),
    };
    Object.keys(next).forEach((k) => { if (!next[k]) delete next[k]; });
    setErrors(next);
    if (Object.keys(next).length) return;

    setBusy(true);
    try {
      const { data } = await api.post('/auth/register', { name, phone, password });
      setAuth(data);
      navigate('/cabinet');
    } catch (err) {
      setErrors(mapApiError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="section auth-page">
      <div className="auth-card">
        <h2>Регистрация</h2>
        <TelegramLoginWidget
          disabled={busy}
          onAuth={onTelegramAuth}
          onError={onTelegramError}
        />
        {tgHint && <p className="auth-hint">{tgHint}</p>}
        <div className="auth-divider" aria-hidden="true" />
        <form className="form" onSubmit={submit} noValidate>
          <label className={errors.name ? 'has-error' : undefined}>
            Имя
            <input
              value={name}
              onChange={(e) => { setName(e.target.value); clearField('name'); }}
              autoComplete="name"
            />
            {errors.name && <span className="field-error">{errors.name}</span>}
          </label>
          <label className={errors.phone ? 'has-error' : undefined}>
            Телефон
            <input
              value={phone}
              onChange={(e) => { setPhone(e.target.value); clearField('phone'); }}
              placeholder="+7..."
              autoComplete="tel"
            />
            {errors.phone && <span className="field-error">{errors.phone}</span>}
          </label>
          <label className={errors.password ? 'has-error' : undefined}>
            Пароль
            <input
              type="password"
              value={password}
              onChange={(e) => { setPassword(e.target.value); clearField('password'); }}
              autoComplete="new-password"
            />
            {errors.password && <span className="field-error">{errors.password}</span>}
          </label>
          <button className="btn btn-primary" type="submit" disabled={busy}>Создать аккаунт</button>
        </form>
        <p className="auth-links">
          <Link to="/login">Вход</Link>
        </p>
      </div>
    </section>
  );
}
