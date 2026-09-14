-- Idempotent: add AboutImageUrl for existing MySQL installs
SET @col_exists := (
  SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'SalonSettings'
    AND COLUMN_NAME = 'AboutImageUrl'
);
SET @sql := IF(@col_exists = 0,
  'ALTER TABLE SalonSettings ADD COLUMN AboutImageUrl VARCHAR(512) NULL AFTER AboutHtml',
  'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
