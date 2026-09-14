-- Idempotent: social URLs + notification trigger descriptions for existing MySQL installs
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'SalonSettings'
    AND COLUMN_NAME = 'InstagramUrl'
);
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE SalonSettings ADD COLUMN InstagramUrl VARCHAR(512) NULL AFTER AboutImageUrl',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'SalonSettings'
    AND COLUMN_NAME = 'TelegramUrl'
);
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE SalonSettings ADD COLUMN TelegramUrl VARCHAR(512) NULL AFTER InstagramUrl',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'NotificationTemplates'
    AND COLUMN_NAME = 'TriggerDescription'
);
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE NotificationTemplates ADD COLUMN TriggerDescription VARCHAR(512) NOT NULL DEFAULT '''' AFTER Title',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
