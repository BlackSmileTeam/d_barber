import { createContext, useCallback, useContext, useMemo, useState } from 'react';

const ModalContext = createContext(null);

export function ModalProvider({ children }) {
  const [modal, setModal] = useState(null);

  const close = useCallback(() => setModal(null), []);
  const show = useCallback((payload) => setModal(payload), []);

  const value = useMemo(() => ({ show, close, modal }), [show, close, modal]);

  return (
    <ModalContext.Provider value={value}>
      {children}
      {modal && (
        <div className="modal-backdrop" onClick={close} role="presentation">
          <div className="modal" onClick={(e) => e.stopPropagation()} role="dialog" aria-modal="true">
            <h3>{modal.title}</h3>
            {modal.message && <p>{modal.message}</p>}
            {modal.details && (
              <div className="details">
                {modal.details.map((line) => (
                  <div key={line}>{line}</div>
                ))}
              </div>
            )}
            <div className="modal-actions">
              {(modal.actions || [{ label: 'Закрыть', onClick: close, primary: true }]).map((action) => (
                <button
                  key={action.label}
                  type="button"
                  className={action.primary ? 'btn btn-primary' : 'btn btn-ghost'}
                  onClick={() => {
                    action.onClick?.();
                    if (!action.keepOpen) close();
                  }}
                >
                  {action.label}
                </button>
              ))}
            </div>
          </div>
        </div>
      )}
    </ModalContext.Provider>
  );
}

export function useModal() {
  const ctx = useContext(ModalContext);
  if (!ctx) throw new Error('useModal outside provider');
  return ctx;
}

export function apiErrorMessage(err) {
  return err?.response?.data?.message || err?.message || 'Произошла ошибка';
}
