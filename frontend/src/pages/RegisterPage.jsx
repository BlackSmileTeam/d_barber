import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage, useModal } from '../context/ModalContext';

export default function RegisterPage() {
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const { setAuth } = useAuth();
  const { show } = useModal();
  const navigate = useNavigate();

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true);
    try {
      const { data } = await api.post('/auth/register', { name, phone, password });
      setAuth(data);
      navigate('/cabinet');
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
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
    if (!name.trim()) {
      show({ title: 'Имя', message: 'Укажите имя' });
      return;
    }
    if (!phone.trim()) {
      show({ title: 'Телефон', message: 'Укажите номер телефона' });
      return;
    }
    setBusy(true);
    try {
      const { data } = await api.post('/auth/telegram-pass', { phone, name: name.trim() });
      if (data?.needTelegram) {
        await openBot();
        show({
          title: 'Telegram',
          message: 'Напишите боту и поделитесь номером — пароль придёт в чат',
          actions: [{ label: 'Войти', primary: true, onClick: () => navigate('/login') }],
        });
        return;
      }
      show({
        title: 'Telegram',
        message: 'Проверьте сообщения от бота',
        actions: [{ label: 'Войти', primary: true, onClick: () => navigate('/login') }],
      });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="section auth-page">
      <div className="auth-card">
        <h2>Регистрация</h2>
        <form className="form" onSubmit={submit}>
          <label>Имя<input value={name} onChange={(e) => setName(e.target.value)} required /></label>
          <label>Телефон<input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+7..." required /></label>
          <label>Пароль<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={6} /></label>
          <button className="btn btn-primary" type="submit" disabled={busy}>Создать аккаунт</button>
        </form>
        <div className="auth-divider" aria-hidden="true" />
        <button className="btn btn-telegram" type="button" disabled={busy} onClick={viaTelegram}>
          Войти через Telegram
        </button>
        <p className="auth-links">
          <Link to="/login">Вход</Link>
        </p>
      </div>
    </section>
  );
}
