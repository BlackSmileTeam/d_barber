import { useEffect, useMemo, useState } from 'react';
import api from '../services/api';

function IconComb({ className }) {
  return (
    <svg className={className} viewBox="0 0 24 24" aria-hidden="true">
      <path fill="currentColor" d="M4.5 3.5h2v17h-2v-17zm3.5 0h1.6v17H8V3.5zm3.2 0H13v17h-1.3V3.5zm3.3 0h1.6v17H14.5V3.5zm3.2 0H19.5c.8 0 1.5.7 1.5 1.5v14c0 .8-.7 1.5-1.5 1.5H17.7V3.5z" />
    </svg>
  );
}

function IconPin({ className }) {
  return (
    <svg className={className} viewBox="0 0 24 24" aria-hidden="true">
      <path fill="currentColor" d="M12 2C8.1 2 5 5.1 5 9c0 5.2 7 13 7 13s7-7.8 7-13c0-3.9-3.1-7-7-7zm0 9.5A2.5 2.5 0 1 1 12 6.5a2.5 2.5 0 0 1 0 5z" />
    </svg>
  );
}

function IconInstagram({ className }) {
  return (
    <svg className={className} viewBox="0 0 24 24" aria-hidden="true">
      <path fill="currentColor" d="M7 2h10a5 5 0 0 1 5 5v10a5 5 0 0 1-5 5H7a5 5 0 0 1-5-5V7a5 5 0 0 1 5-5zm0 2a3 3 0 0 0-3 3v10a3 3 0 0 0 3 3h10a3 3 0 0 0 3-3V7a3 3 0 0 0-3-3H7zm5 3.5A4.5 4.5 0 1 1 7.5 12 4.5 4.5 0 0 1 12 7.5zm0 2A2.5 2.5 0 1 0 14.5 12 2.5 2.5 0 0 0 12 9.5zm5.2-3.3a1.1 1.1 0 1 1-1.1 1.1 1.1 1.1 0 0 1 1.1-1.1z" />
    </svg>
  );
}

function IconTelegram({ className }) {
  return (
    <svg className={className} viewBox="0 0 24 24" aria-hidden="true">
      <path fill="currentColor" d="M21.7 4.3 2.9 11.5c-1.3.5-1.3 1.2-.2 1.5l4.8 1.5 1.8 5.6c.2.7.1.9.8.9.5 0 .7-.2 1-.5l2.3-2.2 4.8 3.5c.9.5 1.5.2 1.7-.8L22.9 5.5c.3-1.2-.4-1.7-1.2-1.2zM9.4 14.7l-.2 3.1 1.6-2.1 6.6-6c.3-.2.5 0 .3.2l-5.4 4.9-2.9-.1z" />
    </svg>
  );
}

export default function ContactsPage() {
  const [salon, setSalon] = useState(null);
  const [failed, setFailed] = useState(false);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    api.get('/salon')
      .then((r) => {
        setSalon(r.data);
        setFailed(false);
      })
      .catch(() => {
        setSalon(null);
        setFailed(true);
      })
      .finally(() => setLoaded(true));
  }, []);

  const mapSrc = useMemo(() => {
    if (!salon?.address) return '';
    const q = encodeURIComponent(salon.address);
    return `https://yandex.ru/map-widget/v1/?text=${q}&z=16`;
  }, [salon]);

  const openMap = () => {
    if (!salon?.address) return;
    window.open(`https://yandex.ru/maps/?text=${encodeURIComponent(salon.address)}`, '_blank');
  };

  const route = () => {
    if (!salon?.address) return;
    window.open(`https://yandex.ru/maps/?rtext=~${encodeURIComponent(salon.address)}&rtt=auto`, '_blank');
  };

  const empty = failed || (loaded && !salon);

  return (
    <section className="section section-contacts" id="contacts">
      <div className="container">
        <h2>Контакты</h2>
        {empty ? (
          <p className="empty-block">Данные отсутствуют</p>
        ) : (
          <div className="contacts-grid">
            <div>
              <p className="contact-line contact-name">
                <IconComb className="contact-icon" />
                <span>{salon?.salonName || 'Данные отсутствуют'}</span>
              </p>
              <p className="contact-line">
                <IconPin className="contact-icon" />
                <span>{salon?.address || 'Данные отсутствуют'}</span>
              </p>
              {salon?.phone && <p className="lead">{salon.phone}</p>}
              {(salon?.instagramUrl || salon?.telegramUrl) && (
                <div className="social-icons">
                  {salon?.instagramUrl && (
                    <a
                      href={salon.instagramUrl}
                      target="_blank"
                      rel="noreferrer"
                      aria-label="Instagram"
                      title="Instagram"
                    >
                      <IconInstagram />
                    </a>
                  )}
                  {salon?.telegramUrl && (
                    <a
                      href={salon.telegramUrl}
                      target="_blank"
                      rel="noreferrer"
                      aria-label="Telegram"
                      title="Telegram"
                    >
                      <IconTelegram />
                    </a>
                  )}
                </div>
              )}
              <div className="contact-actions">
                <button type="button" className="btn btn-primary" onClick={openMap} disabled={!salon?.address}>Открыть в Яндекс.Картах</button>
                <button type="button" className="btn btn-ghost" onClick={route} disabled={!salon?.address}>Построить маршрут</button>
              </div>
            </div>
            <div className="map-frame">
              {mapSrc && <iframe title="Яндекс карта" src={mapSrc} loading="lazy" />}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
