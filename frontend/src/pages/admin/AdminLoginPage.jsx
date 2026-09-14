import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../../services/api';
import { useAuth } from '../../context/AuthContext';
import { apiErrorMessage, useModal } from '../../context/ModalContext';

export default function AdminLoginPage() {
  const [login, setLogin] = useState('admin');
  const [password, setPassword] = useState('');
  const { setAuth } = useAuth();
  const { show } = useModal();
  const navigate = useNavigate();

  const submit = async (e) => {
    e.preventDefault();
    try {
      const { data } = await api.post('/auth/admin/login', { login, password });
      setAuth(data);
      navigate('/admin');
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  return (
    <section className="section auth-page">
      <div className="auth-card">
        <h2>Вход администратора</h2>
        <form className="form" onSubmit={submit}>
          <label>Логин<input value={login} onChange={(e) => setLogin(e.target.value)} required /></label>
          <label>Пароль<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required /></label>
          <button className="btn btn-primary" type="submit">Войти</button>
        </form>
        <p className="auth-links">
          <Link to="/login">Вход для клиента</Link>
        </p>
      </div>
    </section>
  );
}
