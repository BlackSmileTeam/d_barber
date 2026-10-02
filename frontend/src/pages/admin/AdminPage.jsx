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

const STATUS_LABELS = {
  Confirmed: 'Подтверждена',
  Rescheduled: 'Перенесена',
  Cancelled: 'Отменена',
  Completed: 'Выполнена',
  NoShow: 'Не пришёл',
};

const STATUS_OPTIONS = [
  { value: '', label: 'Все статусы' },
  { value: 'Confirmed', label: 'Подтверждена' },
  { value: 'Rescheduled', label: 'Перенесена' },
  { value: 'Completed', label: 'Выполнена' },
  { value: 'Cancelled', label: 'Отменена' },
  { value: 'NoShow', label: 'Не пришёл' },
];

const statusLabel = (s) => STATUS_LABELS[s] || s;

const WEEKDAYS_RU = ['пн', 'вт', 'ср', 'чт', 'пт', 'сб', 'вс'];

const toLocalYmd = (d) => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
};

const phoneDigits = (phone) => String(phone || '').replace(/\D/g, '');

const telegramPhoneHref = (phone) => {
  const digits = phoneDigits(phone);
  return digits ? `tg://resolve?phone=${digits}` : null;
};

const openTelegramByPhone = (phone) => {
  const href = telegramPhoneHref(phone);
  if (!href) return;
  const opened = window.open(href, '_blank', 'noopener,noreferrer');
  if (!opened) {
    window.location.href = href;
  }
};

const buildDonutSegments = (bars) => {
  const total = Math.max(1, bars.reduce((s, b) => s + b.count, 0));
  const r = 36;
  const c = 2 * Math.PI * r;
  let offset = 0;
  return bars.map((b) => {
    const len = (b.count / total) * c;
    const seg = { ...b, dash: `${len} ${c - len}`, offset: -offset };
    offset += len;
    return seg;
  });
};

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

const PencilIcon = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <path d="M4 20h4.5L19 9.5 14.5 5 4 15.5V20z" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />
    <path d="M12.5 7l4.5 4.5" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
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

const TgIcon = () => (
  <svg className="admin-tg-icon" width="14" height="14" viewBox="0 0 24 24" aria-hidden="true">
    <path
      fill="currentColor"
      d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.64 6.8-1.55 7.3c-.12.53-.43.66-.87.41l-2.4-1.77-1.16 1.12c-.13.13-.24.24-.49.24l.17-2.43 4.45-4.02c.19-.17-.04-.27-.3-.1l-5.5 3.46-2.37-.74c-.51-.16-.52-.51.11-.76l9.27-3.57c.43-.16.8.1.64.66z"
    />
  </svg>
);

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
  const [filterClient, setFilterClient] = useState('');
  const [filterService, setFilterService] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [calMonth, setCalMonth] = useState(() => {
    const n = new Date();
    return new Date(n.getFullYear(), n.getMonth(), 1);
  });
  const [selectedDay, setSelectedDay] = useState(() => toLocalYmd(new Date()));
  const [createDraft, setCreateDraft] = useState({ clientId: '', serviceId: '', slot: '', slots: [] });
  const [showCreate, setShowCreate] = useState(false);
  const [createBusy, setCreateBusy] = useState(false);
  const [reschedule, setReschedule] = useState(null); // { id, serviceId, date, slots, slot }
  const [statusBusy, setStatusBusy] = useState(null);
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
      .catch((err) => {
        if (err?.response?.status === 401) return;
        setLoadFailed(true);
      });
  };

  useEffect(() => {
    reload();
  }, [auth]);

  const salonDirty = useMemo(() => {
    if (!salon || !salonBaseline) return false;
    return stableJson(salon) !== stableJson(salonBaseline);
  }, [salon, salonBaseline]);

  const statusClass = (s) => (s || '').toLowerCase();

  // All hooks must run before any early return (React #300 on logout / 401).
  const filteredAppointments = useMemo(() => {
    const clientQ = filterClient.trim().toLowerCase();
    return appointments.filter((a) => {
      if (filterStatus && a.status !== filterStatus) return false;
      if (filterService && a.serviceId !== filterService) return false;
      if (clientQ) {
        const hay = `${a.clientName || ''} ${a.clientPhone || ''}`.toLowerCase();
        if (!hay.includes(clientQ)) return false;
      }
      return true;
    });
  }, [appointments, filterClient, filterService, filterStatus]);

  const monthChart = useMemo(() => {
    if (!stats) return [];
    return [
      {
        key: 'prev',
        label: stats.previousMonth?.label || 'Прошлый',
        count: stats.previousMonth?.count ?? 0,
        sum: Number(stats.previousMonth?.sum || 0),
      },
      {
        key: 'cur',
        label: stats.currentMonth?.label || 'Текущий',
        count: stats.currentMonth?.count ?? 0,
        sum: Number(stats.currentMonth?.sum || 0),
      },
    ];
  }, [stats]);

  const statusBars = useMemo(() => {
    const rows = stats?.byStatus || [];
    const total = Math.max(1, rows.reduce((s, r) => s + (r.count || 0), 0));
    return rows
      .map((r) => ({
        key: r.status,
        label: statusLabel(r.status),
        count: r.count || 0,
        pct: Math.round(((r.count || 0) / total) * 100),
        cls: statusClass(r.status),
      }))
      .filter((b) => b.count > 0)
      .sort((a, b) => b.count - a.count);
  }, [stats]);

  const donutSegments = useMemo(() => buildDonutSegments(statusBars), [statusBars]);
  const donutTotal = useMemo(
    () => statusBars.reduce((s, b) => s + b.count, 0),
    [statusBars],
  );

  const appointmentsByDay = useMemo(() => {
    const map = new Map();
    for (const a of filteredAppointments) {
      const key = toLocalYmd(new Date(a.startAtUtc));
      if (!map.has(key)) map.set(key, []);
      map.get(key).push(a);
    }
    for (const list of map.values()) {
      list.sort((x, y) => new Date(x.startAtUtc) - new Date(y.startAtUtc));
    }
    return map;
  }, [filteredAppointments]);

  const calendarCells = useMemo(() => {
    const year = calMonth.getFullYear();
    const month = calMonth.getMonth();
    const first = new Date(year, month, 1);
    const startPad = (first.getDay() + 6) % 7; // Monday-first
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const prevDays = new Date(year, month, 0).getDate();
    const cells = [];
    for (let i = 0; i < startPad; i += 1) {
      const day = prevDays - startPad + i + 1;
      const date = new Date(year, month - 1, day);
      cells.push({ key: toLocalYmd(date), date, inMonth: false });
    }
    for (let d = 1; d <= daysInMonth; d += 1) {
      const date = new Date(year, month, d);
      cells.push({ key: toLocalYmd(date), date, inMonth: true });
    }
    while (cells.length % 7 !== 0) {
      const last = cells[cells.length - 1].date;
      const date = new Date(last.getFullYear(), last.getMonth(), last.getDate() + 1);
      cells.push({ key: toLocalYmd(date), date, inMonth: false });
    }
    return cells;
  }, [calMonth]);

  const dayAppointments = useMemo(
    () => appointmentsByDay.get(selectedDay) || [],
    [appointmentsByDay, selectedDay],
  );

  const statusTableAppointments = useMemo(() => {
    if (!filterStatus) return [];
    return [...filteredAppointments].sort(
      (a, b) => new Date(b.startAtUtc) - new Date(a.startAtUtc),
    );
  }, [filteredAppointments, filterStatus]);

  const toggleStatusFilter = (status) => {
    setFilterStatus((prev) => (prev === status ? '' : status));
  };

  const todayYmd = toLocalYmd(new Date());

  if (!auth || auth.role !== 'Admin') {
    return <Navigate to="/admin/login" replace />;
  }

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

  const aboutPreview = mediaUrl(salon?.aboutImageUrl);

  const money = (n) => `${Number(n || 0).toLocaleString('ru-RU')} ₽`;

  const appointmentActions = (a) => {
    const items = [];
    if (a.status !== 'Completed' && a.status !== 'Cancelled' && a.status !== 'NoShow') {
      items.push({ key: 'Completed', label: 'Выполнено', danger: false, run: () => setAppointmentStatus(a, 'Completed') });
    }
    if (a.status !== 'Cancelled' && a.status !== 'NoShow' && a.status !== 'Completed') {
      items.push({ key: 'Rescheduled', label: 'Перенесено', danger: false, run: () => openReschedule(a) });
    }
    if (a.status !== 'Cancelled') {
      items.push({ key: 'Cancelled', label: 'Отменено', danger: true, run: () => setAppointmentStatus(a, 'Cancelled') });
    }
    if (a.status !== 'NoShow' && a.status !== 'Completed' && a.status !== 'Cancelled') {
      items.push({ key: 'NoShow', label: 'Не пришёл', danger: false, run: () => setAppointmentStatus(a, 'NoShow') });
    }
    return items;
  };

  const setAppointmentStatus = async (appt, status, startAtUtc) => {
    setStatusBusy(appt.id);
    try {
      const { data } = await api.post(`/appointments/${appt.id}/admin-status`, {
        status,
        startAtUtc: startAtUtc || null,
      });
      setAppointments((prev) => prev.map((x) => (x.id === appt.id ? { ...x, ...data } : x)));
      setReschedule(null);
      reload();
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    } finally {
      setStatusBusy(null);
    }
  };

  const openReschedule = async (appt) => {
    const local = new Date(appt.startAtUtc);
    const y = local.getFullYear();
    const m = String(local.getMonth() + 1).padStart(2, '0');
    const d = String(local.getDate()).padStart(2, '0');
    const date = `${y}-${m}-${d}`;
    setReschedule({ id: appt.id, serviceId: appt.serviceId, date, slots: [], slot: '', appt });
    try {
      const { data } = await api.get('/appointments/slots', { params: { serviceId: appt.serviceId, date } });
      setReschedule((prev) => prev && prev.id === appt.id
        ? { ...prev, slots: data.slotsUtc || data.SlotsUtc || [] }
        : prev);
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const loadRescheduleSlots = async (date) => {
    if (!reschedule) return;
    setReschedule((prev) => ({ ...prev, date, slots: [], slot: '' }));
    try {
      const { data } = await api.get('/appointments/slots', {
        params: { serviceId: reschedule.serviceId, date },
      });
      setReschedule((prev) => prev
        ? { ...prev, date, slots: data.slotsUtc || data.SlotsUtc || [], slot: '' }
        : prev);
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    }
  };

  const loadCreateSlots = async (serviceId, date) => {
    if (!serviceId || !date) {
      setCreateDraft((prev) => ({ ...prev, serviceId: serviceId || prev.serviceId, slots: [], slot: '' }));
      return;
    }
    try {
      const { data } = await api.get('/appointments/slots', { params: { serviceId, date } });
      setCreateDraft((prev) => ({
        ...prev,
        serviceId,
        slots: data.slotsUtc || data.SlotsUtc || [],
        slot: '',
      }));
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
      setCreateDraft((prev) => ({ ...prev, serviceId, slots: [], slot: '' }));
    }
  };

  const openCreateForDay = () => {
    setShowCreate(true);
    setCreateDraft({ clientId: '', serviceId: '', slot: '', slots: [] });
  };

  const createAppointment = async () => {
    if (!createDraft.clientId || !createDraft.serviceId || !createDraft.slot) return;
    setCreateBusy(true);
    try {
      await api.post('/appointments/admin', {
        clientId: createDraft.clientId,
        serviceId: createDraft.serviceId,
        startAtUtc: createDraft.slot,
      });
      setShowCreate(false);
      setCreateDraft({ clientId: '', serviceId: '', slot: '', slots: [] });
      show({ title: 'Запись создана', message: 'Клиент записан на выбранное время.' });
      reload();
    } catch (err) {
      show({ title: 'Ошибка', message: apiErrorMessage(err) });
    } finally {
      setCreateBusy(false);
    }
  };

  const shiftCalMonth = (delta) => {
    setCalMonth((prev) => new Date(prev.getFullYear(), prev.getMonth() + delta, 1));
  };

  const selectCalendarDay = (ymd) => {
    setSelectedDay(ymd);
    setShowCreate(false);
    setCreateDraft({ clientId: '', serviceId: '', slot: '', slots: [] });
    const [y, m] = ymd.split('-').map(Number);
    setCalMonth((prev) => {
      if (prev.getFullYear() === y && prev.getMonth() === m - 1) return prev;
      return new Date(y, m - 1, 1);
    });
  };

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
    const setField = (patch) => (isNew ? setNewService({ ...svc, ...patch }) : patchService(idx, patch));
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
        <div className="form admin-service-form" style={{ marginTop: '.75rem' }}>
          <label>Название<input value={svc.name} onChange={(e) => setField({ name: e.target.value })} /></label>
          <div className="admin-service-body">
            <label className="admin-service-desc">
              Описание
              <textarea
                rows={6}
                value={svc.description || ''}
                onChange={(e) => setField({ description: e.target.value })}
              />
            </label>
            <div className="admin-service-meta">
              <label>Цена, ₽<input type="number" value={svc.price} onChange={(e) => setField({ price: e.target.value })} /></label>
              <label>Длительность, мин<input type="number" value={svc.durationMinutes} onChange={(e) => setField({ durationMinutes: e.target.value })} /></label>
              <label>Порядок<input type="number" value={svc.sortOrder} onChange={(e) => setField({ sortOrder: e.target.value })} /></label>
              <label className="admin-check">
                <input type="checkbox" checked={!!svc.isActive} onChange={(e) => setField({ isActive: e.target.checked })} />
                Активна
              </label>
            </div>
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

        {!loadFailed && tab === 'appointments' && (
          <>
            {stats && (
              <div className="admin-infographic panel">
                <div className="admin-chart-head">
                  <strong>Показатели</strong>
                  <span className="admin-chart-today">Сегодня: {stats.todayConfirmed} подтв.</span>
                </div>
                <div className="admin-donut-wrap">
                  <svg className="admin-donut" viewBox="0 0 100 100" role="img" aria-label="Распределение статусов">
                    <circle cx="50" cy="50" r="36" fill="none" stroke="var(--bg-soft)" strokeWidth="14" />
                    {donutSegments.length === 0 ? (
                      <circle cx="50" cy="50" r="36" fill="none" stroke="var(--line)" strokeWidth="14" />
                    ) : (
                      donutSegments.map((seg) => {
                        const active = filterStatus === seg.key;
                        const dimmed = filterStatus && !active;
                        return (
                          <circle
                            key={seg.key}
                            className={[
                              'admin-donut-seg',
                              active ? 'is-active' : '',
                              dimmed ? 'is-dimmed' : '',
                            ].filter(Boolean).join(' ')}
                            cx="50"
                            cy="50"
                            r="36"
                            fill="none"
                            strokeWidth={active ? 16 : 14}
                            strokeDasharray={seg.dash}
                            strokeDashoffset={seg.offset}
                            transform="rotate(-90 50 50)"
                            role="button"
                            tabIndex={0}
                            aria-pressed={active}
                            aria-label={`${seg.label}: ${seg.count}`}
                            onClick={() => toggleStatusFilter(seg.key)}
                            onKeyDown={(e) => {
                              if (e.key === 'Enter' || e.key === ' ') {
                                e.preventDefault();
                                toggleStatusFilter(seg.key);
                              }
                            }}
                            style={{
                              stroke: seg.cls === 'confirmed' ? 'var(--ok)'
                                : seg.cls === 'cancelled' || seg.cls === 'noshow' ? 'var(--danger)'
                                  : seg.cls === 'rescheduled' ? 'var(--warn)'
                                    : 'var(--copper)',
                            }}
                          />
                        );
                      })
                    )}
                    <circle
                      className="admin-donut-center"
                      cx="50"
                      cy="50"
                      r="26"
                      role="button"
                      tabIndex={0}
                      aria-label="Сбросить фильтр статуса"
                      onClick={() => setFilterStatus('')}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          setFilterStatus('');
                        }
                      }}
                      style={{ cursor: filterStatus ? 'pointer' : 'default' }}
                    />
                    <text className="admin-donut-value" x="50" y="48" pointerEvents="none">{donutTotal}</text>
                    <text className="admin-donut-label" x="50" y="62" pointerEvents="none">записей</text>
                  </svg>
                  <div className="admin-donut-side">
                    {monthChart.map((m) => (
                      <div key={m.key} className="admin-donut-month">
                        <span>{m.label}</span>
                        <strong>{m.count} · {money(m.sum)}</strong>
                      </div>
                    ))}
                    <div className="admin-chart-legend">
                      <button
                        type="button"
                        className={`admin-chart-legend-item admin-chart-legend-btn${!filterStatus ? ' is-active' : ''}`}
                        onClick={() => setFilterStatus('')}
                      >
                        Все
                      </button>
                      {statusBars.map((b) => {
                        const active = filterStatus === b.key;
                        return (
                          <button
                            key={b.key}
                            type="button"
                            className={[
                              'admin-chart-legend-item',
                              'admin-chart-legend-btn',
                              active ? 'is-active' : '',
                              filterStatus && !active ? 'is-dimmed' : '',
                            ].filter(Boolean).join(' ')}
                            aria-pressed={active}
                            onClick={() => toggleStatusFilter(b.key)}
                          >
                            <i className={`admin-chart-dot ${b.cls}`} />
                            {b.label} {b.count}
                          </button>
                        );
                      })}
                      <span className="admin-chart-legend-item muted">
                        отмены/неявки: {(stats.cancelledTotal || 0) + (stats.noShowTotal || 0)}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {filterStatus && (
              <div className="panel admin-status-table">
                <div className="admin-status-table-head">
                  <strong className="admin-card-title">
                    Записи · {statusLabel(filterStatus)}
                    <span className="admin-status-table-count"> ({statusTableAppointments.length})</span>
                  </strong>
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs"
                    onClick={() => setFilterStatus('')}
                  >
                    Все
                  </button>
                </div>
                {statusTableAppointments.length === 0 ? (
                  <p className="empty-block" style={{ margin: 0 }}>Нет записей с этим статусом</p>
                ) : (
                  <div className="admin-status-table-scroll">
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Клиент</th>
                          <th>Услуга</th>
                          <th>Дата и время</th>
                          <th>Статус</th>
                          <th>Действия</th>
                        </tr>
                      </thead>
                      <tbody>
                        {statusTableAppointments.map((a) => {
                          const actions = appointmentActions(a);
                          return (
                            <tr key={a.id}>
                              <td>
                                <strong>{a.clientName}</strong>
                                <div className="admin-status-table-sub">{a.clientPhone}</div>
                              </td>
                              <td>
                                {a.serviceName}
                                <div className="admin-status-table-sub">{money(a.price)}</div>
                              </td>
                              <td>
                                {new Date(a.startAtUtc).toLocaleString('ru-RU', {
                                  day: '2-digit',
                                  month: '2-digit',
                                  year: 'numeric',
                                  hour: '2-digit',
                                  minute: '2-digit',
                                })}
                              </td>
                              <td>
                                <span className={`badge ${statusClass(a.status)}`}>
                                  {statusLabel(a.status)}
                                </span>
                              </td>
                              <td>
                                {actions.length === 0 ? (
                                  <span className="muted">—</span>
                                ) : (
                                  <div className="admin-actions">
                                    {actions.map((item) => (
                                      <button
                                        key={item.key}
                                        type="button"
                                        className="btn btn-ghost btn-xs"
                                        style={item.danger
                                          ? { color: 'var(--danger)', borderColor: 'var(--danger)' }
                                          : undefined}
                                        disabled={statusBusy === a.id}
                                        onClick={item.run}
                                      >
                                        {item.label === 'Отменено' ? 'Удалить' : item.label}
                                      </button>
                                    ))}
                                  </div>
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            <div className="admin-filters panel">
              <label>
                Клиент
                <input
                  type="search"
                  placeholder="Имя или телефон"
                  value={filterClient}
                  onChange={(e) => setFilterClient(e.target.value)}
                />
              </label>
              <label>
                Услуга
                <select value={filterService} onChange={(e) => setFilterService(e.target.value)}>
                  <option value="">Все услуги</option>
                  {services.map((s) => (
                    <option key={s.id} value={s.id}>{s.name}</option>
                  ))}
                </select>
              </label>
              <label>
                Статус
                <select value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)}>
                  {STATUS_OPTIONS.map((o) => (
                    <option key={o.value || 'all'} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </label>
              {(filterClient || filterService || filterStatus) && (
                <button
                  type="button"
                  className="btn btn-ghost btn-xs"
                  onClick={() => {
                    setFilterClient('');
                    setFilterService('');
                    setFilterStatus('');
                  }}
                >
                  Сбросить
                </button>
              )}
            </div>

            <div className="admin-calendar-wrap">
              <div className="panel admin-calendar">
                <div className="admin-cal-head">
                  <h2 className="admin-cal-title">
                    {calMonth.toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' })}
                  </h2>
                  <div className="admin-cal-nav">
                    <button type="button" className="btn btn-ghost btn-xs" onClick={() => shiftCalMonth(-1)} aria-label="Предыдущий месяц">‹</button>
                    <button
                      type="button"
                      className="btn btn-ghost btn-xs"
                      onClick={() => {
                        const n = new Date();
                        setCalMonth(new Date(n.getFullYear(), n.getMonth(), 1));
                        setSelectedDay(toLocalYmd(n));
                      }}
                    >
                      Сегодня
                    </button>
                    <button type="button" className="btn btn-ghost btn-xs" onClick={() => shiftCalMonth(1)} aria-label="Следующий месяц">›</button>
                  </div>
                </div>
                <div className="admin-cal-weekdays">
                  {WEEKDAYS_RU.map((d) => (
                    <span key={d} className="admin-cal-weekday">{d}</span>
                  ))}
                </div>
                <div className="admin-cal-grid">
                  {calendarCells.map((cell) => {
                    const dayItems = appointmentsByDay.get(cell.key) || [];
                    const dots = dayItems.slice(0, 4);
                    return (
                      <button
                        key={cell.key}
                        type="button"
                        className={[
                          'admin-cal-day',
                          cell.inMonth ? '' : 'muted',
                          cell.key === selectedDay ? 'selected' : '',
                          cell.key === todayYmd ? 'today' : '',
                        ].filter(Boolean).join(' ')}
                        onClick={() => selectCalendarDay(cell.key)}
                      >
                        <span className="admin-cal-day-num">{cell.date.getDate()}</span>
                        {dayItems.length > 0 && (
                          <>
                            <span className="admin-cal-dots">
                              {dots.map((a) => (
                                <i key={a.id} className={`admin-cal-dot ${statusClass(a.status)}`} />
                              ))}
                            </span>
                            <span className="admin-cal-count">{dayItems.length}</span>
                          </>
                        )}
                      </button>
                    );
                  })}
                </div>
              </div>

              <div className="panel admin-day-panel">
                <div className="admin-day-panel-head">
                  <strong className="admin-card-title">
                    {new Date(`${selectedDay}T12:00:00`).toLocaleDateString('ru-RU', {
                      weekday: 'long', day: 'numeric', month: 'long',
                    })}
                  </strong>
                  <button type="button" className="btn btn-primary btn-xs" onClick={openCreateForDay}>
                    Создать запись
                  </button>
                </div>

                {dayAppointments.length === 0 ? (
                  <p className="empty-block" style={{ margin: 0 }}>На этот день записей нет</p>
                ) : (
                  <div className="admin-day-list">
                    {dayAppointments.map((a) => {
                      const actions = appointmentActions(a);
                      return (
                        <div key={a.id} className="admin-day-item">
                          <div className="admin-day-item-top">
                            <div>
                              <strong>
                                {new Date(a.startAtUtc).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}
                                {' · '}
                                {a.clientName}
                              </strong>
                              <p className="admin-day-item-meta">
                                {a.clientPhone} · {a.serviceName} · {money(a.price)}
                              </p>
                            </div>
                            <span className={`badge ${statusClass(a.status)}`}>{statusLabel(a.status)}</span>
                          </div>
                          {actions.length > 0 && (
                            <div className="admin-actions">
                              {actions.map((item) => (
                                <button
                                  key={item.key}
                                  type="button"
                                  className="btn btn-ghost btn-xs"
                                  style={item.danger ? { color: 'var(--danger)', borderColor: 'var(--danger)' } : undefined}
                                  disabled={statusBusy === a.id}
                                  onClick={item.run}
                                >
                                  {item.label === 'Отменено' ? 'Удалить' : item.label}
                                </button>
                              ))}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}

                {showCreate && (
                  <div className="admin-day-create form">
                    <strong className="admin-card-title">Новая запись</strong>
                    <label>
                      Клиент
                      <select
                        value={createDraft.clientId}
                        onChange={(e) => setCreateDraft((prev) => ({ ...prev, clientId: e.target.value }))}
                      >
                        <option value="">Выберите клиента</option>
                        {clients.map((c) => (
                          <option key={c.id} value={c.id}>{c.name} · {c.phone}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Услуга
                      <select
                        value={createDraft.serviceId}
                        onChange={(e) => loadCreateSlots(e.target.value, selectedDay)}
                      >
                        <option value="">Выберите услугу</option>
                        {services.filter((s) => s.isActive).map((s) => (
                          <option key={s.id} value={s.id}>{s.name}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Время
                      <select
                        value={createDraft.slot}
                        onChange={(e) => setCreateDraft((prev) => ({ ...prev, slot: e.target.value }))}
                        disabled={!createDraft.serviceId}
                      >
                        <option value="">Выберите слот</option>
                        {(createDraft.slots || []).map((s) => (
                          <option key={s} value={s}>
                            {new Date(s).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}
                          </option>
                        ))}
                      </select>
                    </label>
                    <div className="admin-actions">
                      <button
                        type="button"
                        className="btn btn-primary"
                        disabled={createBusy || !createDraft.clientId || !createDraft.serviceId || !createDraft.slot}
                        onClick={createAppointment}
                      >
                        Записать
                      </button>
                      <button
                        type="button"
                        className="btn btn-ghost"
                        onClick={() => {
                          setShowCreate(false);
                          setCreateDraft({ clientId: '', serviceId: '', slot: '', slots: [] });
                        }}
                      >
                        Отмена
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </>
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
          <div className="admin-section">
            <SectionToolbar title="Клиенты" />
            {clients.length === 0 ? (
              <p className="empty-block">Данные отсутствуют</p>
            ) : (
              <div className="admin-clients-list">
                {clients.map((c) => (
                  <div key={c.id} className="admin-client-row">
                    <div className="admin-client-main">
                      <div className="admin-client-line">
                        <strong>{c.name}</strong>
                        <span className="admin-key">{c.phone}</span>
                        {phoneDigits(c.phone) ? (
                          <a
                            className="admin-tg-badge"
                            href={telegramPhoneHref(c.phone)}
                            title="Открыть в Telegram"
                            aria-label="Открыть в Telegram"
                            onClick={(e) => {
                              e.preventDefault();
                              openTelegramByPhone(c.phone);
                            }}
                          >
                            <TgIcon />
                          </a>
                        ) : c.telegramLinked ? (
                          <span className="admin-tg-badge" title="Telegram привязан" aria-label="Telegram привязан">
                            <TgIcon />
                          </span>
                        ) : null}
                      </div>
                      <p className="admin-client-meta">
                        {new Date(c.createdAtUtc).toLocaleDateString('ru-RU')}
                        {c.lastVisitAtUtc
                          ? ` · Последний визит: ${new Date(c.lastVisitAtUtc).toLocaleDateString('ru-RU')}`
                          : ' · без визитов'}
                      </p>
                    </div>
                    <div className="admin-client-reset">
                      <input
                        type="text"
                        value={resetDrafts[c.id] || ''}
                        onChange={(e) => setResetDrafts((prev) => ({ ...prev, [c.id]: e.target.value }))}
                        placeholder="Новый пароль"
                      />
                      <button type="button" className="btn btn-ghost btn-xs" onClick={() => resetPassword(c, true)}>
                        Генерация
                      </button>
                      <button
                        type="button"
                        className="btn btn-ghost btn-xs btn-save-password"
                        onClick={() => resetPassword(c, false)}
                        disabled={!(resetDrafts[c.id] || '').trim()}
                      >
                        Сохранить
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {!loadFailed && tab === 'templates' && (
          <div className="admin-templates">
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
                <div className="form admin-templates-form" style={{ marginTop: '.75rem' }}>
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
                  <div className="form admin-templates-form" style={{ marginTop: '.75rem' }}>
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
          </div>
        )}

        {!loadFailed && tab === 'settings' && (
          <div className="admin-section">
            {!salon ? (
              <p className="empty-block">Данные отсутствуют</p>
            ) : (
              <>
                <SectionToolbar title="Настройки салона" />
                <form className="form admin-salon-form" onSubmit={saveSalon}>
                  <div className="admin-salon-top">
                    <label className="admin-salon-photo" title={uploading ? 'Загрузка…' : 'Изменить фото'}>
                      {aboutPreview ? (
                        <img src={aboutPreview} alt="Фото салона" />
                      ) : (
                        <span className="admin-salon-photo-empty">Нет фото</span>
                      )}
                      <span className="admin-salon-photo-edit" aria-hidden="true">
                        <PencilIcon />
                      </span>
                      <input
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        hidden
                        disabled={uploading}
                        onChange={uploadAboutPhoto}
                      />
                    </label>
                    <div className="admin-salon-fields">
                      <label>Бренд<input value={salon.brandName} onChange={(e) => setSalon({ ...salon, brandName: e.target.value })} /></label>
                      <label>Название салона<input value={salon.salonName} onChange={(e) => setSalon({ ...salon, salonName: e.target.value })} /></label>
                      <label>Телефон<input value={salon.phone || ''} onChange={(e) => setSalon({ ...salon, phone: e.target.value })} /></label>
                      <label>Instagram (ссылка)<input value={salon.instagramUrl || ''} onChange={(e) => setSalon({ ...salon, instagramUrl: e.target.value })} placeholder="https://www.instagram.com/…" /></label>
                      <label>Telegram (ссылка)<input value={salon.telegramUrl || ''} onChange={(e) => setSalon({ ...salon, telegramUrl: e.target.value })} placeholder="https://t.me/…" /></label>
                    </div>
                  </div>
                  <div className="admin-salon-below">
                    <label>Адрес<input value={salon.address} onChange={(e) => setSalon({ ...salon, address: e.target.value })} /></label>
                    <label>
                      Обо мне
                      <textarea
                        rows={8}
                        value={salon.aboutHtml || ''}
                        onChange={(e) => setSalon({ ...salon, aboutHtml: e.target.value })}
                        placeholder="Текст блока «Обо мне» (абзацы через пустую строку)"
                      />
                    </label>
                  </div>
                  {salonDirty && <button className="btn btn-primary" type="submit">Сохранить</button>}
                </form>
              </>
            )}
          </div>
        )}
      </main>

      {reschedule && (
        <div
          className="modal-backdrop"
          onClick={() => setReschedule(null)}
          role="presentation"
        >
          <div
            className="modal admin-reschedule-modal"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-labelledby="admin-reschedule-title"
          >
            <h3 id="admin-reschedule-title">Перенос записи</h3>
            <p>
              {reschedule.appt?.clientName}
              {reschedule.appt?.serviceName ? ` · ${reschedule.appt.serviceName}` : ''}
            </p>
            <div className="form modal-form">
              <label>
                Новая дата
                <input
                  type="date"
                  value={reschedule.date}
                  onChange={(e) => loadRescheduleSlots(e.target.value)}
                />
              </label>
              <label>
                Время
                <select
                  value={reschedule.slot}
                  onChange={(e) => setReschedule((prev) => ({ ...prev, slot: e.target.value }))}
                >
                  <option value="">Выберите слот</option>
                  {(reschedule.slots || []).map((s) => (
                    <option key={s} value={s}>
                      {new Date(s).toLocaleString('ru-RU', {
                        day: '2-digit',
                        month: '2-digit',
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </option>
                  ))}
                </select>
              </label>
              {(reschedule.slots || []).length === 0 && reschedule.date && (
                <p className="admin-reschedule-empty">На эту дату свободных слотов нет</p>
              )}
            </div>
            <div className="modal-actions">
              <button
                type="button"
                className="btn btn-primary"
                disabled={!reschedule.slot || statusBusy === reschedule.id}
                onClick={() => setAppointmentStatus(reschedule.appt, 'Rescheduled', reschedule.slot)}
              >
                Сохранить перенос
              </button>
              <button type="button" className="btn btn-ghost" onClick={() => setReschedule(null)}>
                Отмена
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
