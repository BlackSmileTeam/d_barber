# GitHub Secrets for BlackSmileTeam/d_barber
#
# Уже есть на уровне Organization (создавать в репозитории НЕ нужно):
#   PROD_HOST
#   PROD_USER
#   PROD_SSH_PRIVATE_KEY
#   PROD_SSH_PORT
#
# Создайте в Settings → Secrets and variables → Actions → Repository secrets:

## Обязательные (repository)

| Name | Описание |
|------|----------|
| `JWT_KEY` | Секрет подписи JWT, ≥ 32 символов |
| `DB_CONNECTION_STRING` | MySQL connection string для `dbarber` / `dbarber_app` |

Пример connection string (API в Docker на том же хосте, MySQL на хосте):

```
server=host.docker.internal;port=3306;database=dbarber;user=dbarber_app;password=ВАШ_ПАРОЛЬ;Allow User Variables=True
```

Альтернативное имя (если так привычнее): `APP_CONNECTION_STRING` — подойдёт вместо `DB_CONNECTION_STRING`.

## Рекомендуемые (repository)

| Name | Описание | Пример |
|------|----------|--------|
| `JWT_ISSUER` | Issuer JWT | `DBarberApi` |
| `JWT_AUDIENCE` | Audience JWT | `DBarberClient` |
| `FRONTEND_PUBLIC_URL` | Публичный URL сайта | `http://139.100.225.234:55333` (пока без домена; тот же сервер, что и MySQL) |
| `TELEGRAM_BOT_TOKEN` | Токен бота от @BotFather | |
| `TELEGRAM_ADMIN_CHAT_ID` | Chat id админа для уведомлений о записях | |

## Не создавать (org уже даёт)

- `PROD_HOST`
- `PROD_USER`
- `PROD_SSH_PRIVATE_KEY`
- `PROD_SSH_PORT`
