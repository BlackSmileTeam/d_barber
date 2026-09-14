import { useEffect, useState } from 'react';
import api from '../services/api';
import { apiErrorMessage, useModal } from '../context/ModalContext';

export default function NewsPage() {
  const [items, setItems] = useState([]);
  const { show } = useModal();

  useEffect(() => {
    api.get('/news').then((r) => setItems(r.data)).catch((e) => show({ title: 'Ошибка', message: apiErrorMessage(e) }));
  }, [show]);

  return (
    <section className="section section-news" id="news">
      <div className="container">
        <h2>Новости</h2>
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
      </div>
    </section>
  );
}
