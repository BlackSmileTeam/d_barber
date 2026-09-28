import { useEffect, useRef, useState } from 'react';
import api from '../services/api';
import { apiErrorMessage } from '../context/ModalContext';

/**
 * Official Telegram Login Widget.
 * Requires BotFather /setdomain for the site host and Telegram:BotUsername + BotToken on API.
 */
export default function TelegramLoginWidget({ onAuth, onError, disabled }) {
  const hostRef = useRef(null);
  const [enabled, setEnabled] = useState(false);
  const [ready, setReady] = useState(false);
  const cbNameRef = useRef(`onTelegramAuth_${Math.random().toString(36).slice(2)}`);

  useEffect(() => {
    let cancelled = false;
    const cbName = cbNameRef.current;

    window[cbName] = async (user) => {
      if (disabled) return;
      try {
        const { data } = await api.post('/auth/telegram', {
          id: user.id,
          firstName: user.first_name,
          lastName: user.last_name,
          username: user.username,
          photoUrl: user.photo_url,
          authDate: user.auth_date,
          hash: user.hash,
        });
        onAuth?.(data);
      } catch (err) {
        onError?.(apiErrorMessage(err));
      }
    };

    (async () => {
      try {
        const { data } = await api.get('/auth/telegram-widget');
        if (cancelled) return;
        if (!data?.enabled || !data?.botUsername) {
          setEnabled(false);
          setReady(true);
          return;
        }
        setEnabled(true);

        const existing = document.querySelector('script[data-telegram-login-widget]');
        const mount = () => {
          if (cancelled || !hostRef.current) return;
          hostRef.current.innerHTML = '';
          const script = document.createElement('script');
          script.src = 'https://telegram.org/js/telegram-widget.js?22';
          script.async = true;
          script.setAttribute('data-telegram-login', data.botUsername);
          script.setAttribute('data-size', 'large');
          script.setAttribute('data-radius', '8');
          script.setAttribute('data-request-access', 'write');
          script.setAttribute('data-onauth', `${cbName}(user)`);
          script.setAttribute('data-telegram-login-widget', '1');
          hostRef.current.appendChild(script);
          setReady(true);
        };

        if (existing && window.Telegram?.Login) {
          mount();
        } else {
          mount();
        }
      } catch {
        if (!cancelled) {
          setEnabled(false);
          setReady(true);
        }
      }
    })();

    return () => {
      cancelled = true;
      try { delete window[cbName]; } catch { /* ignore */ }
    };
  }, [disabled, onAuth, onError]);

  if (ready && !enabled) {
    return null;
  }

  return (
    <div className="auth-social" aria-busy={!ready}>
      <div ref={hostRef} className="telegram-login-host" />
    </div>
  );
}
