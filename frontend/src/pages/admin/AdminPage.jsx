import { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import api, { mediaUrl } from '../../services/api';
import { useAuth } from '../../context/AuthContext';
import { apiErrorMessage, useModal } from '../../context/ModalContext';

export default function AdminPage() {
  const { auth } = useAuth();
  const [appointments, setAppointments] = useState([]);
  const [stats, setStats] = useState(null);
  const [services, setServices] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [salon, setSalon] = useState(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [tab, setTab] = useState('appointments');
  const [uploading, setUploading] = useState(false);
  const { show } = useModal();

  useEffect(() => {
    if (auth?.role !== 'Admin') return;
    Promise.all([
      api.get('/appointments'),
      api.get('/admin/stats'),
      api.get('/services', { params: { all: true } }),
      api.get('/admin/templates'),
      api.get('/salon'),
    ])
      .then(([a, s, svc, t, salonRes]) => {
        setAppointments(a.data);
        setStats(s.data);
        setServices(svc.data);
        setTemplates(t.data);
        setSalon(salonRes.data);
        setLoadFailed(false);
      })
      .catch(() => setLoadFailed(true));
  }, [auth]);

  if (!auth) return <Navigate to="/admin/login" replace />;
  if (auth.role !== 'Admin') return <Navigate to="/" replace />;

  const saveSalon = async (e) => {
    e.preventDefault();
    try {
      const { data } = await api.put('/admin/salon', salon);
      setSalon(data);
      show({ title: 'Сохранено', message: 'Настройки салона обновлены. Они подставятся в уведомления.' });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const uploadAboutPhoto = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    const form = new FormData();
    form.append('file', file);
    setUploading(true);
    try {
      const { data } = await api.post('/admin/salon/about-image', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      setSalon(data);
      show({ title: 'Фото обновлено', message: 'Блок «Обо мне» использует новое изображение.' });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    } finally {
      setUploading(false);
    }
  };

  const saveService = async (svc) => {
    try {
      await api.put(`/services/${svc.id}`, {
        name: svc.name,
        description: svc.description,
        price: Number(svc.price),
        durationMinutes: Number(svc.durationMinutes),
        isActive: svc.isActive,
        sortOrder: svc.sortOrder,
      });
      show({ title: 'Услуга сохранена', message: `${svc.name}: ${svc.durationMinutes} мин` });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const saveTemplate = async (t) => {
    try {
      await api.put(`/admin/templates/${t.id}`, t);
      show({ title: 'Шаблон сохранён', message: t.key });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const statusClass = (s) => s.toLowerCase();
  const aboutPreview = mediaUrl(salon?.aboutImageUrl);

  return (
    <div className="admin-layout">
      <aside className="admin-side">
        <strong style={{ display: 'block', marginBottom: '1rem' }}>D_Barber Admin</strong>
        <a href="#appointments" className={tab === 'appointments' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('appointments'); }}>Записи</a>
        <a href="#services" className={tab === 'services' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('services'); }}>Услуги</a>
        <a href="#templates" className={tab === 'templates' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('templates'); }}>Шаблоны уведомлений</a>
        <a href="#settings" className={tab === 'settings' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('settings'); }}>Настройки</a>
        <Link to="/">На сайт</Link>
      </aside>
      <main className="admin-main">
        {loadFailed && <p className="empty-block">Данные отсутствуют</p>}

        {!loadFailed && stats && (
          <p className="lead">Сегодня подтверждённых: {stats.todayConfirmed} · Отмен: {stats.cancelledTotal} · Переносов: {stats.rescheduledTotal}</p>
        )}

        {!loadFailed && tab === 'appointments' && (
          appointments.length === 0 ? (
            <p className="empty-block">Данные отсутствуют</p>
          ) : (
            <table className="table">
              <thead>
                <tr>
                  <th>Дата</th><th>Клиент</th><th>Услуга</th><th>Статус</th>
                </tr>
              </thead>
              <tbody>
                {appointments.map((a) => (
                  <tr key={a.id}>
                    <td>{new Date(a.startAtUtc).toLocaleString('ru-RU')}</td>
                    <td>{a.clientName}<br /><span style={{ color: 'var(--muted)' }}>{a.clientPhone}</span></td>
                    <td>{a.serviceName}</td>
                    <td><span className={`badge ${statusClass(a.status)}`}>{a.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        {!loadFailed && tab === 'services' && (
          services.length === 0 ? (
            <p className="empty-block">Данные отсутствуют</p>
          ) : services.map((svc, idx) => (
            <div key={svc.id} className="panel" style={{ marginBottom: '1rem' }}>
              <div className="form">
                <label>Название<input value={svc.name} onChange={(e) => {
                  const next = [...services]; next[idx] = { ...svc, name: e.target.value }; setServices(next);
                }} /></label>
                <label>Цена<input type="number" value={svc.price} onChange={(e) => {
                  const next = [...services]; next[idx] = { ...svc, price: e.target.value }; setServices(next);
                }} /></label>
                <label>Длительность (мин)<input type="number" value={svc.durationMinutes} onChange={(e) => {
                  const next = [...services]; next[idx] = { ...svc, durationMinutes: e.target.value }; setServices(next);
                }} /></label>
                <button type="button" className="btn btn-primary" onClick={() => saveService(services[idx])}>Сохранить</button>
              </div>
            </div>
          ))
        )}

        {!loadFailed && tab === 'templates' && (
          templates.length === 0 ? (
            <p className="empty-block">Данные отсутствуют</p>
          ) : templates.map((t, idx) => (
            <div key={t.id} className="panel" style={{ marginBottom: '1rem' }}>
              <strong>{t.key}</strong>
              <div className="form" style={{ marginTop: '.75rem' }}>
                <label>Заголовок<input value={t.title} onChange={(e) => {
                  const next = [...templates]; next[idx] = { ...t, title: e.target.value }; setTemplates(next);
                }} /></label>
                <label>Текст<textarea rows={4} value={t.body} onChange={(e) => {
                  const next = [...templates]; next[idx] = { ...t, body: e.target.value }; setTemplates(next);
                }} /></label>
                <button type="button" className="btn btn-primary" onClick={() => saveTemplate(templates[idx])}>Сохранить</button>
              </div>
            </div>
          ))
        )}

        {!loadFailed && tab === 'settings' && (
          !salon ? (
            <p className="empty-block">Данные отсутствуют</p>
          ) : (
            <form className="form" onSubmit={saveSalon} style={{ maxWidth: 560 }}>
              <label>Бренд<input value={salon.brandName} onChange={(e) => setSalon({ ...salon, brandName: e.target.value })} /></label>
              <label>Название салона<input value={salon.salonName} onChange={(e) => setSalon({ ...salon, salonName: e.target.value })} /></label>
              <label>Адрес<input value={salon.address} onChange={(e) => setSalon({ ...salon, address: e.target.value })} /></label>
              <label>Телефон<input value={salon.phone || ''} onChange={(e) => setSalon({ ...salon, phone: e.target.value })} /></label>
              <label>
                Обо мне
                <textarea
                  rows={8}
                  value={salon.aboutHtml || ''}
                  onChange={(e) => setSalon({ ...salon, aboutHtml: e.target.value })}
                  placeholder="Текст блока «Обо мне» (абзацы через пустую строку)"
                />
              </label>
              <div className="admin-about-photo">
                <span>Фото «Обо мне»</span>
                {aboutPreview ? (
                  <img src={aboutPreview} alt="Превью" className="admin-about-preview" />
                ) : (
                  <p className="empty-block">Данные отсутствуют</p>
                )}
                <label className="btn btn-ghost" style={{ justifySelf: 'start', cursor: 'pointer' }}>
                  {uploading ? 'Загрузка…' : 'Загрузить фото'}
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp"
                    hidden
                    disabled={uploading}
                    onChange={uploadAboutPhoto}
                  />
                </label>
              </div>
              <button className="btn btn-primary" type="submit">Сохранить</button>
            </form>
          )
        )}
      </main>
    </div>
  );
}
