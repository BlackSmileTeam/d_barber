import { useEffect, useRef, useState, useCallback } from 'react';
import api, { mediaUrl } from '../services/api';

export default function PortfolioPage() {
  const [items, setItems] = useState([]);
  const [lightboxIndex, setLightboxIndex] = useState(null);
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

  const closeLightbox = useCallback(() => setLightboxIndex(null), []);
  const openLightbox = (index) => setLightboxIndex(index);

  const stepLightbox = useCallback((dir) => {
    setLightboxIndex((current) => {
      if (current == null || items.length === 0) return current;
      return (current + dir + items.length) % items.length;
    });
  }, [items.length]);

  useEffect(() => {
    if (lightboxIndex == null) return undefined;
    const onKey = (e) => {
      if (e.key === 'Escape') closeLightbox();
      if (e.key === 'ArrowLeft') stepLightbox(-1);
      if (e.key === 'ArrowRight') stepLightbox(1);
    };
    document.body.style.overflow = 'hidden';
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = '';
      window.removeEventListener('keydown', onKey);
    };
  }, [lightboxIndex, closeLightbox, stepLightbox]);

  const scrollBy = (dir) => {
    const el = trackRef.current;
    if (!el) return;
    const amount = Math.min(el.clientWidth * 0.8, 320);
    el.scrollBy({ left: dir * amount, behavior: 'smooth' });
  };

  const empty = failed || (loaded && items.length === 0);
  const lightbox = lightboxIndex != null ? items[lightboxIndex] : null;

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
                <button type="button" className="portfolio-thumb" onClick={() => openLightbox(i)}>
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
        <div className="lightbox-backdrop" onClick={closeLightbox} role="presentation">
          <div
            className="lightbox"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label={lightbox.title}
          >
            <button type="button" className="lightbox-close" onClick={closeLightbox} aria-label="Закрыть">
              <span aria-hidden="true">×</span>
              <span className="lightbox-close-label">Закрыть</span>
            </button>

            {items.length > 1 && (
              <button
                type="button"
                className="lightbox-nav lightbox-prev"
                onClick={() => stepLightbox(-1)}
                aria-label="Предыдущая работа"
              >
                ‹
              </button>
            )}

            <div className="lightbox-stage">
              <img src={mediaUrl(lightbox.imageUrl)} alt={lightbox.title} />
            </div>

            {items.length > 1 && (
              <button
                type="button"
                className="lightbox-nav lightbox-next"
                onClick={() => stepLightbox(1)}
                aria-label="Следующая работа"
              >
                ›
              </button>
            )}

            <div className="lightbox-meta">
              <strong>{lightbox.title}</strong>
              {(lightbox.serviceName || lightbox.description) && (
                <p>{lightbox.serviceName || lightbox.description}</p>
              )}
              {items.length > 1 && (
                <span className="lightbox-counter">
                  {lightboxIndex + 1} / {items.length}
                </span>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
