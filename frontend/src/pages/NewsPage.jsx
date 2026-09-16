import { useEffect, useState } from 'react';
import api from '../services/api';

export default function NewsPage() {
  const [items, setItems] = useState([]);
  const [failed, setFailed] = useState(false);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    api.get('/news')
      .then((r) => {
        setItems(r.data || []);
        setFailed(false);
      })
      .catch(() => {
        setItems([]);
        setFailed(true);
      })
      .finally(() => setLoaded(true));
  }, []);

  const empty = failed || (loaded && items.length === 0);

  return (
    <section className="section section-news reveal" id="news">
      <div className="container">
        <h2>Новости</h2>
        <p className="lead section-kicker">Что нового в D_Barber.</p>
        {empty ? (
          <p className="empty-block">Данные отсутствуют</p>
        ) : (
          <div className="news-list">
            {items.map((n) => (
              <article key={n.id} className="news-card">
                {n.coverImageUrl ? <img src={n.coverImageUrl} alt="" /> : null}
                <div className="news-copy">
                  <div className="date">{n.publishAtUtc ? new Date(n.publishAtUtc).toLocaleDateString('ru-RU') : ''}</div>
                  <h3>{n.title}</h3>
                  <p className="lead">{n.body}</p>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
