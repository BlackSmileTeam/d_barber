import { useEffect, useState } from 'react';
import api from '../services/api';
import { apiErrorMessage, useModal } from '../context/ModalContext';

const DEFAULT_ABOUT = [
  'Опыт работы более 3 лет. Работал в сети Barbarossa в Санкт-Петербурге — там отточил темп, чистоту линий и подход к каждому гостю.',
  'Специализируюсь на мужских стрижках, аккуратных fade и оформлении бороды. Подбираю форму под черты лица, структуру волос и ваш повседневный стиль — без лишней «воды», но с вниманием к деталям.',
  'В работе важны точность переходов, аккуратная окантовка и комфорт в кресле. Расскажу, как поддерживать результат дома, чтобы стрижка держалась дольше и выглядела свежо между визитами.',
];

export default function AboutPage() {
  const [salon, setSalon] = useState(null);
  const { show } = useModal();

  useEffect(() => {
    api.get('/salon').then((r) => setSalon(r.data)).catch((e) => show({ title: 'Ошибка', message: apiErrorMessage(e) }));
  }, [show]);

  const paragraphs = salon?.aboutHtml
    ? salon.aboutHtml.split(/\n+/).map((p) => p.trim()).filter(Boolean)
    : DEFAULT_ABOUT;

  return (
    <section className="section section-about" id="about">
      <div className="container">
        <h2>Обо мне</h2>
        <div className="about-layout">
          <div className="about-photo">
            <img src="/images/about-denis.png" alt="D_Barber" />
          </div>
          <div className="about-copy">
            {paragraphs.map((p) => (
              <p key={p.slice(0, 24)} className="lead">{p}</p>
            ))}
            {salon && (
              <p className="lead about-place">
                {salon.salonName}. {salon.address}.
              </p>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
