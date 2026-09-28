using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Barber.Infrastructure.Data;

public static class DbSeeder
{
    private static readonly (string Key, string Title, string Trigger, TriggerIntervalType Interval, int? Days, string Body)[] SystemTemplates =
    [
        (
            "booking_created",
            "Новая запись",
            "Сразу после создания записи клиентом — уведомление администратору",
            TriggerIntervalType.None,
            null,
            "Новая запись: {Клиент}, {Услуга}, {Дата} в {Время}. {НазваниеСалона}, {Адрес}."
        ),
        (
            "reminder_2h",
            "Напоминание за 2 часа",
            "За 2 часа до начала визита — клиенту в Telegram",
            TriggerIntervalType.None,
            null,
            "{Имя}, через 2 часа запись на «{Услуга}» — {Дата} в {Время}. Ждём вас в {НазваниеСалона}: {Адрес}."
        ),
        (
            "monthly_comeback",
            "Ежемесячное напоминание",
            "Раз в месяц после последнего визита",
            TriggerIntervalType.Monthly,
            30,
            "{Имя}, уже месяц с вашего визита в {НазваниеСалона}. Будем рады снова привести стиль в порядок — запишитесь: {СсылкаНаЗапись}. Ждём вас: {Адрес}."
        )
    ];

    public static async Task SeedAsync(BarberDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureSchemaExtensionsAsync(db);

        if (!await db.AdminUsers.AnyAsync())
        {
            db.AdminUsers.Add(new AdminUser
            {
                Id = Guid.NewGuid(),
                Login = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                DisplayName = "D_Barber"
            });
        }

        if (!await db.SalonSettings.AnyAsync())
        {
            db.SalonSettings.Add(new SalonSettings
            {
                Id = Guid.NewGuid(),
                AboutHtml = "Опыт работы более 3 лет. Работал в сети Barbarossa в Санкт-Петербурге — там отточил темп, чистоту линий и подход к каждому гостю.\n\nСпециализируюсь на мужских стрижках, аккуратных fade и оформлении бороды. Подбираю форму под черты лица, структуру волос и ваш повседневный стиль.\n\nВ работе важны точность переходов, аккуратная окантовка и комфорт в кресле. Расскажу, как поддерживать результат дома, чтобы стрижка держалась дольше.",
                AboutImageUrl = "/images/about-denis.png",
                InstagramUrl = "https://www.instagram.com/Denis_ryabtsov",
                TelegramUrl = "https://t.me/DenisRyabtsov"
            });
        }
        else
        {
            var settings = await db.SalonSettings.FirstAsync();
            if (string.IsNullOrWhiteSpace(settings.AboutHtml)
                || !settings.AboutHtml.Contains("Barbarossa", StringComparison.OrdinalIgnoreCase))
            {
                settings.AboutHtml = "Опыт работы более 3 лет. Работал в сети Barbarossa в Санкт-Петербурге — там отточил темп, чистоту линий и подход к каждому гостю.\n\nСпециализируюсь на мужских стрижках, аккуратных fade и оформлении бороды. Подбираю форму под черты лица, структуру волос и ваш повседневный стиль.\n\nВ работе важны точность переходов, аккуратная окантовка и комфорт в кресле. Расскажу, как поддерживать результат дома, чтобы стрижка держалась дольше.";
            }
            if (string.IsNullOrWhiteSpace(settings.AboutImageUrl))
                settings.AboutImageUrl = "/images/about-denis.png";
            if (string.IsNullOrWhiteSpace(settings.InstagramUrl))
                settings.InstagramUrl = "https://www.instagram.com/Denis_ryabtsov";
            if (string.IsNullOrWhiteSpace(settings.TelegramUrl))
                settings.TelegramUrl = "https://t.me/DenisRyabtsov";
        }

        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(
                new Service { Id = Guid.NewGuid(), Name = "Стрижка", Price = 2300, DurationMinutes = 60, SortOrder = 1, Description = "Мужская стрижка" },
                new Service { Id = Guid.NewGuid(), Name = "Оформление бороды", Price = 2000, DurationMinutes = 60, SortOrder = 2, Description = "Моделирование и оформление бороды" },
                new Service { Id = Guid.NewGuid(), Name = "Стрижка + оформление бороды", Price = 3500, DurationMinutes = 60, SortOrder = 3, Description = "Комплекс" }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.WorkSchedules.AnyAsync())
        {
            for (var d = 1; d <= 6; d++)
            {
                db.WorkSchedules.Add(new WorkSchedule
                {
                    Id = Guid.NewGuid(),
                    DayOfWeek = d,
                    StartTime = new TimeSpan(10, 0, 0),
                    EndTime = new TimeSpan(20, 0, 0),
                    IsDayOff = false
                });
            }

            db.WorkSchedules.Add(new WorkSchedule
            {
                Id = Guid.NewGuid(),
                DayOfWeek = 0,
                StartTime = TimeSpan.Zero,
                EndTime = TimeSpan.Zero,
                IsDayOff = true
            });
        }

        if (!await db.NotificationTemplates.AnyAsync())
        {
            foreach (var t in SystemTemplates)
            {
                db.NotificationTemplates.Add(new NotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    Key = t.Key,
                    Title = t.Title,
                    TriggerDescription = t.Trigger,
                    TriggerIntervalType = t.Interval,
                    TriggerIntervalDays = t.Days,
                    Body = t.Body
                });
            }
        }
        else
        {
            foreach (var t in SystemTemplates)
            {
                var existing = await db.NotificationTemplates.FirstOrDefaultAsync(x => x.Key == t.Key);
                if (existing is null) continue;
                if (string.IsNullOrWhiteSpace(existing.TriggerDescription))
                    existing.TriggerDescription = t.Trigger;
                if (existing.TriggerIntervalType == TriggerIntervalType.None && t.Interval != TriggerIntervalType.None)
                {
                    existing.TriggerIntervalType = t.Interval;
                    existing.TriggerIntervalDays = t.Days;
                    if (string.IsNullOrWhiteSpace(existing.TriggerDescription)
                        || existing.TriggerDescription.Contains("примерно через месяц", StringComparison.OrdinalIgnoreCase))
                        existing.TriggerDescription = t.Trigger;
                }
                if (string.IsNullOrWhiteSpace(existing.Title)
                    || existing.Title.Equals(t.Key, StringComparison.OrdinalIgnoreCase))
                    existing.Title = t.Title;
            }
        }

        if (!await db.PortfolioItems.AnyAsync())
        {
            var cut = await db.Services.AsNoTracking().FirstAsync(s => s.Name == "Стрижка");
            db.PortfolioItems.AddRange(
                new PortfolioItem
                {
                    Id = Guid.NewGuid(),
                    Title = "Чистый fade",
                    Description = "Короткие виски, аккуратный переход",
                    ImageUrl = "https://images.unsplash.com/photo-1599351431202-1e0f0137899a?w=800",
                    ServiceId = cut.Id,
                    DisplayPrice = cut.Price,
                    SortOrder = 1
                },
                new PortfolioItem
                {
                    Id = Guid.NewGuid(),
                    Title = "Классика + текстура",
                    Description = "Стрижка с естественной укладкой",
                    ImageUrl = "https://images.unsplash.com/photo-1621605815971-fbc98d665033?w=800",
                    ServiceId = cut.Id,
                    DisplayPrice = cut.Price,
                    SortOrder = 2
                }
            );
        }

        if (!await db.NewsPosts.AnyAsync())
        {
            db.NewsPosts.AddRange(
                new NewsPost
                {
                    Id = Guid.NewGuid(),
                    Title = "Открыта онлайн-запись",
                    Body = "Теперь записаться к D_Barber можно на сайте и в Telegram. Выбирайте услугу, дату и время.",
                    Status = NewsStatus.Published,
                    PublishAtUtc = DateTime.UtcNow.AddDays(-7),
                    CoverImageUrl = "https://images.unsplash.com/photo-1503951914875-452162b0f3f1?w=800"
                },
                new NewsPost
                {
                    Id = Guid.NewGuid(),
                    Title = "Сезон свежих fade",
                    Body = "Осень — время обновить форму: чистые виски, аккуратный переход и лёгкая укладка под повседневный ритм Петербурга.",
                    Status = NewsStatus.Published,
                    PublishAtUtc = DateTime.UtcNow.AddDays(-3),
                    CoverImageUrl = "https://images.unsplash.com/photo-1622286342621-4bd786c2447c?w=800"
                },
                new NewsPost
                {
                    Id = Guid.NewGuid(),
                    Title = "Борода: форма под лицо",
                    Body = "Помогу подобрать контур бороды под овал лица и густоту — без резких границ и с понятными советами по уходу дома.",
                    Status = NewsStatus.Published,
                    PublishAtUtc = DateTime.UtcNow.AddDays(-1),
                    CoverImageUrl = "https://images.unsplash.com/photo-1621605815971-fbc98d665033?w=800"
                }
            );
        }
        else
        {
            foreach (var post in await db.NewsPosts.Where(n => n.Body.Contains("подтверждение сразу")).ToListAsync())
                post.Body = post.Body.Replace(" — подтверждение сразу.", ".", StringComparison.Ordinal)
                    .Replace("— подтверждение сразу.", ".", StringComparison.Ordinal)
                    .Replace(" подтверждение сразу.", ".", StringComparison.Ordinal);

            if (await db.NewsPosts.CountAsync() < 3)
            {
                if (!await db.NewsPosts.AnyAsync(n => n.Title == "Сезон свежих fade"))
                {
                    db.NewsPosts.Add(new NewsPost
                    {
                        Id = Guid.NewGuid(),
                        Title = "Сезон свежих fade",
                        Body = "Осень — время обновить форму: чистые виски, аккуратный переход и лёгкая укладка под повседневный ритм Петербурга.",
                        Status = NewsStatus.Published,
                        PublishAtUtc = DateTime.UtcNow.AddDays(-3),
                        CoverImageUrl = "https://images.unsplash.com/photo-1622286342621-4bd786c2447c?w=800"
                    });
                }

                if (!await db.NewsPosts.AnyAsync(n => n.Title == "Борода: форма под лицо"))
                {
                    db.NewsPosts.Add(new NewsPost
                    {
                        Id = Guid.NewGuid(),
                        Title = "Борода: форма под лицо",
                        Body = "Помогу подобрать контур бороды под овал лица и густоту — без резких границ и с понятными советами по уходу дома.",
                        Status = NewsStatus.Published,
                        PublishAtUtc = DateTime.UtcNow.AddDays(-1),
                        CoverImageUrl = "https://images.unsplash.com/photo-1621605815971-fbc98d665033?w=800"
                    });
                }
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// EnsureCreated does not add columns to existing MySQL tables.
    /// </summary>
    private static async Task EnsureSchemaExtensionsAsync(BarberDbContext db)
    {
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return;

        var isMysql = provider.Contains("MySql", StringComparison.OrdinalIgnoreCase);
        var isSqlite = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
        if (!isMysql && !isSqlite) return;

        if (isMysql)
        {
            await EnsureMysqlColumnAsync(db, "SalonSettings", "AboutImageUrl",
                "ALTER TABLE SalonSettings ADD COLUMN AboutImageUrl VARCHAR(512) NULL");
            await EnsureMysqlColumnAsync(db, "SalonSettings", "InstagramUrl",
                "ALTER TABLE SalonSettings ADD COLUMN InstagramUrl VARCHAR(512) NULL");
            await EnsureMysqlColumnAsync(db, "SalonSettings", "TelegramUrl",
                "ALTER TABLE SalonSettings ADD COLUMN TelegramUrl VARCHAR(512) NULL");
            await EnsureMysqlColumnAsync(db, "NotificationTemplates", "TriggerDescription",
                "ALTER TABLE NotificationTemplates ADD COLUMN TriggerDescription VARCHAR(512) NOT NULL DEFAULT ''");
            await EnsureMysqlColumnAsync(db, "NotificationTemplates", "TriggerIntervalType",
                "ALTER TABLE NotificationTemplates ADD COLUMN TriggerIntervalType VARCHAR(32) NOT NULL DEFAULT 'None'");
            await EnsureMysqlColumnAsync(db, "NotificationTemplates", "TriggerIntervalDays",
                "ALTER TABLE NotificationTemplates ADD COLUMN TriggerIntervalDays INT NULL");
            await EnsureMysqlColumnAsync(db, "Clients", "HasUserPassword",
                "ALTER TABLE Clients ADD COLUMN HasUserPassword TINYINT(1) NOT NULL DEFAULT 1");
            await EnsureMysqlColumnAsync(db, "Clients", "CreatedViaTelegram",
                "ALTER TABLE Clients ADD COLUMN CreatedViaTelegram TINYINT(1) NOT NULL DEFAULT 0");
            await EnsureMysqlColumnAsync(db, "Clients", "TelegramUserId",
                "ALTER TABLE Clients ADD COLUMN TelegramUserId BIGINT NULL");
            await EnsureMysqlColumnAsync(db, "Clients", "TelegramUsername",
                "ALTER TABLE Clients ADD COLUMN TelegramUsername VARCHAR(64) NULL");
            await EnsureMysqlColumnAsync(db, "Clients", "TelegramFirstName",
                "ALTER TABLE Clients ADD COLUMN TelegramFirstName VARCHAR(128) NULL");
            await EnsureMysqlColumnAsync(db, "Clients", "TelegramLastName",
                "ALTER TABLE Clients ADD COLUMN TelegramLastName VARCHAR(128) NULL");
            await EnsureMysqlColumnAsync(db, "Clients", "TelegramPhotoUrl",
                "ALTER TABLE Clients ADD COLUMN TelegramPhotoUrl VARCHAR(512) NULL");
            await EnsureMysqlColumnAsync(db, "Clients", "TelegramAuthAtUtc",
                "ALTER TABLE Clients ADD COLUMN TelegramAuthAtUtc DATETIME(6) NULL");
        }
        else
        {
            var alters = new[]
            {
                "ALTER TABLE SalonSettings ADD COLUMN AboutImageUrl TEXT NULL",
                "ALTER TABLE SalonSettings ADD COLUMN InstagramUrl TEXT NULL",
                "ALTER TABLE SalonSettings ADD COLUMN TelegramUrl TEXT NULL",
                "ALTER TABLE NotificationTemplates ADD COLUMN TriggerDescription TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE NotificationTemplates ADD COLUMN TriggerIntervalType TEXT NOT NULL DEFAULT 'None'",
                "ALTER TABLE NotificationTemplates ADD COLUMN TriggerIntervalDays INTEGER NULL",
                "ALTER TABLE Clients ADD COLUMN HasUserPassword INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE Clients ADD COLUMN CreatedViaTelegram INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE Clients ADD COLUMN TelegramUserId INTEGER NULL",
                "ALTER TABLE Clients ADD COLUMN TelegramUsername TEXT NULL",
                "ALTER TABLE Clients ADD COLUMN TelegramFirstName TEXT NULL",
                "ALTER TABLE Clients ADD COLUMN TelegramLastName TEXT NULL",
                "ALTER TABLE Clients ADD COLUMN TelegramPhotoUrl TEXT NULL",
                "ALTER TABLE Clients ADD COLUMN TelegramAuthAtUtc TEXT NULL"
            };
            foreach (var sql in alters)
            {
                try { await db.Database.ExecuteSqlRawAsync(sql); }
                catch { /* column exists */ }
            }
        }

        var createOutbox = isMysql
            ? """
              CREATE TABLE IF NOT EXISTS TelegramOutbox (
                Id CHAR(36) NOT NULL PRIMARY KEY,
                ChatId BIGINT NOT NULL,
                Text LONGTEXT NOT NULL,
                CreatedAtUtc DATETIME(6) NOT NULL,
                SentAtUtc DATETIME(6) NULL,
                INDEX IX_TelegramOutbox_SentAtUtc (SentAtUtc),
                INDEX IX_TelegramOutbox_CreatedAtUtc (CreatedAtUtc)
              )
              """
            : """
              CREATE TABLE IF NOT EXISTS TelegramOutbox (
                Id TEXT NOT NULL PRIMARY KEY,
                ChatId INTEGER NOT NULL,
                Text TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                SentAtUtc TEXT NULL
              )
              """;
        try
        {
            await db.Database.ExecuteSqlRawAsync(createOutbox);
        }
        catch
        {
            // Table already exists / provider difference.
        }

        if (isMysql)
        {
            await EnsureMysqlIndexAsync(db, "Clients", "IX_Clients_TelegramUserId",
                "CREATE UNIQUE INDEX IX_Clients_TelegramUserId ON Clients (TelegramUserId)");
        }
        else
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_Clients_TelegramUserId ON Clients (TelegramUserId)");
            }
            catch { /* exists */ }
        }
    }

    private static async Task EnsureMysqlColumnAsync(
        BarberDbContext db, string table, string column, string alterSql)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table
                  AND COLUMN_NAME = @column
                """;
            var pTable = cmd.CreateParameter();
            pTable.ParameterName = "@table";
            pTable.Value = table;
            cmd.Parameters.Add(pTable);
            var pCol = cmd.CreateParameter();
            pCol.ParameterName = "@column";
            pCol.Value = column;
            cmd.Parameters.Add(pCol);

            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            if (exists) return;

            await db.Database.ExecuteSqlRawAsync(alterSql);
        }
        catch
        {
            // Best-effort schema patch.
        }
    }

    private static async Task EnsureMysqlIndexAsync(
        BarberDbContext db, string table, string indexName, string createSql)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                SELECT COUNT(*) FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table
                  AND INDEX_NAME = @index
                """;
            var pTable = cmd.CreateParameter();
            pTable.ParameterName = "@table";
            pTable.Value = table;
            cmd.Parameters.Add(pTable);
            var pIdx = cmd.CreateParameter();
            pIdx.ParameterName = "@index";
            pIdx.Value = indexName;
            cmd.Parameters.Add(pIdx);

            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            if (exists) return;

            await db.Database.ExecuteSqlRawAsync(createSql);
        }
        catch
        {
            // Best-effort index create.
        }
    }
}
