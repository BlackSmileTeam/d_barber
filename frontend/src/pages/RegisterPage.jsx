import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage, useModal } from '../context/ModalContext';

export default function RegisterPage() {
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const { setAuth } = useAuth();
  const { show } = useModal();
  const navigate = useNavigate();

  const submit = async (e) => {
    e.preventDefault();
    try {
      const { data } = await api.post('/auth/register', { name, phone, password });
      setAuth(data);
      show({
        title: 'Регистрация успешна',
        message: 'Аккаунт создан. Можно записываться на услуги.',
        actions: [{ label: 'К записи', primary: true, onClick: () => navigate('/#book') }],
      });
    } catch (err) {
      show({ title: 'Ошибка регистрации', message: apiErrorMessage(err) });
    }
  };

  return (
    <section className="section">
      <div className="container">
        <h2>Регистрация</h2>
        <form className="form" onSubmit={submit}>
          <label>Имя<input value={name} onChange={(e) => setName(e.target.value)} required /></label>
          <label>Телефон<input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+7..." required /></label>
          <label>Пароль<input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={6} /></label>
          <button className="btn btn-primary" type="submit">Создать аккаунт</button>
        </form>
        <p className="lead" style={{ marginTop: '1rem' }}>Уже есть аккаунт? <Link to="/login">Войти</Link></p>
      </div>
    </section>
  );
}
