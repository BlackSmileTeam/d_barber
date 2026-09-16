import { useState } from 'react';
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
  const [open, setOpen] = useState(false);

  const close = () => setOpen(false);

  return (
    <header className={`site-header${open ? ' is-open' : ''}`}>
      <div className="container inner">
        <Link to="/#top" className="brand" onClick={close}>D_Barber</Link>

        <button
          type="button"
          className="nav-toggle"
          aria-expanded={open}
          aria-controls="site-nav"
          aria-label={open ? 'Закрыть меню' : 'Открыть меню'}
          onClick={() => setOpen((v) => !v)}
        >
          <span />
          <span />
        </button>

        <nav className="nav" id="site-nav">
          {sections.map((s) => (
            onLanding ? (
              <a key={s.href} href={s.href.replace('/', '')} onClick={close}>{s.label}</a>
            ) : (
              <Link key={s.href} to={s.href} onClick={close}>{s.label}</Link>
            )
          ))}
          {auth?.role === 'Client' && <Link to="/cabinet" onClick={close}>Кабинет</Link>}
          {auth?.role === 'Admin' && <Link to="/admin" onClick={close}>Админка</Link>}
          {auth ? (
            <button type="button" className="btn btn-ghost" onClick={() => { logout(); close(); }}>Выйти</button>
          ) : (
            <Link to="/login" className="nav-login" onClick={close}>Войти</Link>
          )}
        </nav>
      </div>
    </header>
  );
}
