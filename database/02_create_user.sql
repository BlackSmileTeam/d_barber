-- D_Barber: шаг 2 — пользователь приложения (выполнять от root)
-- Перед запуском замените CHANGE_ME_PASSWORD на надёжный пароль.

CREATE USER IF NOT EXISTS 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';

-- Если пользователь уже был с другим паролем:
-- ALTER USER 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP
  ON dbarber.* TO 'dbarber_app'@'%';

FLUSH PRIVILEGES;
