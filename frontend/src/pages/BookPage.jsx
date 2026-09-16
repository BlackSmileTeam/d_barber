import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../services/api';
import { useAuth } from '../context/AuthContext';
import { apiErrorMessage, useModal } from '../context/ModalContext';

function toLocalInputDate(d = new Date()) {
  return d.toISOString().slice(0, 10);
}

function googleCalendarUrl(appointment, salon) {
  const start = new Date(appointment.startAtUtc);
  const end = new Date(appointment.endAtUtc);
  const fmt = (dt) => dt.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
  const params = new URLSearchParams({
    action: 'TEMPLATE',
    text: `${appointment.serviceName} — D_Barber`,
    dates: `${fmt(start)}/${fmt(end)}`,
    details: `${salon?.salonName || ''}. ${appointment.serviceName}`,
    location: salon?.address || '',
  });
  return `https://calendar.google.com/calendar/render?${params.toString()}`;
}

function ServiceList({ services, service, onSelect, selectable }) {
  return (
    <div className="service-list">
      {services.map((s) => (
        <button
          key={s.id}
          type="button"
          className={`service-row ${selectable && service?.id === s.id ? 'selected' : ''} ${selectable ? '' : 'readonly'}`}
          onClick={() => selectable && onSelect?.(s)}
          disabled={!selectable}
        >
          <div className="service-main">
            <strong>{s.name}</strong>
            <div className="meta">{s.description}</div>
          </div>
          <div className="meta">{s.durationMinutes} мин</div>
          <div className="price">{s.price} ₽</div>
        </button>
      ))}
    </div>
  );
}

export default function BookPage() {
  const [step, setStep] = useState(1);
  const [services, setServices] = useState([]);
  const [service, setService] = useState(null);
  const [date, setDate] = useState(toLocalInputDate(new Date(Date.now() + 86400000)));
  const [slots, setSlots] = useState([]);
  const [slot, setSlot] = useState(null);
  const [salon, setSalon] = useState(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const { auth } = useAuth();
  const { show } = useModal();
  const navigate = useNavigate();
  const canBook = auth?.role === 'Client';

  useEffect(() => {
    Promise.all([api.get('/services'), api.get('/salon')])
      .then(([s, salonRes]) => {
        setServices(s.data || []);
        setSalon(salonRes.data);
        setLoadFailed(false);
      })
      .catch(() => {
        setServices([]);
        setSalon(null);
        setLoadFailed(true);
      })
      .finally(() => setLoaded(true));
  }, []);

  useEffect(() => {
    if (!canBook) {
      setStep(1);
      setService(null);
      setSlot(null);
    }
  }, [canBook]);

  useEffect(() => {
    if (!canBook || !service || !date) return;
    api.get('/appointments/slots', { params: { serviceId: service.id, date } })
      .then((r) => setSlots(r.data.slotsUtc || []))
      .catch(() => setSlots([]));
  }, [canBook, service, date]);

  const slotLabels = useMemo(
    () => slots.map((s) => ({ utc: s, label: new Date(s).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' }) })),
    [slots],
  );

  const confirm = async () => {
    try {
      const { data } = await api.post('/appointments', {
        serviceId: service.id,
        startAtUtc: slot,
      });
      show({
        title: 'Запись подтверждена',
        details: [
          `Услуга: ${data.serviceName}`,
          `Дата: ${new Date(data.startAtUtc).toLocaleDateString('ru-RU')}`,
          `Время: ${new Date(data.startAtUtc).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}`,
          `Адрес: ${salon?.salonName}, ${salon?.address}`,
          `Сумма: ${data.price} ₽`,
        ],
        actions: [
          {
            label: 'Добавить в Google Календарь',
            primary: true,
            keepOpen: true,
            onClick: () => window.open(googleCalendarUrl(data, salon), '_blank'),
          },
          {
            label: 'Скачать для iPhone (.ics)',
            keepOpen: true,
            onClick: async () => {
              const res = await api.get(`/appointments/${data.id}/calendar.ics`, { responseType: 'blob' });
              const url = URL.createObjectURL(res.data);
              const a = document.createElement('a');
              a.href = url;
              a.download = 'dbarber-appointment.ics';
              a.click();
              URL.revokeObjectURL(url);
            },
          },
          { label: 'В личный кабинет', onClick: () => navigate('/cabinet') },
          { label: 'На главную', onClick: () => navigate('/#top') },
        ],
      });
      setStep(1);
      setService(null);
      setSlot(null);
    } catch (e) {
      show({ title: 'Не удалось записаться', message: apiErrorMessage(e) });
    }
  };

  return (
    <section className="section section-book reveal" id="book">
      <div className="container">
        <h2>Онлайн-запись</h2>
        <p className="lead section-kicker">Выбери услугу и удобное время.</p>

        {(loadFailed || (loaded && services.length === 0)) ? (
          <p className="empty-block">Данные отсутствуют</p>
        ) : (
          <>
        {canBook && (
          <div className="progress">
            <span className={step === 1 ? 'on' : ''}>1 Услуга</span>
            <span className={step === 2 ? 'on' : ''}>2 Дата</span>
            <span className={step === 3 ? 'on' : ''}>3 Подтверждение</span>
          </div>
        )}

        <div className="booking-stage">
          <div className="booking-stage-body">
            {!canBook && (
              <ServiceList services={services} selectable={false} />
            )}

            {canBook && step === 1 && (
              <ServiceList
                services={services}
                service={service}
                selectable
                onSelect={setService}
              />
            )}

            {canBook && step === 2 && (
              <div className="calendar-layout">
                <div className="panel panel-date">
                  <div className="panel-label">Дата</div>
                  <p className="service-summary">
                    {service?.name} · {service?.price} ₽ · {service?.durationMinutes} мин
                  </p>
                  <label className="form">
                    <span className="sr-only">Дата</span>
                    <input type="date" value={date} min={toLocalInputDate()} onChange={(e) => { setDate(e.target.value); setSlot(null); }} />
                  </label>
                </div>
                <div className="panel panel-slots">
                  <div className="panel-label">Время</div>
                  <div className="slots">
                    {slotLabels.length === 0 && <span className="muted">Нет свободных слотов</span>}
                    {slotLabels.map((s) => (
                      <button
                        key={s.utc}
                        type="button"
                        className={`slot ${slot === s.utc ? 'selected' : ''}`}
                        onClick={() => setSlot(s.utc)}
                      >
                        {s.label}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            )}

            {canBook && step === 3 && (
              <div className="panel panel-confirm">
                <div className="panel-label">Подтверждение</div>
                <div className="details">
                  <div>Услуга: {service?.name}</div>
                  <div>Дата: {new Date(slot).toLocaleDateString('ru-RU')}</div>
                  <div>Время: {new Date(slot).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}</div>
                  <div>Адрес: {salon?.salonName}, {salon?.address}</div>
                  <div>Сумма: {service?.price} ₽</div>
                </div>
              </div>
            )}
          </div>

          <div className="booking-stage-actions">
            {!canBook && (
              <>
                <Link className="btn btn-primary" to="/login">Войти</Link>
                <Link className="btn btn-ghost" to="/register">Зарегистрироваться</Link>
              </>
            )}
            {canBook && step === 1 && (
              <button type="button" className="btn btn-primary" disabled={!service} onClick={() => setStep(2)}>Далее</button>
            )}
            {canBook && step === 2 && (
              <>
                <button type="button" className="btn btn-ghost" onClick={() => setStep(1)}>Назад</button>
                <button type="button" className="btn btn-primary" disabled={!slot} onClick={() => setStep(3)}>Далее</button>
              </>
            )}
            {canBook && step === 3 && (
              <>
                <button type="button" className="btn btn-ghost" onClick={() => setStep(2)}>Назад</button>
                <button type="button" className="btn btn-primary" onClick={confirm}>Подтвердить запись</button>
              </>
            )}
          </div>
        </div>
          </>
        )}
      </div>
    </section>
  );
}
