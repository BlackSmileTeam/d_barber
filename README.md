# D_Barber

Монорепозиторий: Web API (.NET 10), React (Vite), Telegram bot, SQL-скрипты MySQL.

Репозиторий: https://github.com/BlackSmileTeam/d_barber

## Локальный запуск

```bash
# API (SQLite для разработки)
cd backend
dotnet run --project Barber.Api --launch-profile http

# Frontend
cd frontend
npm install
npm run dev
```

- Сайт: http://localhost:5173  
- API / Swagger: http://localhost:5271/swagger  
- Админ по умолчанию: `admin` / `admin123` (смените в проде)

## MySQL

Скрипты: `database/00_install_all.sql` (или `01`→`02`→`03`).

## Секреты GitHub

См. [GITHUB_SECRETS.md](GITHUB_SECRETS.md). Org уже содержит `PROD_*` SSH-секреты.

## CI/CD

- `.github/workflows/ci.yml` — build + обязательные API-тесты  
- `.github/workflows/deploy.yml` — после успешного CI деплой API/frontend на Selectel (порты 55511 / 55512)
