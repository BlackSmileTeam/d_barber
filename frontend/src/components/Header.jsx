import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

const sections = [
  { href: '/#top', label: 'Главная' },
  { href: '/#news', label: 'Новости' },
  { href: '/#about', label: 'Обо мне' },
  { href: '/#portfolio', label: 'Портфолио' },
  { href: '/#book', label: 'Запись' },
  { href: '/#contacts', label: 'Контакты' },
];

export default function Header() {
  const { auth, logout } = useAuth();
  const location = useLocation();
  const onLanding = location.pathname === '/';

  return (
    <header className="site-header">
      <div className="container inner">
        <Link to="/#top" className="brand">D_Barber</Link>
        <nav className="nav">
          {sections.map((s) => (
            onLanding ? (
              <a key={s.href} href={s.href.replace('/', '')}>{s.label}</a>
            ) : (
              <Link key={s.href} to={s.href}>{s.label}</Link>
            )
          ))}
          {auth?.role === 'Client' && <Link to="/cabinet">Кабинет</Link>}
          {auth?.role === 'Admin' && <Link to="/admin">Админка</Link>}
          {auth ? (
            <button type="button" className="btn btn-ghost" onClick={logout}>Выйти</button>
          ) : (
            <Link to="/login">Войти</Link>
          )}
        </nav>
      </div>
    </header>
  );
}
