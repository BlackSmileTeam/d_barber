import { useEffect, useRef, useState } from 'react';
import api, { mediaUrl } from '../services/api';

export default function PortfolioPage() {
  const [items, setItems] = useState([]);
  const [lightbox, setLightbox] = useState(null);
  const [failed, setFailed] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const trackRef = useRef(null);

  useEffect(() => {
    api.get('/portfolio')
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

  const scrollBy = (dir) => {
    const el = trackRef.current;
    if (!el) return;
    const amount = Math.min(el.clientWidth * 0.8, 320);
    el.scrollBy({ left: dir * amount, behavior: 'smooth' });
  };

  const empty = failed || (loaded && items.length === 0);

  return (
    <section className="section section-portfolio reveal" id="portfolio">
      <div className="container">
        <div className="portfolio-head">
          <div className="section-head">
            <span className="section-index" aria-hidden="true">03</span>
            <div>
              <h2>Портфолио</h2>
              <p className="lead section-kicker">Работы, которые говорят сами за себя.</p>
            </div>
          </div>
          {!empty && (
            <div className="carousel-nav">
              <button type="button" className="btn btn-ghost carousel-btn" onClick={() => scrollBy(-1)} aria-label="Назад">‹</button>
              <button type="button" className="btn btn-ghost carousel-btn" onClick={() => scrollBy(1)} aria-label="Вперёд">›</button>
            </div>
          )}
        </div>
        <div className="section-rule" aria-hidden="true" />
        {empty ? (
          <p className="empty-block">Данные отсутствуют</p>
        ) : (
          <div className="portfolio-carousel" ref={trackRef}>
            {items.map((item, i) => (
              <article
                key={item.id}
                className="portfolio-item reveal-child"
                style={{ '--reveal-delay': `${0.06 + i * 0.05}s` }}
              >
                <button type="button" className="portfolio-thumb" onClick={() => setLightbox(item)}>
                  <img src={mediaUrl(item.imageUrl)} alt={item.title} />
                  <span className="portfolio-shine" aria-hidden="true" />
                </button>
                <div className="cap">
                  <strong>{item.title}</strong>
                  {item.serviceName || ''}
                </div>
              </article>
            ))}
          </div>
        )}
      </div>

      {lightbox && (
        <div className="lightbox-backdrop" onClick={() => setLightbox(null)} role="presentation">
          <figure className="lightbox" onClick={(e) => e.stopPropagation()}>
            <img src={mediaUrl(lightbox.imageUrl)} alt={lightbox.title} />
            <figcaption>
              <strong>{lightbox.title}</strong>
              {lightbox.serviceName || ''}
            </figcaption>
            <button type="button" className="btn btn-ghost lightbox-close" onClick={() => setLightbox(null)}>Закрыть</button>
          </figure>
        </div>
      )}
    </section>
  );
}
