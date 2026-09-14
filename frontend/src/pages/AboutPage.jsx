import { useEffect, useState } from 'react';
import api, { mediaUrl } from '../services/api';

export default function AboutPage() {
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

  const paragraphs = salon?.aboutHtml
    ? salon.aboutHtml.split(/\n+/).map((p) => p.trim()).filter(Boolean)
    : [];
  const photo = mediaUrl(salon?.aboutImageUrl);
  const missing = failed || (loaded && (!salon || (!paragraphs.length && !photo)));

  return (
    <section className="section section-about" id="about">
      <div className="container">
        <h2>Обо мне</h2>
        {missing ? (
          <p className="empty-block">Данные отсутствуют</p>
        ) : (
          <div className="about-layout">
            <div className="about-photo">
              {photo ? (
                <img src={photo} alt={salon?.brandName || 'D_Barber'} />
              ) : (
                <div className="about-photo-empty">Данные отсутствуют</div>
              )}
            </div>
            <div className="about-copy">
              {paragraphs.length > 0 ? (
                paragraphs.map((p) => (
                  <p key={p.slice(0, 24)} className="lead">{p}</p>
                ))
              ) : (
                <p className="empty-block">Данные отсутствуют</p>
              )}
              {salon?.salonName && salon?.address && (
                <p className="lead about-place">
                  {salon.salonName}. {salon.address}.
                </p>
              )}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
