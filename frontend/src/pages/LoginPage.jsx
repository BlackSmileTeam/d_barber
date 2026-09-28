import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage } from '../context/ModalContext';

function TelegramIcon() {
  return (
    <svg viewBox="0 0 24 24" width="28" height="28" aria-hidden="true">
      <path
        fill="currentColor"
        d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8-1.55 7.31c-.12.52-.42.65-.86.4l-2.38-1.75-1.15 1.11c-.13.13-.23.23-.47.23l.17-2.42 4.4-3.97c.19-.17-.04-.27-.3-.1l-5.44 3.42-2.34-.73c-.51-.16-.52-.51.11-.75l9.14-3.52c.42-.16.79.1.67.67z"
      />
    </svg>
  );
}

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

  const openBot = async () => {
    try {
      const { data } = await api.get('/salon');
      if (data?.telegramUrl) window.open(data.telegramUrl, '_blank', 'noopener,noreferrer');
    } catch {
      /* ignore */
    }
  };

  const viaTelegram = async () => {
    const next = {};
    if (!phone.trim()) next.phone = 'Укажите телефон';
    setErrors(next);
    setTgHint('');
    if (Object.keys(next).length) return;

    setBusy(true);
    try {
      const { data } = await api.post('/auth/telegram-pass', { phone });
      if (data?.needTelegram) {
        await openBot();
        setTgHint('Напишите боту и поделитесь номером — пароль придёт в чат');
        return;
      }
      setTgHint('Пароль отправлен в Telegram');
      setErrors({ password: 'Введите пароль из Telegram' });
    } catch (err) {
      const msg = apiErrorMessage(err);
      if (/зарегистрируйтесь|имя/i.test(msg)) {
        setErrors({ phone: 'Сначала зарегистрируйтесь' });
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

        <div className="auth-social">
          <button
            type="button"
            className="auth-tg-icon"
            disabled={busy}
            onClick={viaTelegram}
            title="Войти через Telegram"
            aria-label="Войти через Telegram"
          >
            <TelegramIcon />
          </button>
        </div>
        {tgHint && <p className="auth-hint">{tgHint}</p>}

        <p className="auth-links">
          <Link to="/register">Регистрация</Link>
        </p>
      </div>
    </section>
  );
}
