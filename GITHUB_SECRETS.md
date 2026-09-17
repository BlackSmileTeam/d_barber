# GitHub Secrets for BlackSmileTeam/d_barber
#
# Organization secrets (уже есть — в репозитории НЕ создавать):
#   PROD_HOST
#   PROD_USER
#   PROD_SSH_PRIVATE_KEY
#   PROD_SSH_PORT
#
# Repository secrets → Settings → Secrets and variables → Actions
# https://github.com/BlackSmileTeam/d_barber/settings/secrets/actions

## Обязательные (repository)

| Name | Значение |
|------|----------|
| `JWT_KEY` | случайная строка ≥ 32 символов |
| `DB_CONNECTION_STRING` | см. ниже |
| `FRONTEND_PUBLIC_URL` | **`http://139.100.225.234:55333`** |

`FRONTEND_PUBLIC_URL` — публичный URL фронта на том же сервере, что и MySQL (пока без домена).  
После появления домена/HTTPS замените на `https://ваш-домен`.

Connection string (API в Docker, MySQL на хосте):

```
Server=host.docker.internal;Port=3306;Database=dbarber;User Id=dbarber_app;Password=ВАШ_ПАРОЛЬ;SslMode=Disabled;AllowPublicKeyRetrieval=True
```

Если в секрете стоит `SslMode=None` (как у Pomelo/bebochka) — API сам заменит на `Disabled` (Oracle MySql.Data).

### Troubleshooting: Access Denied / Restarting (139)

Ошибка в логах контейнера `dbarber_api` / `dbarber-backend`:

```
Access denied for user 'dbarber_app'@'172.18.0.4' (using password: YES)
```

на `DbSeeder` / `EnsureCreatedAsync` — **не** SslMode. Код пароль не «починит». Нужно согласовать MySQL и секрет.

| Что проверить | Действие |
|---------------|----------|
| Пароль в секрете ≠ пароль в MySQL | Обновить `DB_CONNECTION_STRING` (или `APP_CONNECTION_STRING`) в repo secrets: тот же `Password=...`, что задали в MySQL |
| Есть только `dbarber_app`@`localhost` | Создать/починить `@%` скриптом `database/05_fix_docker_access.sql` (от root на сервере) |
| `CREATE USER IF NOT EXISTS` уже отработал | Пароль сам не меняется — в скрипте есть `ALTER USER ... IDENTIFIED BY` |

Диагностика на сервере (от root):

```sql
SELECT user, host FROM mysql.user WHERE user = 'dbarber_app';
SHOW GRANTS FOR 'dbarber_app'@'%';
```

После правок MySQL и/или секрета — снова **Actions → Deploy** (без ручного `docker run` на проде).

## Telegram (repository) — нужны для бота и уведомлений

Полная инструкция: [TELEGRAM.md](TELEGRAM.md).

| Name | Как получить |
|------|----------------|
| `TELEGRAM_BOT_TOKEN` | [@BotFather](https://t.me/BotFather) → `/newbot` → скопировать токен |
| `TELEGRAM_ADMIN_CHAT_ID` | Написать боту `/chatid` → скопировать число |

Куда вставить:

1. Открыть https://github.com/BlackSmileTeam/d_barber/settings/secrets/actions  
2. **New repository secret** → имя `TELEGRAM_BOT_TOKEN` → Value = токен → **Add secret**  
3. **New repository secret** → имя `TELEGRAM_ADMIN_CHAT_ID` → Value = chat id → **Add secret**  
4. **Actions** → **Deploy production** → **Run workflow**

Что делают секреты при деплое:

- API: `Telegram__BotToken`, `Telegram__AdminChatId` — исходящие сообщения (запись, напоминания, админ).
- Контейнер `dbarber-telegram-bot`: тот же токен + `Api__BaseUrl=http://dbarber-backend:44315/api` — приём `/start`, привязка телефона.

## Опциональные (repository)

| Name | Default / пример |
|------|------------------|
| `JWT_ISSUER` | `DBarberApi` |
| `JWT_AUDIENCE` | `DBarberClient` |

## Порты после деплоя

| Сервис | URL / контейнер |
|--------|-----------------|
| Frontend | http://139.100.225.234:55333 |
| API | http://139.100.225.234:55332 |
| Health | http://139.100.225.234:55332/api/health |
| Telegram bot | контейнер `dbarber-telegram-bot` (без внешнего порта) |

Публикация только через GitHub Actions (`.github/workflows/deploy.yml`), не вручную по SSH.
