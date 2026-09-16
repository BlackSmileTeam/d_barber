import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import HomePage from './HomePage';
import NewsPage from './NewsPage';
import AboutPage from './AboutPage';
import PortfolioPage from './PortfolioPage';
import BookPage from './BookPage';
import ContactsPage from './ContactsPage';

function scrollToHash(hash) {
  if (!hash) return;
  const id = hash.replace('#', '');
  const el = document.getElementById(id);
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

export default function LandingPage() {
  const location = useLocation();

  useEffect(() => {
    scrollToHash(location.hash);
  }, [location.hash]);

  useEffect(() => {
    if (location.hash) {
      const t = setTimeout(() => scrollToHash(location.hash), 100);
      return () => clearTimeout(t);
    }
    return undefined;
  }, [location.pathname, location.hash]);

  return (
    <main className="landing">
      <HomePage />
      <NewsPage />
      <AboutPage />
      <PortfolioPage />
      <BookPage />
      <ContactsPage />
    </main>
  );
}
