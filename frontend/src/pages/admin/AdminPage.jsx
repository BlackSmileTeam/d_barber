import { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import api, { mediaUrl } from '../../services/api';
import { useAuth } from '../../context/AuthContext';
import { apiErrorMessage, useModal } from '../../context/ModalContext';

const emptyService = () => ({
  id: null,
  name: '',
  description: '',
  price: 2300,
  durationMinutes: 60,
  isActive: true,
  sortOrder: 0,
});

const emptyTemplate = () => ({
  id: null,
  key: '',
  title: '',
  triggerDescription: '',
  body: '',
});

const emptyPortfolio = () => ({
  id: null,
  title: '',
  description: '',
  imageUrl: '',
  serviceId: '',
  displayPrice: '',
  sortOrder: 0,
});

export default function AdminPage() {
  const { auth } = useAuth();
  const [appointments, setAppointments] = useState([]);
  const [stats, setStats] = useState(null);
  const [services, setServices] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [portfolio, setPortfolio] = useState([]);
  const [salon, setSalon] = useState(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [tab, setTab] = useState('appointments');
  const [uploading, setUploading] = useState(false);
  const [newService, setNewService] = useState(emptyService);
  const [newTemplate, setNewTemplate] = useState(emptyTemplate);
  const [newPortfolio, setNewPortfolio] = useState(emptyPortfolio);
  const { show } = useModal();

  const reload = () => {
    if (auth?.role !== 'Admin') return;
    Promise.all([
      api.get('/appointments'),
      api.get('/admin/stats'),
      api.get('/services', { params: { all: true } }),
      api.get('/admin/templates'),
      api.get('/portfolio'),
      api.get('/salon'),
    ])
      .then(([a, s, svc, t, p, salonRes]) => {
        setAppointments(a.data);
        setStats(s.data);
        setServices(svc.data);
        setTemplates(t.data);
        setPortfolio(p.data);
        setSalon(salonRes.data);
        setLoadFailed(false);
      })
      .catch(() => setLoadFailed(true));
  };

  useEffect(() => {
    reload();
  }, [auth]);

  if (!auth) return <Navigate to="/admin/login" replace />;
  if (auth.role !== 'Admin') return <Navigate to="/" replace />;

  const saveSalon = async (e) => {
    e.preventDefault();
    try {
      const { data } = await api.put('/admin/salon', salon);
      setSalon(data);
      show({ title: 'Сохранено', message: 'Настройки салона обновлены.' });
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

  const patchService = (idx, patch) => {
    const next = [...services];
    next[idx] = { ...next[idx], ...patch };
    setServices(next);
  };

  const saveService = async (svc) => {
    try {
      const payload = {
        name: svc.name,
        description: svc.description,
        price: Number(svc.price),
        durationMinutes: Number(svc.durationMinutes),
        isActive: !!svc.isActive,
        sortOrder: Number(svc.sortOrder) || 0,
      };
      if (svc.id) {
        await api.put(`/services/${svc.id}`, payload);
        show({ title: 'Услуга сохранена', message: svc.name });
      } else {
        const { data } = await api.post('/services', payload);
        setServices((prev) => [...prev, data]);
        setNewService(emptyService());
        show({ title: 'Услуга создана', message: data.name });
      }
      reload();
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const deleteService = (svc) => {
    show({
      title: 'Удалить услугу?',
      message: `«${svc.name}» будет удалена. Если есть записи — удаление запрещено.`,
      actions: [
        { label: 'Отмена' },
        {
          label: 'Удалить',
          primary: true,
          onClick: async () => {
            try {
              await api.delete(`/services/${svc.id}`);
              setServices((prev) => prev.filter((s) => s.id !== svc.id));
              show({ title: 'Удалено', message: `Услуга «${svc.name}» удалена.` });
            } catch (err) {
              show({ title: 'Ошибка', message: apiErrorMessage(err) });
            }
          },
        },
      ],
    });
  };

  const patchTemplate = (idx, patch) => {
    const next = [...templates];
    next[idx] = { ...next[idx], ...patch };
    setTemplates(next);
  };

  const saveTemplate = async (t) => {
    try {
      await api.put(`/admin/templates/${t.id}`, t);
      show({ title: 'Шаблон сохранён', message: t.title || t.key });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const createTemplate = async () => {
    try {
      const { data } = await api.post('/admin/templates', {
        key: newTemplate.key,
        title: newTemplate.title,
        triggerDescription: newTemplate.triggerDescription,
        body: newTemplate.body,
      });
      setTemplates((prev) => [...prev, data]);
      setNewTemplate(emptyTemplate());
      show({ title: 'Шаблон создан', message: data.title });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const deleteTemplate = (t) => {
    show({
      title: 'Удалить шаблон?',
      message: `«${t.title || t.key}» будет удалён. Связанные уведомления перестанут отправляться.`,
      actions: [
        { label: 'Отмена' },
        {
          label: 'Удалить',
          primary: true,
          onClick: async () => {
            try {
              await api.delete(`/admin/templates/${t.id}`);
              setTemplates((prev) => prev.filter((x) => x.id !== t.id));
              show({ title: 'Удалено', message: 'Шаблон удалён.' });
            } catch (err) {
              show({ title: 'Ошибка', message: apiErrorMessage(err) });
            }
          },
        },
      ],
    });
  };

  const patchPortfolio = (idx, patch) => {
    const next = [...portfolio];
    next[idx] = { ...next[idx], ...patch };
    setPortfolio(next);
  };

  const portfolioPayload = (item) => ({
    title: item.title,
    description: item.description || null,
    imageUrl: item.imageUrl,
    serviceId: item.serviceId || null,
    displayPrice: item.displayPrice === '' || item.displayPrice == null ? null : Number(item.displayPrice),
    sortOrder: Number(item.sortOrder) || 0,
  });

  const savePortfolioItem = async (item, isNew = false) => {
    try {
      if (isNew || !item.id) {
        const { data } = await api.post('/admin/portfolio', portfolioPayload(item));
        setPortfolio((prev) => [...prev, data]);
        setNewPortfolio(emptyPortfolio());
        show({ title: 'Добавлено', message: data.title });
      } else {
        const { data } = await api.put(`/admin/portfolio/${item.id}`, portfolioPayload(item));
        setPortfolio((prev) => prev.map((p) => (p.id === data.id ? data : p)));
        show({ title: 'Сохранено', message: data.title });
      }
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const deletePortfolioItem = (item) => {
    show({
      title: 'Удалить работу?',
      message: `«${item.title}» исчезнет из портфолио на сайте.`,
      actions: [
        { label: 'Отмена' },
        {
          label: 'Удалить',
          primary: true,
          onClick: async () => {
            try {
              await api.delete(`/admin/portfolio/${item.id}`);
              setPortfolio((prev) => prev.filter((p) => p.id !== item.id));
              show({ title: 'Удалено', message: 'Работа удалена из портфолио.' });
            } catch (err) {
              show({ title: 'Ошибка', message: apiErrorMessage(err) });
            }
          },
        },
      ],
    });
  };

  const uploadPortfolioImage = async (e, target) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    const form = new FormData();
    form.append('file', file);
    setUploading(true);
    try {
      const { data } = await api.post('/admin/portfolio/image', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      if (target === 'new') {
        setNewPortfolio((prev) => ({ ...prev, imageUrl: data.imageUrl }));
      } else {
        patchPortfolio(target, { imageUrl: data.imageUrl });
      }
      show({ title: 'Фото загружено', message: 'Не забудьте сохранить карточку.' });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    } finally {
      setUploading(false);
    }
  };

  const statusClass = (s) => s.toLowerCase();
  const aboutPreview = mediaUrl(salon?.aboutImageUrl);

  const renderServiceForm = (svc, idx, isNew = false) => (
    <div key={svc.id || 'new-service'} className="panel" style={{ marginBottom: '1rem' }}>
      {!isNew && <strong className="admin-card-title">{svc.name || 'Услуга'}</strong>}
      {isNew && <strong className="admin-card-title">Новая услуга</strong>}
      <div className="form" style={{ marginTop: '.75rem' }}>
        <label>Название<input value={svc.name} onChange={(e) => (isNew ? setNewService({ ...svc, name: e.target.value }) : patchService(idx, { name: e.target.value }))} /></label>
        <label>Описание<textarea rows={2} value={svc.description || ''} onChange={(e) => (isNew ? setNewService({ ...svc, description: e.target.value }) : patchService(idx, { description: e.target.value }))} /></label>
        <div className="admin-form-row">
          <label>Цена, ₽<input type="number" value={svc.price} onChange={(e) => (isNew ? setNewService({ ...svc, price: e.target.value }) : patchService(idx, { price: e.target.value }))} /></label>
          <label>Длительность, мин<input type="number" value={svc.durationMinutes} onChange={(e) => (isNew ? setNewService({ ...svc, durationMinutes: e.target.value }) : patchService(idx, { durationMinutes: e.target.value }))} /></label>
          <label>Порядок<input type="number" value={svc.sortOrder} onChange={(e) => (isNew ? setNewService({ ...svc, sortOrder: e.target.value }) : patchService(idx, { sortOrder: e.target.value }))} /></label>
        </div>
        <label className="admin-check">
          <input type="checkbox" checked={!!svc.isActive} onChange={(e) => (isNew ? setNewService({ ...svc, isActive: e.target.checked }) : patchService(idx, { isActive: e.target.checked }))} />
          Активна (показывается клиентам)
        </label>
        <div className="admin-actions">
          <button type="button" className="btn btn-primary" onClick={() => saveService(svc)}>{isNew ? 'Создать' : 'Сохранить'}</button>
          {!isNew && <button type="button" className="btn btn-danger" onClick={() => deleteService(svc)}>Удалить</button>}
        </div>
      </div>
    </div>
  );

  const renderPortfolioForm = (item, idx, isNew = false) => (
    <div key={item.id || 'new-portfolio'} className="panel" style={{ marginBottom: '1rem' }}>
      <strong className="admin-card-title">{isNew ? 'Новая работа' : (item.title || 'Работа')}</strong>
      <div className="form" style={{ marginTop: '.75rem' }}>
        <label>Название<input value={item.title} onChange={(e) => (isNew ? setNewPortfolio({ ...item, title: e.target.value }) : patchPortfolio(idx, { title: e.target.value }))} /></label>
        <label>Описание<textarea rows={2} value={item.description || ''} onChange={(e) => (isNew ? setNewPortfolio({ ...item, description: e.target.value }) : patchPortfolio(idx, { description: e.target.value }))} /></label>
        <label>
          Услуга
          <select
            value={item.serviceId || ''}
            onChange={(e) => (isNew ? setNewPortfolio({ ...item, serviceId: e.target.value }) : patchPortfolio(idx, { serviceId: e.target.value }))}
          >
            <option value="">Без привязки</option>
            {services.map((s) => (
              <option key={s.id} value={s.id}>{s.name}</option>
            ))}
          </select>
        </label>
        <div className="admin-form-row">
          <label>Цена на сайте<input type="number" value={item.displayPrice ?? ''} onChange={(e) => (isNew ? setNewPortfolio({ ...item, displayPrice: e.target.value }) : patchPortfolio(idx, { displayPrice: e.target.value }))} /></label>
          <label>Порядок<input type="number" value={item.sortOrder} onChange={(e) => (isNew ? setNewPortfolio({ ...item, sortOrder: e.target.value }) : patchPortfolio(idx, { sortOrder: e.target.value }))} /></label>
        </div>
        <label>URL изображения<input value={item.imageUrl || ''} onChange={(e) => (isNew ? setNewPortfolio({ ...item, imageUrl: e.target.value }) : patchPortfolio(idx, { imageUrl: e.target.value }))} placeholder="https://… или загрузите файл" /></label>
        {item.imageUrl && (
          <img src={mediaUrl(item.imageUrl)} alt="" className="admin-portfolio-preview" />
        )}
        <label className="btn btn-ghost" style={{ justifySelf: 'start', cursor: 'pointer' }}>
          {uploading ? 'Загрузка…' : 'Загрузить фото'}
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            hidden
            disabled={uploading}
            onChange={(e) => uploadPortfolioImage(e, isNew ? 'new' : idx)}
          />
        </label>
        <div className="admin-actions">
          <button type="button" className="btn btn-primary" onClick={() => savePortfolioItem(item, isNew)}>{isNew ? 'Добавить' : 'Сохранить'}</button>
          {!isNew && <button type="button" className="btn btn-danger" onClick={() => deletePortfolioItem(item)}>Удалить</button>}
        </div>
      </div>
    </div>
  );

  return (
    <div className="admin-layout">
      <aside className="admin-side">
        <strong style={{ display: 'block', marginBottom: '1rem' }}>D_Barber Admin</strong>
        <a href="#appointments" className={tab === 'appointments' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('appointments'); }}>Записи</a>
        <a href="#services" className={tab === 'services' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('services'); }}>Услуги</a>
        <a href="#portfolio" className={tab === 'portfolio' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('portfolio'); }}>Портфолио</a>
        <a href="#templates" className={tab === 'templates' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('templates'); }}>Уведомления</a>
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
          <>
            {services.map((svc, idx) => renderServiceForm(svc, idx))}
            {renderServiceForm(newService, -1, true)}
          </>
        )}

        {!loadFailed && tab === 'portfolio' && (
          <>
            {portfolio.length === 0 && <p className="empty-block">Данные отсутствуют</p>}
            {portfolio.map((item, idx) => renderPortfolioForm(item, idx))}
            {renderPortfolioForm(newPortfolio, -1, true)}
          </>
        )}

        {!loadFailed && tab === 'templates' && (
          <>
            <p className="lead admin-hint">
              У каждого шаблона видно, когда он отправляется. Системные ключи
              {' '}
              <code>booking_created</code>
              ,
              {' '}
              <code>reminder_2h</code>
              ,
              {' '}
              <code>monthly_comeback</code>
              {' '}
              используются ботом автоматически.
            </p>
            {templates.length === 0 && <p className="empty-block">Данные отсутствуют</p>}
            {templates.map((t, idx) => (
              <div key={t.id} className="panel" style={{ marginBottom: '1rem' }}>
                <strong className="admin-card-title">{t.title || t.key}</strong>
                <p className="admin-trigger">{t.triggerDescription || 'Когда отправляется — не указано'}</p>
                <p className="admin-key">Ключ: {t.key}</p>
                <div className="form" style={{ marginTop: '.75rem' }}>
                  <label>Название<input value={t.title} onChange={(e) => patchTemplate(idx, { title: e.target.value })} /></label>
                  <label>Когда отправляется<input value={t.triggerDescription || ''} onChange={(e) => patchTemplate(idx, { triggerDescription: e.target.value })} placeholder="Например: сразу после записи" /></label>
                  <label>Текст<textarea rows={4} value={t.body} onChange={(e) => patchTemplate(idx, { body: e.target.value })} /></label>
                  <div className="admin-actions">
                    <button type="button" className="btn btn-primary" onClick={() => saveTemplate(templates[idx])}>Сохранить</button>
                    <button type="button" className="btn btn-danger" onClick={() => deleteTemplate(t)}>Удалить</button>
                  </div>
                </div>
              </div>
            ))}
            <div className="panel">
              <strong className="admin-card-title">Новый шаблон</strong>
              <div className="form" style={{ marginTop: '.75rem' }}>
                <label>Ключ (латиница)<input value={newTemplate.key} onChange={(e) => setNewTemplate({ ...newTemplate, key: e.target.value })} placeholder="custom_promo" /></label>
                <label>Название<input value={newTemplate.title} onChange={(e) => setNewTemplate({ ...newTemplate, title: e.target.value })} placeholder="Акция выходного дня" /></label>
                <label>Когда отправляется<input value={newTemplate.triggerDescription} onChange={(e) => setNewTemplate({ ...newTemplate, triggerDescription: e.target.value })} placeholder="Вручную / по расписанию…" /></label>
                <label>Текст<textarea rows={4} value={newTemplate.body} onChange={(e) => setNewTemplate({ ...newTemplate, body: e.target.value })} /></label>
                <button type="button" className="btn btn-primary" onClick={createTemplate}>Создать</button>
              </div>
            </div>
          </>
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
              <label>Instagram (ссылка)<input value={salon.instagramUrl || ''} onChange={(e) => setSalon({ ...salon, instagramUrl: e.target.value })} placeholder="https://www.instagram.com/…" /></label>
              <label>Telegram (ссылка)<input value={salon.telegramUrl || ''} onChange={(e) => setSalon({ ...salon, telegramUrl: e.target.value })} placeholder="https://t.me/…" /></label>
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
