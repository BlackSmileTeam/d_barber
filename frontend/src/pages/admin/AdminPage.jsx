import { useEffect, useMemo, useState } from 'react';
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
  triggerIntervalType: 'None',
  triggerIntervalDays: '',
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

const INTERVAL_OPTIONS = [
  { value: 'None', label: 'Событие / вручную (свободный текст)' },
  { value: 'Monthly', label: 'Раз в месяц после визита' },
  { value: 'Daily', label: 'Каждый день после визита' },
  { value: 'Weekly', label: 'Раз в неделю после визита' },
  { value: 'Custom', label: 'Кастомное (дней после визита)' },
];

const intervalLabel = (type, days) => {
  switch (type) {
    case 'Monthly': return 'Раз в месяц после визита';
    case 'Daily': return 'Каждый день после визита';
    case 'Weekly': return 'Раз в неделю после визита';
    case 'Custom': return days ? `Через ${days} дн. после визита` : 'Кастомный интервал';
    default: return null;
  }
};

const stableJson = (value) => JSON.stringify(value, Object.keys(value || {}).sort());

const TrashIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <path d="M3 6h18" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    <path d="M8 6V4.5A1.5 1.5 0 0 1 9.5 3h5A1.5 1.5 0 0 1 16 4.5V6" stroke="currentColor" strokeWidth="1.8" />
    <path d="M19 6l-1 14.5A1.5 1.5 0 0 1 16.5 22h-9A1.5 1.5 0 0 1 6 20.5L5 6" stroke="currentColor" strokeWidth="1.8" />
    <path d="M10 11v6M14 11v6" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
  </svg>
);

function SectionToolbar({ title, onCreate, createLabel = 'Создать' }) {
  return (
    <div className="admin-section-toolbar">
      <h2 className="admin-section-title">{title}</h2>
      {onCreate && (
        <button type="button" className="btn btn-primary" onClick={onCreate}>{createLabel}</button>
      )}
    </div>
  );
}

export default function AdminPage() {
  const { auth } = useAuth();
  const [appointments, setAppointments] = useState([]);
  const [stats, setStats] = useState(null);
  const [services, setServices] = useState([]);
  const [servicesBaseline, setServicesBaseline] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [templatesBaseline, setTemplatesBaseline] = useState([]);
  const [portfolio, setPortfolio] = useState([]);
  const [portfolioBaseline, setPortfolioBaseline] = useState([]);
  const [clients, setClients] = useState([]);
  const [salon, setSalon] = useState(null);
  const [salonBaseline, setSalonBaseline] = useState(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [tab, setTab] = useState('appointments');
  const [uploading, setUploading] = useState(false);
  const [showNewService, setShowNewService] = useState(false);
  const [showNewTemplate, setShowNewTemplate] = useState(false);
  const [showNewPortfolio, setShowNewPortfolio] = useState(false);
  const [newService, setNewService] = useState(emptyService);
  const [newTemplate, setNewTemplate] = useState(emptyTemplate);
  const [newPortfolio, setNewPortfolio] = useState(emptyPortfolio);
  const [resetDrafts, setResetDrafts] = useState({});
  const { show } = useModal();

  const normalizeTemplate = (t) => ({
    ...t,
    triggerIntervalType: t.triggerIntervalType || 'None',
    triggerIntervalDays: t.triggerIntervalDays ?? '',
  });

  const reload = () => {
    if (auth?.role !== 'Admin') return;
    Promise.all([
      api.get('/appointments'),
      api.get('/admin/stats'),
      api.get('/services', { params: { all: true } }),
      api.get('/admin/templates'),
      api.get('/portfolio'),
      api.get('/salon'),
      api.get('/admin/clients'),
    ])
      .then(([a, s, svc, t, p, salonRes, clientsRes]) => {
        setAppointments(a.data);
        setStats(s.data);
        setServices(svc.data);
        setServicesBaseline(svc.data.map((x) => ({ ...x })));
        const tpl = (t.data || []).map(normalizeTemplate);
        setTemplates(tpl);
        setTemplatesBaseline(tpl.map((x) => ({ ...x })));
        setPortfolio(p.data);
        setPortfolioBaseline(p.data.map((x) => ({ ...x })));
        setSalon(salonRes.data);
        setSalonBaseline({ ...salonRes.data });
        setClients(clientsRes.data || []);
        setLoadFailed(false);
      })
      .catch(() => setLoadFailed(true));
  };

  useEffect(() => {
    reload();
  }, [auth]);

  const salonDirty = useMemo(() => {
    if (!salon || !salonBaseline) return false;
    return stableJson(salon) !== stableJson(salonBaseline);
  }, [salon, salonBaseline]);

  const isServiceDirty = (svc) => {
    if (!svc?.id) return true;
    const base = servicesBaseline.find((x) => x.id === svc.id);
    if (!base) return true;
    return stableJson({
      name: svc.name,
      description: svc.description || '',
      price: Number(svc.price),
      durationMinutes: Number(svc.durationMinutes),
      isActive: !!svc.isActive,
      sortOrder: Number(svc.sortOrder) || 0,
    }) !== stableJson({
      name: base.name,
      description: base.description || '',
      price: Number(base.price),
      durationMinutes: Number(base.durationMinutes),
      isActive: !!base.isActive,
      sortOrder: Number(base.sortOrder) || 0,
    });
  };

  const isTemplateDirty = (t) => {
    if (!t?.id) return true;
    const base = templatesBaseline.find((x) => x.id === t.id);
    if (!base) return true;
    return stableJson({
      title: t.title,
      triggerDescription: t.triggerDescription || '',
      triggerIntervalType: t.triggerIntervalType || 'None',
      triggerIntervalDays: t.triggerIntervalDays === '' || t.triggerIntervalDays == null ? null : Number(t.triggerIntervalDays),
      body: t.body || '',
    }) !== stableJson({
      title: base.title,
      triggerDescription: base.triggerDescription || '',
      triggerIntervalType: base.triggerIntervalType || 'None',
      triggerIntervalDays: base.triggerIntervalDays === '' || base.triggerIntervalDays == null ? null : Number(base.triggerIntervalDays),
      body: base.body || '',
    });
  };

  const portfolioPayload = (item) => ({
    title: item.title,
    description: item.description || null,
    imageUrl: item.imageUrl,
    serviceId: item.serviceId || null,
    displayPrice: item.displayPrice === '' || item.displayPrice == null ? null : Number(item.displayPrice),
    sortOrder: Number(item.sortOrder) || 0,
  });

  const isPortfolioDirty = (item) => {
    if (!item?.id) return true;
    const base = portfolioBaseline.find((x) => x.id === item.id);
    if (!base) return true;
    return stableJson(portfolioPayload(item)) !== stableJson(portfolioPayload(base));
  };

  if (!auth) return <Navigate to="/admin/login" replace />;
  if (auth.role !== 'Admin') return <Navigate to="/" replace />;

  const saveSalon = async (e) => {
    e.preventDefault();
    if (!salonDirty) return;
    try {
      const { data } = await api.put('/admin/salon', salon);
      setSalon(data);
      setSalonBaseline({ ...data });
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
      setSalonBaseline({ ...data });
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
        setNewService(emptyService());
        setShowNewService(false);
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
              setServicesBaseline((prev) => prev.filter((s) => s.id !== svc.id));
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

  const templatePayload = (t) => ({
    id: t.id,
    key: t.key,
    title: t.title,
    triggerDescription: t.triggerDescription || '',
    triggerIntervalType: t.triggerIntervalType || 'None',
    triggerIntervalDays: t.triggerIntervalType === 'Custom'
      ? (t.triggerIntervalDays === '' || t.triggerIntervalDays == null ? null : Number(t.triggerIntervalDays))
      : null,
    body: t.body || '',
  });

  const saveTemplate = async (t) => {
    try {
      const { data } = await api.put(`/admin/templates/${t.id}`, templatePayload(t));
      const normalized = normalizeTemplate(data);
      setTemplates((prev) => prev.map((x) => (x.id === normalized.id ? normalized : x)));
      setTemplatesBaseline((prev) => prev.map((x) => (x.id === normalized.id ? { ...normalized } : x)));
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
        triggerIntervalType: newTemplate.triggerIntervalType || 'None',
        triggerIntervalDays: newTemplate.triggerIntervalType === 'Custom'
          ? (newTemplate.triggerIntervalDays === '' ? null : Number(newTemplate.triggerIntervalDays))
          : null,
        body: newTemplate.body,
      });
      const normalized = normalizeTemplate(data);
      setTemplates((prev) => [...prev, normalized]);
      setTemplatesBaseline((prev) => [...prev, { ...normalized }]);
      setNewTemplate(emptyTemplate());
      setShowNewTemplate(false);
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
              setTemplatesBaseline((prev) => prev.filter((x) => x.id !== t.id));
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

  const savePortfolioItem = async (item, isNew = false) => {
    try {
      if (isNew || !item.id) {
        const { data } = await api.post('/admin/portfolio', portfolioPayload(item));
        setPortfolio((prev) => [...prev, data]);
        setPortfolioBaseline((prev) => [...prev, { ...data }]);
        setNewPortfolio(emptyPortfolio());
        setShowNewPortfolio(false);
        show({ title: 'Добавлено', message: data.title });
      } else {
        const { data } = await api.put(`/admin/portfolio/${item.id}`, portfolioPayload(item));
        setPortfolio((prev) => prev.map((p) => (p.id === data.id ? data : p)));
        setPortfolioBaseline((prev) => prev.map((p) => (p.id === data.id ? { ...data } : p)));
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
              setPortfolioBaseline((prev) => prev.filter((p) => p.id !== item.id));
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

  const resetPassword = async (client, generate) => {
    const draft = resetDrafts[client.id] || '';
    try {
      const { data } = await api.post(`/admin/clients/${client.id}/reset-password`, {
        newPassword: generate ? null : (draft.trim() || null),
      });
      setResetDrafts((prev) => ({ ...prev, [client.id]: '' }));
      show({
        title: 'Пароль сброшен',
        message: `Новый пароль для ${client.name} (${client.phone}). Сохраните его сейчас — повторно он не отобразится.`,
        details: [data.password],
      });
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const statusClass = (s) => s.toLowerCase();
  const aboutPreview = mediaUrl(salon?.aboutImageUrl);

  const renderTriggerFields = (t, onChange) => (
    <>
      <label>
        Когда отправляется
        <select
          value={t.triggerIntervalType || 'None'}
          onChange={(e) => onChange({
            triggerIntervalType: e.target.value,
            triggerIntervalDays: e.target.value === 'Custom' ? (t.triggerIntervalDays || 30) : '',
          })}
        >
          {INTERVAL_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </label>
      {(t.triggerIntervalType || 'None') === 'Custom' && (
        <label>
          Число дней после визита
          <input
            type="number"
            min={1}
            value={t.triggerIntervalDays ?? ''}
            onChange={(e) => onChange({ triggerIntervalDays: e.target.value })}
          />
        </label>
      )}
      <label>
        {(t.triggerIntervalType || 'None') === 'None' ? 'Описание триггера' : 'Комментарий (необязательно)'}
        <input
          value={t.triggerDescription || ''}
          onChange={(e) => onChange({ triggerDescription: e.target.value })}
          placeholder={(t.triggerIntervalType || 'None') === 'None' ? 'Например: сразу после записи' : 'Дополнительное пояснение'}
        />
      </label>
    </>
  );

  const renderServiceForm = (svc, idx, isNew = false) => {
    const dirty = isNew || isServiceDirty(svc);
    return (
      <div key={svc.id || 'new-service'} className="panel" style={{ marginBottom: '1rem' }}>
        <div className="admin-card-head">
          <strong className="admin-card-title">{isNew ? 'Новая услуга' : (svc.name || 'Услуга')}</strong>
          {!isNew && (
            <button type="button" className="btn-icon-danger" aria-label="Удалить услугу" title="Удалить" onClick={() => deleteService(svc)}>
              <TrashIcon />
            </button>
          )}
        </div>
        <div className="form admin-service-grid" style={{ marginTop: '.75rem' }}>
          <div className="admin-service-col">
            <label>Название<input value={svc.name} onChange={(e) => (isNew ? setNewService({ ...svc, name: e.target.value }) : patchService(idx, { name: e.target.value }))} /></label>
            <label>Описание<textarea rows={3} value={svc.description || ''} onChange={(e) => (isNew ? setNewService({ ...svc, description: e.target.value }) : patchService(idx, { description: e.target.value }))} /></label>
            <label className="admin-check">
              <input type="checkbox" checked={!!svc.isActive} onChange={(e) => (isNew ? setNewService({ ...svc, isActive: e.target.checked }) : patchService(idx, { isActive: e.target.checked }))} />
              Активна (показывается клиентам)
            </label>
          </div>
          <div className="admin-service-col">
            <label>Цена, ₽<input type="number" value={svc.price} onChange={(e) => (isNew ? setNewService({ ...svc, price: e.target.value }) : patchService(idx, { price: e.target.value }))} /></label>
            <label>Длительность, мин<input type="number" value={svc.durationMinutes} onChange={(e) => (isNew ? setNewService({ ...svc, durationMinutes: e.target.value }) : patchService(idx, { durationMinutes: e.target.value }))} /></label>
            <label>Порядок<input type="number" value={svc.sortOrder} onChange={(e) => (isNew ? setNewService({ ...svc, sortOrder: e.target.value }) : patchService(idx, { sortOrder: e.target.value }))} /></label>
          </div>
          <div className="admin-actions admin-service-actions">
            {isNew ? (
              <>
                <button type="button" className="btn btn-primary" onClick={() => saveService(svc)}>Создать</button>
                <button type="button" className="btn btn-ghost" onClick={() => { setShowNewService(false); setNewService(emptyService()); }}>Отмена</button>
              </>
            ) : dirty ? (
              <button type="button" className="btn btn-primary" onClick={() => saveService(svc)}>Сохранить</button>
            ) : null}
          </div>
        </div>
      </div>
    );
  };

  const renderPortfolioForm = (item, idx, isNew = false) => {
    const dirty = isNew || isPortfolioDirty(item);
    return (
      <div key={item.id || 'new-portfolio'} className="panel" style={{ marginBottom: '1rem' }}>
        <div className="admin-card-head">
          <strong className="admin-card-title">{isNew ? 'Новая работа' : (item.title || 'Работа')}</strong>
          {!isNew && (
            <button type="button" className="btn-icon-danger" aria-label="Удалить работу" title="Удалить" onClick={() => deletePortfolioItem(item)}>
              <TrashIcon />
            </button>
          )}
        </div>
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
            {isNew ? (
              <>
                <button type="button" className="btn btn-primary" onClick={() => savePortfolioItem(item, true)}>Добавить</button>
                <button type="button" className="btn btn-ghost" onClick={() => { setShowNewPortfolio(false); setNewPortfolio(emptyPortfolio()); }}>Отмена</button>
              </>
            ) : dirty ? (
              <button type="button" className="btn btn-primary" onClick={() => savePortfolioItem(item)}>Сохранить</button>
            ) : null}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="admin-layout">
      <aside className="admin-side">
        <strong style={{ display: 'block', marginBottom: '1rem' }}>D_Barber Admin</strong>
        <a href="#appointments" className={tab === 'appointments' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('appointments'); }}>Записи</a>
        <a href="#services" className={tab === 'services' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('services'); }}>Услуги</a>
        <a href="#portfolio" className={tab === 'portfolio' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('portfolio'); }}>Портфолио</a>
        <a href="#clients" className={tab === 'clients' ? 'active' : ''} onClick={(e) => { e.preventDefault(); setTab('clients'); }}>Клиенты</a>
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
            <SectionToolbar title="Услуги" onCreate={() => setShowNewService(true)} />
            {showNewService && renderServiceForm(newService, -1, true)}
            {services.map((svc, idx) => renderServiceForm(svc, idx))}
            {services.length === 0 && !showNewService && <p className="empty-block">Данные отсутствуют</p>}
          </>
        )}

        {!loadFailed && tab === 'portfolio' && (
          <>
            <SectionToolbar title="Портфолио" onCreate={() => setShowNewPortfolio(true)} createLabel="Добавить" />
            {showNewPortfolio && renderPortfolioForm(newPortfolio, -1, true)}
            {portfolio.map((item, idx) => renderPortfolioForm(item, idx))}
            {portfolio.length === 0 && !showNewPortfolio && <p className="empty-block">Данные отсутствуют</p>}
          </>
        )}

        {!loadFailed && tab === 'clients' && (
          <>
            <SectionToolbar title="Клиенты" />
            {clients.length === 0 ? (
              <p className="empty-block">Данные отсутствуют</p>
            ) : (
              <div className="admin-clients-list">
                {clients.map((c) => (
                  <div key={c.id} className="panel admin-client-card">
                    <div className="admin-client-main">
                      <strong className="admin-card-title">{c.name}</strong>
                      <p className="admin-key">{c.phone}</p>
                      <p className="admin-client-meta">
                        Регистрация: {new Date(c.createdAtUtc).toLocaleString('ru-RU')}
                        {c.lastVisitAtUtc ? ` · Последний визит: ${new Date(c.lastVisitAtUtc).toLocaleString('ru-RU')}` : ' · Визитов ещё не было'}
                        {c.telegramLinked ? ' · Telegram привязан' : ''}
                      </p>
                    </div>
                    <div className="admin-client-reset form">
                      <label>
                        Новый пароль
                        <input
                          type="text"
                          value={resetDrafts[c.id] || ''}
                          onChange={(e) => setResetDrafts((prev) => ({ ...prev, [c.id]: e.target.value }))}
                          placeholder="Оставьте пустым и сгенерируйте"
                        />
                      </label>
                      <div className="admin-actions">
                        <button type="button" className="btn btn-primary" onClick={() => resetPassword(c, false)} disabled={!(resetDrafts[c.id] || '').trim()}>
                          Установить пароль
                        </button>
                        <button type="button" className="btn btn-ghost" onClick={() => resetPassword(c, true)}>
                          Сгенерировать
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </>
        )}

        {!loadFailed && tab === 'templates' && (
          <>
            <SectionToolbar title="Уведомления" onCreate={() => setShowNewTemplate(true)} />
            <p className="lead admin-hint">
              У каждого шаблона видно, когда он отправляется. Для напоминаний после визита выберите интервал
              (раз в месяц / неделю / день или кастом). Системные ключи
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
            {showNewTemplate && (
              <div className="panel" style={{ marginBottom: '1rem' }}>
                <strong className="admin-card-title">Новый шаблон</strong>
                <div className="form" style={{ marginTop: '.75rem' }}>
                  <label>Ключ (латиница)<input value={newTemplate.key} onChange={(e) => setNewTemplate({ ...newTemplate, key: e.target.value })} placeholder="custom_promo" /></label>
                  <label>Название<input value={newTemplate.title} onChange={(e) => setNewTemplate({ ...newTemplate, title: e.target.value })} placeholder="Акция выходного дня" /></label>
                  {renderTriggerFields(newTemplate, (patch) => setNewTemplate({ ...newTemplate, ...patch }))}
                  <label>Текст<textarea rows={4} value={newTemplate.body} onChange={(e) => setNewTemplate({ ...newTemplate, body: e.target.value })} /></label>
                  <div className="admin-actions">
                    <button type="button" className="btn btn-primary" onClick={createTemplate}>Создать</button>
                    <button type="button" className="btn btn-ghost" onClick={() => { setShowNewTemplate(false); setNewTemplate(emptyTemplate()); }}>Отмена</button>
                  </div>
                </div>
              </div>
            )}
            {templates.length === 0 && !showNewTemplate && <p className="empty-block">Данные отсутствуют</p>}
            {templates.map((t, idx) => {
              const dirty = isTemplateDirty(t);
              const auto = intervalLabel(t.triggerIntervalType, t.triggerIntervalDays);
              return (
                <div key={t.id} className="panel" style={{ marginBottom: '1rem' }}>
                  <div className="admin-card-head">
                    <strong className="admin-card-title">{t.title || t.key}</strong>
                    <button type="button" className="btn-icon-danger" aria-label="Удалить шаблон" title="Удалить" onClick={() => deleteTemplate(t)}>
                      <TrashIcon />
                    </button>
                  </div>
                  <p className="admin-trigger">{auto || t.triggerDescription || 'Когда отправляется — не указано'}</p>
                  <p className="admin-key">Ключ: {t.key}</p>
                  <div className="form" style={{ marginTop: '.75rem' }}>
                    <label>Название<input value={t.title} onChange={(e) => patchTemplate(idx, { title: e.target.value })} /></label>
                    {renderTriggerFields(t, (patch) => patchTemplate(idx, patch))}
                    <label>Текст<textarea rows={4} value={t.body} onChange={(e) => patchTemplate(idx, { body: e.target.value })} /></label>
                    {dirty && (
                      <div className="admin-actions">
                        <button type="button" className="btn btn-primary" onClick={() => saveTemplate(templates[idx])}>Сохранить</button>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </>
        )}

        {!loadFailed && tab === 'settings' && (
          !salon ? (
            <p className="empty-block">Данные отсутствуют</p>
          ) : (
            <form className="form" onSubmit={saveSalon} style={{ maxWidth: 560 }}>
              <SectionToolbar title="Настройки салона" />
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
              {salonDirty && <button className="btn btn-primary" type="submit">Сохранить</button>}
            </form>
          )
        )}
      </main>
    </div>
  );
}
