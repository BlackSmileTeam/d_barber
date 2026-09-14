import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage, useModal } from '../context/ModalContext';

export default function LoginPage() {
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const { setAuth } = useAuth();
  const { show } = useModal();
  const navigate = useNavigate();

  const submit = async (e) => {
    e.preventDefault();
    try {
      const { data } = await api.post('/auth/login', { phone, password });
      setAuth(data);
      show({
        title: 'Вход выполнен',
        message: `Добро пожаловать, ${data.name}`,
        actions: [{ label: 'В кабинет', primary: true, onClick: () => navigate('/cabinet') }],
      });
    } catch (err) {
      show({ title: 'Ошибка входа', message: apiErrorMessage(err) });
    }
  };

  return (
    <section className="section auth-page">
      <div className="auth-card">
        <h2>Вход</h2>
        <form className="form" onSubmit={submit}>
          <label>Телефон<input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+7..." required /></label>
          <label>Пароль<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={6} /></label>
          <button className="btn btn-primary" type="submit">Войти</button>
        </form>
        <p className="auth-links">
          <Link to="/register">Регистрация</Link>
          {' · '}
          <Link to="/admin/login">Вход для админа</Link>
        </p>
      </div>
    </section>
  );
}
