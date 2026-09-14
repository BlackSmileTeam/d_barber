using Barber.Domain.Entities;
using Barber.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Barber.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(BarberDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

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
                AboutHtml = "Опыт работы более 3 лет. Работал в сети Barbarossa в Санкт-Петербурге — там отточил темп, чистоту линий и подход к каждому гостю.\n\nСпециализируюсь на мужских стрижках, аккуратных fade и оформлении бороды. Подбираю форму под черты лица, структуру волос и ваш повседневный стиль.\n\nВ работе важны точность переходов, аккуратная окантовка и комфорт в кресле. Расскажу, как поддерживать результат дома, чтобы стрижка держалась дольше."
            });
        }
        else
        {
            var settings = await db.SalonSettings.FirstAsync();
            if (!settings.AboutHtml.Contains("Barbarossa", StringComparison.OrdinalIgnoreCase))
            {
                settings.AboutHtml = "Опыт работы более 3 лет. Работал в сети Barbarossa в Санкт-Петербурге — там отточил темп, чистоту линий и подход к каждому гостю.\n\nСпециализируюсь на мужских стрижках, аккуратных fade и оформлении бороды. Подбираю форму под черты лица, структуру волос и ваш повседневный стиль.\n\nВ работе важны точность переходов, аккуратная окантовка и комфорт в кресле. Расскажу, как поддерживать результат дома, чтобы стрижка держалась дольше.";
            }
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
            db.NotificationTemplates.AddRange(
                new NotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    Key = "booking_created",
                    Title = "Новая запись",
                    Body = "Новая запись: {Клиент}, {Услуга}, {Дата} в {Время}. {НазваниеСалона}, {Адрес}."
                },
                new NotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    Key = "reminder_2h",
                    Title = "Напоминание о записи",
                    Body = "{Имя}, через 2 часа запись на «{Услуга}» — {Дата} в {Время}. Ждём вас в {НазваниеСалона}: {Адрес}."
                },
                new NotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    Key = "monthly_comeback",
                    Title = "Пора освежить стиль",
                    Body = "{Имя}, уже месяц с вашего визита в {НазваниеСалона}. Будем рады снова привести стиль в порядок — запишитесь: {СсылкаНаЗапись}. Ждём вас: {Адрес}."
                }
            );
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
}
