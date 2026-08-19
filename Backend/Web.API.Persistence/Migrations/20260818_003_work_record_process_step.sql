SET @process_no_column_exists := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND COLUMN_NAME = 'process_no'
);
SET @add_process_no_sql := IF(
    @process_no_column_exists = 0,
    'ALTER TABLE leak_test_work_records ADD COLUMN process_no INT NULL AFTER parameter_pressure',
    'SELECT 1'
);
PREPARE add_process_no_statement FROM @add_process_no_sql;
EXECUTE add_process_no_statement;
DEALLOCATE PREPARE add_process_no_statement;

SET @step_no_column_exists := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND COLUMN_NAME = 'step_no'
);
SET @add_step_no_sql := IF(
    @step_no_column_exists = 0,
    'ALTER TABLE leak_test_work_records ADD COLUMN step_no INT NULL AFTER process_no',
    'SELECT 1'
);
PREPARE add_step_no_statement FROM @add_step_no_sql;
EXECUTE add_step_no_statement;
DEALLOCATE PREPARE add_step_no_statement;

