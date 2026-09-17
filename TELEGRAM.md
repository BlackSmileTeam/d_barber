# Telegram-бот D_Barber — публикация

Пошаговая инструкция: создать бота, прописать секреты, задеплоить, проверить.

Архитектура:

| Компонент | Роль |
|-----------|------|
| `dbarber-backend` | Исходящие уведомления (подтверждение записи, напоминания, админ-чат) через Bot API `sendMessage` |
| `dbarber-telegram-bot` | Long-polling: `/start`, привязка телефона клиента, `/chatid` |
| Шаблоны в админке | `booking_created`, `reminder_2h`, `monthly_comeback` (+ свои с интервалом после визита) |

---

## 1. Создать бота в @BotFather

В Telegram откройте [@BotFather](https://t.me/BotFather) и отправьте по очереди:

```
/newbot
```

Дальше BotFather спросит:

1. **Display name** (отображаемое имя), например:
   ```
   D_Barber
   ```
2. **Username** (обязательно заканчивается на `bot`), например:
   ```
   D_Barber_Notify_bot
   ```

Скопируйте токен вида:

```
1234567890:AAHxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

Это значение для секрета `TELEGRAM_BOT_TOKEN`.

Полезные команды BotFather (по желанию):

```
/setdescription
Бот записи D_Barber: подтверждения и напоминания о визитах.

/setabouttext
Официальный бот салона D_Barber.

/setcommands
start - Привязать телефон и начать
chatid - Показать chat id (для админа)
help - Справка
```

---

## 2. Узнать chat id админа (`TELEGRAM_ADMIN_CHAT_ID`)

После деплоя бота (шаг 4) или локально с токеном:

1. Найдите бота в Telegram по username (например `@D_Barber_Notify_bot`).
2. Нажмите **Start** или отправьте:
   ```
   /chatid
   ```
3. Бот ответит числом, например `512345678`.
4. Это значение для секрета `TELEGRAM_ADMIN_CHAT_ID`.

Альтернатива до деплоя бота (через браузер, подставьте свой токен):

```
https://api.telegram.org/bot<TOKEN>/getUpdates
```

Напишите боту любое сообщение, обновите URL — в JSON найдите `"chat":{"id": ... }`.

---

## 3. GitHub Secrets (repository)

Откройте:

**https://github.com/BlackSmileTeam/d_barber/settings/secrets/actions**

→ **New repository secret** (для каждого):

| Name | Value |
|------|--------|
| `TELEGRAM_BOT_TOKEN` | токен из @BotFather |
| `TELEGRAM_ADMIN_CHAT_ID` | число из `/chatid` |

Остальные секреты (`JWT_KEY`, `DB_CONNECTION_STRING`, `FRONTEND_PUBLIC_URL`, org `PROD_*`) — см. [GITHUB_SECRETS.md](GITHUB_SECRETS.md).

Без `TELEGRAM_BOT_TOKEN`:

- API не шлёт уведомления (тихо пропускает);
- контейнер бота стартует, но в логах: token not configured / idle.

---

## 4. Деплой

Деплой уже включает образ `dbarber-telegram-bot` (см. `.github/workflows/deploy.yml`).

1. Закоммитьте/запушьте в `main` **или** вручную:
   - **Actions** → **Deploy production** → **Run workflow**
2. Дождитесь зелёного workflow.
3. В Summary должны быть API, frontend и упоминание telegram bot.

Проверка контейнеров на сервере (если есть SSH; деплой сам по SSH не требует от вас):

```bash
docker ps --filter name=dbarber --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'
docker logs --tail 50 dbarber-telegram-bot
```

В логах бота ожидайте:

```
Bot authorized as @YourBotUsername
```

---

## 5. Проверка после деплоя

### 5.1. Бот отвечает

1. Откройте бота в Telegram → `/start`
2. Должно прийти приветствие и кнопка «Поделиться номером»
3. `/chatid` → ваш числовой id

### 5.2. Админ получает новую запись

1. Убедитесь, что `TELEGRAM_ADMIN_CHAT_ID` = ваш chat id и секреты уже в последнем деплое
2. На сайте зарегистрируйте клиента и создайте запись
3. В Telegram админу должно прийти сообщение по шаблону `booking_created`

### 5.3. Привязка клиента

1. Клиент регистрируется на сайте с телефоном, например `+79001234567`
2. В боте: `/start` → «Поделиться номером» (тот же телефон)
3. Ответ: «Telegram привязан…»
4. В админке → Клиенты: «Telegram привязан»
5. Новая запись этого клиента → уведомление и админу, и клиенту

### 5.4. Напоминание за 2 часа

Работает автоматически в API (`ReminderHostedService`), раз в минуту:

- ищет записи примерно через 2 часа;
- шлёт шаблон `reminder_2h` только если у клиента привязан Telegram.

### 5.5. Напоминания после визита (monthly / interval)

Шаблоны с интервалом (`monthly_comeback`, Daily/Weekly/Monthly/Custom в админке) отправляются после `LastVisitAtUtc` + N дней клиентам с привязанным Telegram.

---

## Локальный запуск бота

```bash
# Терминал 1 — API
cd backend
dotnet run --project Barber.Api --launch-profile http

# Терминал 2 — бот
cd Barber.TelegramBot
set TELEGRAM_BOT_TOKEN=123456:AAH...
set API_BASE_URL=http://localhost:5271/api
dotnet run
```

В `backend/Barber.Api` для исходящих уведомлений локально:

```json
"Telegram": {
  "BotToken": "123456:AAH...",
  "AdminChatId": "512345678"
}
```

или env: `Telegram__BotToken`, `Telegram__AdminChatId`.

---

## Что уже работает / что нет

| Функция | Статус |
|---------|--------|
| Подтверждение записи → админ | ✅ API сразу после create |
| Подтверждение → клиент | ✅ если Telegram привязан |
| Отмена / перенос → админ | ✅ |
| Привязка chat id по телефону | ✅ бот + `POST /api/telegram/link` |
| Напоминание за 2 ч | ✅ фоновый сервис API |
| Напоминания по интервалу после визита | ✅ по шаблонам с TriggerInterval |
| Полноценный бот-запись (слоты внутри Telegram) | ❌ не в scope; запись на сайте |

---

## Troubleshooting

| Симптом | Что сделать |
|---------|-------------|
| Бот молчит | `docker logs dbarber-telegram-bot` — есть ли token / `Bot authorized` |
| Админ не получает сообщения | Проверить `TELEGRAM_ADMIN_CHAT_ID`, что админ писал боту `/start`, что секрет попал в последний deploy |
| «Клиент не найден» при привязке | Тот же телефон, что при регистрации на сайте |
| 401 на link | Токен бота в контейнере бота ≠ `Telegram__BotToken` в API |
| Conflict getUpdates | Не запускайте второй экземпляр бота с тем же токеном локально + на проде |
