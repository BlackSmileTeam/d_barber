-- D_Barber: шаг 2 — пользователь приложения (выполнять от root)
-- Перед запуском замените CHANGE_ME_PASSWORD на надёжный пароль.
--
-- Важно: '@%' и '@localhost' — разные аккаунты MySQL.
-- API в Docker (bebochka-edge) подключается как 172.18.x → нужен 'dbarber_app'@'%'.
-- Если Access Denied с IP 172.18.* — см. 05_fix_docker_access.sql.

CREATE USER IF NOT EXISTS 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';

-- Если пользователь уже был с другим паролем (CREATE IF NOT EXISTS пароль не меняет):
ALTER USER 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP
  ON dbarber.* TO 'dbarber_app'@'%';

FLUSH PRIVILEGES;
