-- =============================================================================
-- D_Barber — исправление Access Denied из Docker (bebochka-edge / 172.18.x)
-- =============================================================================
-- Симптом в логах API:
--   Authentication ... failed ... Access denied for user 'dbarber_app'@'172.18.0.4'
--
-- Причины (часто обе сразу):
--   1) В GitHub secret DB_CONNECTION_STRING другой пароль, чем в MySQL
--   2) Пользователь есть только как 'dbarber_app'@'localhost', а контейнер
--      подключается с IP 172.18.x (нужен '@%' или '@'172.18.%')
--
-- Запуск на сервере от root (локально / по SSH один раз — не через Actions):
--   mysql -u root -p < database/05_fix_docker_access.sql
-- Перед запуском замените CHANGE_ME_PASSWORD на тот же пароль, что в GitHub secret.
-- =============================================================================

-- Диагностика (можно выполнить отдельно):
-- SELECT user, host FROM mysql.user WHERE user = 'dbarber_app';
-- SHOW GRANTS FOR 'dbarber_app'@'%';

CREATE DATABASE IF NOT EXISTS dbarber
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- Аккаунт для подключений с Docker-сети (host.docker.internal → клиент 172.18.x)
CREATE USER IF NOT EXISTS 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';
ALTER USER 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP
  ON dbarber.* TO 'dbarber_app'@'%';

-- Узкий вариант только для подсети bebochka-edge (опционально вместо '%'):
-- CREATE USER IF NOT EXISTS 'dbarber_app'@'172.18.%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';
-- ALTER USER 'dbarber_app'@'172.18.%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';
-- GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP
--   ON dbarber.* TO 'dbarber_app'@'172.18.%';

FLUSH PRIVILEGES;

-- Проверка с хоста (пароль = CHANGE_ME_PASSWORD / секрет):
--   mysql -u dbarber_app -p -h 127.0.0.1 dbarber -e 'SELECT 1'
-- После правок: обновите GitHub secret DB_CONNECTION_STRING и перезапустите Deploy.
