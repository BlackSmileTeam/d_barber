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
    <section className="section section-news" id="news">
      <div className="container">
        <h2>Новости</h2>
        {empty ? (
          <p className="empty-block">Данные отсутствуют</p>
        ) : (
          <div className="news-list">
            {items.map((n) => (
              <article key={n.id} className="news-card">
                {n.coverImageUrl ? <img src={n.coverImageUrl} alt="" /> : <div />}
                <div>
                  <div className="date">{n.publishAtUtc ? new Date(n.publishAtUtc).toLocaleDateString('ru-RU') : ''}</div>
                  <h3 style={{ margin: '.35rem 0' }}>{n.title}</h3>
                  <p className="lead" style={{ margin: 0 }}>{n.body}</p>
                </div>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
