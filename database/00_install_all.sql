-- =============================================================================
-- D_Barber — полный установочный скрипт (MySQL 8+)
-- Запуск от root:
--   mysql -u root -p < database/00_install_all.sql
-- Перед запуском замените CHANGE_ME_PASSWORD на реальный пароль.
-- =============================================================================

-- 1) База
CREATE DATABASE IF NOT EXISTS dbarber
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- 2) Пользователь приложения
CREATE USER IF NOT EXISTS 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';
-- ALTER USER 'dbarber_app'@'%' IDENTIFIED BY 'CHANGE_ME_PASSWORD';

GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, REFERENCES, DROP
  ON dbarber.* TO 'dbarber_app'@'%';
FLUSH PRIVILEGES;

-- 3) Таблицы
USE dbarber;

CREATE TABLE IF NOT EXISTS AdminUsers (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  Login VARCHAR(64) NOT NULL,
  PasswordHash VARCHAR(255) NOT NULL,
  DisplayName VARCHAR(128) NOT NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  UNIQUE KEY IX_AdminUsers_Login (Login)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Clients (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  Phone VARCHAR(32) NOT NULL,
  PasswordHash VARCHAR(255) NOT NULL,
  Name VARCHAR(128) NOT NULL,
  TelegramChatId BIGINT NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  LastVisitAtUtc DATETIME(6) NULL,
  UNIQUE KEY IX_Clients_Phone (Phone)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Services (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  Name VARCHAR(128) NOT NULL,
  Description TEXT NULL,
  Price DECIMAL(10,2) NOT NULL,
  DurationMinutes INT NOT NULL DEFAULT 60,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  SortOrder INT NOT NULL DEFAULT 0,
  CreatedAtUtc DATETIME(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS Appointments (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  ClientId CHAR(36) NOT NULL,
  ServiceId CHAR(36) NOT NULL,
  StartAtUtc DATETIME(6) NOT NULL,
  EndAtUtc DATETIME(6) NOT NULL,
  Status INT NOT NULL,
  Notes TEXT NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  Reminder2hSentAtUtc DATETIME(6) NULL,
  MonthlyReminderSentAtUtc DATETIME(6) NULL,
  KEY IX_Appointments_StartAtUtc (StartAtUtc),
  KEY IX_Appointments_ClientId (ClientId),
  KEY IX_Appointments_ServiceId (ServiceId),
  CONSTRAINT FK_Appointments_Clients FOREIGN KEY (ClientId) REFERENCES Clients(Id),
  CONSTRAINT FK_Appointments_Services FOREIGN KEY (ServiceId) REFERENCES Services(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS WorkSchedules (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  DayOfWeek INT NOT NULL,
  StartTime TIME NOT NULL,
  EndTime TIME NOT NULL,
  IsDayOff TINYINT(1) NOT NULL,
  KEY IX_WorkSchedules_DayOfWeek (DayOfWeek)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS TimeOffs (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  StartAtUtc DATETIME(6) NOT NULL,
  EndAtUtc DATETIME(6) NOT NULL,
  Reason VARCHAR(255) NULL,
  KEY IX_TimeOffs_Range (StartAtUtc, EndAtUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS PortfolioItems (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  Title VARCHAR(255) NOT NULL,
  Description TEXT NULL,
  ImageUrl VARCHAR(1024) NOT NULL,
  ServiceId CHAR(36) NULL,
  DisplayPrice DECIMAL(10,2) NULL,
  SortOrder INT NOT NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  KEY IX_PortfolioItems_ServiceId (ServiceId),
  CONSTRAINT FK_Portfolio_Services FOREIGN KEY (ServiceId) REFERENCES Services(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS NewsPosts (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  Title VARCHAR(255) NOT NULL,
  Body TEXT NOT NULL,
  CoverImageUrl VARCHAR(1024) NULL,
  Status INT NOT NULL,
  PublishAtUtc DATETIME(6) NULL,
  CreatedAtUtc DATETIME(6) NOT NULL,
  UpdatedAtUtc DATETIME(6) NOT NULL,
  KEY IX_NewsPosts_Status_Publish (Status, PublishAtUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS SalonSettings (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  BrandName VARCHAR(128) NOT NULL,
  SalonName VARCHAR(255) NOT NULL,
  Address VARCHAR(512) NOT NULL,
  City VARCHAR(128) NOT NULL,
  Phone VARCHAR(64) NULL,
  AboutHtml TEXT NULL,
  MapLat VARCHAR(32) NULL,
  MapLon VARCHAR(32) NULL,
  TimeZoneId VARCHAR(64) NOT NULL,
  BookingUrl VARCHAR(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS NotificationTemplates (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  `Key` VARCHAR(64) NOT NULL,
  Title VARCHAR(255) NOT NULL,
  Body TEXT NOT NULL,
  UpdatedAtUtc DATETIME(6) NOT NULL,
  UNIQUE KEY IX_NotificationTemplates_Key (`Key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
