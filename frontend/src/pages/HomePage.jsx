export default function HomePage() {
  return (
    <>
      <section className="hero" id="top">
        <div className="hero-media" aria-hidden="true" />
        <div className="container hero-content">
          <p className="hero-brand">D_Barber</p>
          <h1>Стрижка и борода в Санкт-Петербурге</h1>
          <p className="hero-tagline">Точная работа и спокойная атмосфера.</p>
          <div className="hero-actions">
            <a className="btn btn-primary" href="#book">Записаться</a>
            <a className="btn btn-ghost" href="#about">Обо мне</a>
          </div>
        </div>
      </section>

      <div className="marquee" aria-hidden="true">
        <div className="marquee-track">
          <span>Стрижка // Борода // Fade // Контур // Петербург // D_Barber // </span>
          <span>Стрижка // Борода // Fade // Контур // Петербург // D_Barber // </span>
        </div>
      </div>
    </>
  );
}
