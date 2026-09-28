import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage } from '../context/ModalContext';
import TelegramLoginWidget from '../components/TelegramLoginWidget';

export default function LoginPage() {
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

  const onTelegramAuth = useCallback((data) => {
    setAuth(data);
    navigate('/cabinet');
  }, [setAuth, navigate]);

  const onTelegramError = useCallback((message) => {
    setTgHint(message || 'Не удалось войти через Telegram');
  }, []);

  const submit = async (e) => {
    e.preventDefault();
    const next = {};
    if (!phone.trim()) next.phone = 'Укажите телефон';
    if (!password) next.password = 'Укажите пароль';
    else if (password.length < 6) next.password = 'Минимум 6 символов';
    setErrors(next);
    setTgHint('');
    if (Object.keys(next).length) return;

    setBusy(true);
    try {
      const { data } = await api.post('/auth/login', { phone, password });
      setAuth(data);
      navigate('/cabinet');
    } catch (err) {
      const msg = apiErrorMessage(err);
      const codeSent = err?.response?.data?.codeSentToTelegram;
      if (codeSent) {
        setTgHint('Пароль отправлен в Telegram');
        setErrors({ password: 'Введите пароль из Telegram' });
      } else if (/телефон|пароль|неверн/i.test(msg)) {
        setErrors({ phone: msg, password: msg });
      } else {
        setErrors({ phone: msg });
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="section auth-page">
      <div className="auth-card">
        <h2>Вход</h2>
        <TelegramLoginWidget
          disabled={busy}
          onAuth={onTelegramAuth}
          onError={onTelegramError}
        />
        {tgHint && <p className="auth-hint">{tgHint}</p>}
        <div className="auth-divider" aria-hidden="true" />
        <form className="form" onSubmit={submit} noValidate>
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
              autoComplete="current-password"
            />
            {errors.password && <span className="field-error">{errors.password}</span>}
          </label>
          <button className="btn btn-primary" type="submit" disabled={busy}>Войти</button>
        </form>

        <p className="auth-links">
          <Link to="/register">Регистрация</Link>
        </p>
      </div>
    </section>
  );
}
