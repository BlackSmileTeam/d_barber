import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
      show({
        title: 'Админ-вход',
        message: 'Добро пожаловать в панель D_Barber',
        actions: [{ label: 'Открыть админку', primary: true, onClick: () => navigate('/admin') }],
      });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  return (
    <section className="section">
      <div className="container">
        <h2>Вход администратора</h2>
        <form className="form" onSubmit={submit}>
          <label>Логин<input value={login} onChange={(e) => setLogin(e.target.value)} required /></label>
          <label>Пароль<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required /></label>
          <button className="btn btn-primary" type="submit">Войти</button>
        </form>
      </div>
    </section>
  );
}
