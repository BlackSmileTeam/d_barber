# GitHub Secrets for BlackSmileTeam/d_barber
#
# Organization secrets (уже есть — в репозитории НЕ создавать):
#   PROD_HOST
#   PROD_USER
#   PROD_SSH_PRIVATE_KEY
#   PROD_SSH_PORT
#
# Repository secrets → Settings → Secrets and variables → Actions

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
Server=host.docker.internal;Port=3306;Database=dbarber;User Id=dbarber_app;Password=ВАШ_ПАРОЛЬ;SslMode=None;AllowPublicKeyRetrieval=True
```

Важно: контейнеры D_Barber подключаются к сети **`bebochka-edge`** (как bebochka), чтобы UFW уже разрешал доступ к MySQL. Сервисы bebochka пайплайн **не останавливает**.

## Опциональные (repository)

| Name | Default / пример |
|------|------------------|
| `JWT_ISSUER` | `DBarberApi` |
| `JWT_AUDIENCE` | `DBarberClient` |
| `TELEGRAM_BOT_TOKEN` | токен бота |
| `TELEGRAM_ADMIN_CHAT_ID` | chat id админа |

## Порты после деплоя

| Сервис | URL |
|--------|-----|
| Frontend | http://139.100.225.234:55333 |
| API | http://139.100.225.234:55332 |
| Health | http://139.100.225.234:55332/api/health |

Публикация только через GitHub Actions (`.github/workflows/deploy.yml`), не вручную по SSH.
