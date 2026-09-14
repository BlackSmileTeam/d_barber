import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage, useModal } from '../context/ModalContext';

export default function CabinetPage() {
  const { auth } = useAuth();
  const [items, setItems] = useState([]);
  const { show } = useModal();

  const load = () => api.get('/appointments/mine').then((r) => setItems(r.data));

  useEffect(() => {
    if (auth?.role === 'Client') {
      load().catch((e) => show({ title: 'Ошибка', message: apiErrorMessage(e) }));
    }
  }, [auth, show]);

  if (!auth) return <Navigate to="/login" replace />;
  if (auth.role !== 'Client') return <Navigate to="/" replace />;

  const upcoming = items.filter((a) => a.status !== 'Cancelled' && new Date(a.startAtUtc) >= new Date());
  const history = items.filter((a) => a.status === 'Cancelled' || new Date(a.startAtUtc) < new Date());

  const cancel = async (id) => {
    try {
      await api.post(`/appointments/${id}/cancel`);
      show({ title: 'Запись отменена', message: 'Статус обновлён. Админ увидит отмену.' });
      await load();
    } catch (e) {
      show({ title: 'Ошибка', message: apiErrorMessage(e) });
    }
  };

  const reschedule = async (id) => {
    const date = window.prompt('Новая дата (YYYY-MM-DD)'); // will replace - user asked no alert; use modal form instead
    if (!date) return;
    try {
      const appt = items.find((a) => a.id === id);
      const slotsRes = await api.get('/appointments/slots', { params: { serviceId: appt.serviceId, date } });
      const slots = slotsRes.data.slotsUtc || [];
      if (!slots.length) {
        show({ title: 'Нет слотов', message: 'На выбранную дату свободного времени нет.' });
        return;
      }
      show({
        title: 'Выберите новое время',
        message: 'Нажмите слот для переноса',
        actions: slots.slice(0, 8).map((s) => ({
          label: new Date(s).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' }),
          keepOpen: false,
          onClick: async () => {
            try {
              await api.post(`/appointments/${id}/reschedule`, { startAtUtc: s });
              show({ title: 'Запись перенесена', message: 'Статус «Перенесена» отмечен для админа.' });
              await load();
            } catch (e) {
              show({ title: 'Ошибка', message: apiErrorMessage(e) });
            }
          },
        })).concat([{ label: 'Отмена' }]),
      });
    } catch (e) {
      show({ title: 'Ошибка', message: apiErrorMessage(e) });
    }
  };

  // Replace prompt with modal for date
  const askReschedule = (id) => {
    const appt = items.find((a) => a.id === id);
    let dateValue = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
    show({
      title: 'Перенос записи',
      message: 'Укажите новую дату, затем выберите время.',
      actions: [
        {
          label: 'Продолжить',
          primary: true,
          keepOpen: true,
          onClick: async () => {
            const input = document.getElementById('reschedule-date');
            dateValue = input?.value || dateValue;
            try {
              const slotsRes = await api.get('/appointments/slots', { params: { serviceId: appt.serviceId, date: dateValue } });
              const slots = slotsRes.data.slotsUtc || [];
              if (!slots.length) {
                show({ title: 'Нет слотов', message: 'На выбранную дату свободного времени нет.' });
                return;
              }
              show({
                title: 'Выберите время',
                actions: slots.slice(0, 10).map((s) => ({
                  label: new Date(s).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' }),
                  onClick: async () => {
                    try {
                      await api.post(`/appointments/${id}/reschedule`, { startAtUtc: s });
                      show({ title: 'Запись перенесена', message: 'В админке статус будет жёлтым.' });
                      await load();
                    } catch (e) {
                      show({ title: 'Ошибка', message: apiErrorMessage(e) });
                    }
                  },
                })).concat([{ label: 'Закрыть' }]),
              });
            } catch (e) {
              show({ title: 'Ошибка', message: apiErrorMessage(e) });
            }
          },
        },
        { label: 'Отмена' },
      ],
    });
    // inject date input after modal paints
    setTimeout(() => {
      const modal = document.querySelector('.modal');
      if (!modal || modal.querySelector('#reschedule-date')) return;
      const label = document.createElement('label');
      label.className = 'form';
      label.innerHTML = `Новая дата<input id="reschedule-date" type="date" value="${dateValue}" />`;
      label.style.marginBottom = '1rem';
      modal.insertBefore(label, modal.querySelector('.modal-actions'));
    }, 0);
  };

  void reschedule;

  const Row = ({ a }) => (
    <div className={`appt-row ${a.status === 'Cancelled' ? 'cancelled' : ''} ${a.status === 'Rescheduled' ? 'rescheduled' : ''}`}>
      <strong>{a.serviceName}</strong>
      <div>{new Date(a.startAtUtc).toLocaleString('ru-RU')} · {a.price} ₽</div>
      <div>Статус: {a.status === 'Cancelled' ? 'Отменена' : a.status === 'Rescheduled' ? 'Перенесена' : 'Подтверждена'}</div>
      {a.status !== 'Cancelled' && new Date(a.startAtUtc) >= new Date() && (
        <div className="appt-actions">
          <button type="button" className="btn btn-ghost" onClick={() => askReschedule(a.id)}>Перенести</button>
          <button type="button" className="btn btn-danger" onClick={() => cancel(a.id)}>Отменить</button>
        </div>
      )}
    </div>
  );

  return (
    <section className="section">
      <div className="container">
        <h2>Личный кабинет</h2>
        <p className="lead">{auth.name} · {auth.phone}</p>
        <h3 style={{ color: 'var(--copper)' }}>Предстоящие записи</h3>
        <div className="cabinet-list" style={{ marginBottom: '2rem' }}>
          {upcoming.length === 0 && <p className="lead">Пока нет предстоящих записей</p>}
          {upcoming.map((a) => <Row key={a.id} a={a} />)}
        </div>
        <h3 style={{ color: 'var(--copper)' }}>История</h3>
        <div className="cabinet-list">
          {history.length === 0 && <p className="lead">История пуста</p>}
          {history.map((a) => <Row key={a.id} a={a} />)}
        </div>
      </div>
    </section>
  );
}
