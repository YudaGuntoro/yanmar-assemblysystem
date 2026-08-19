-- Assembly System - Estic nut runner demo master data.
-- Uses existing leak_test_* tables as the demo work-record storage.

INSERT INTO engine_models
  (engine_model, description, note, is_deleted)
VALUES
  ('TF70V', 'ASSEMBLY', 'Demo barcode scan model', 0),
  ('TF85V', 'ASSEMBLY', 'Demo barcode scan model', 0),
  ('TS230V', 'ASSEMBLY', 'Demo barcode scan model', 0)
ON DUPLICATE KEY UPDATE
  description = VALUES(description),
  note = VALUES(note),
  is_deleted = VALUES(is_deleted);

INSERT INTO measurement_units
    (unit_category, unit_symbol, unit_name, is_deleted)
VALUES
    ('pressure', 'N.m', 'Newton meter', 0),
    ('cycle_time', 's', 'Second', 0),
    ('angle', 'deg', 'Degree', 0)
ON DUPLICATE KEY UPDATE
    unit_name = VALUES(unit_name),
    is_deleted = VALUES(is_deleted),
    updated_at = CURRENT_TIMESTAMP;

UPDATE system_settings settings
JOIN measurement_units torque_unit
  ON torque_unit.unit_category = 'pressure'
 AND torque_unit.unit_symbol = 'N.m'
JOIN measurement_units cycle_unit
  ON cycle_unit.unit_category = 'cycle_time'
 AND cycle_unit.unit_symbol = 's'
SET
  settings.pressure_unit_id = torque_unit.id,
  settings.cycle_time_unit_id = cycle_unit.id,
  settings.plc_ip_address = COALESCE(settings.plc_ip_address, '192.168.0.10'),
  settings.updated_at = CURRENT_TIMESTAMP
WHERE settings.id = 1;

INSERT INTO leak_test_judgements
    (judgement_code, judgement_name, result, note, is_deleted)
VALUES
    (1, 'LOW TORQUE NG', 'NG', 'Torque below lower limit', 0),
    (2, 'PASS', 'OK', 'Torque and angle within limits', 0),
    (3, 'HIGH TORQUE NG', 'NG', 'Torque above upper limit', 0),
    (4, 'LOW ANGLE NG', 'NG', 'Angle below lower limit', 0),
    (5, 'HIGH ANGLE NG', 'NG', 'Angle above upper limit', 0),
    (6, 'TOOL ERROR', 'NG', 'Nut runner/controller error', 0)
ON DUPLICATE KEY UPDATE
    judgement_name = VALUES(judgement_name),
    result = VALUES(result),
    note = VALUES(note),
    is_deleted = VALUES(is_deleted),
    updated_at = CURRENT_TIMESTAMP;

DELETE FROM leak_test_work_records
WHERE id IN (1, 2)
   OR id BETWEEN 1001 AND 1070
   OR id BETWEEN 2001 AND 2008
   OR engine_number LIKE 'DASH-%'
   OR engine_number LIKE 'DM-%'
   OR engine_number LIKE 'LT-20260730-%'
   OR engine_number LIKE 'ENG-LT-%'
   OR machine_name LIKE 'Leak Tester Machine%';

INSERT INTO leak_test_work_records
    (id, engine_model_id, engine_number, barcode_scan, check_date, check_time, machine_name, operator_name, parameter_pressure, process_no, step_no, channel_no, press_set_up, press_set_low, pressure_input, cycle_time_leak_test_minutes, judgement_code)
SELECT 9001, model.id, '12220', 'TF70V 12220', CURRENT_DATE, '08:10:00', 'ESTIC Nut Runner 01', 'Demo Operator', 24.00, 1, 1, 'CH-ESTIC-01', 26.00, 22.00, 24.20, 8.50, 2
FROM engine_models model
WHERE model.engine_model = 'TF70V'
UNION ALL
SELECT 9002, model.id, '12221', 'TF85V 12221', CURRENT_DATE, '08:25:00', 'ESTIC Nut Runner 01', 'Demo Operator', 24.00, 2, 1, 'CH-ESTIC-01', 26.00, 22.00, 21.40, 8.80, 1
FROM engine_models model
WHERE model.engine_model = 'TF85V'
UNION ALL
SELECT 9003, model.id, '12222', 'TS230V 12222', CURRENT_DATE, '08:40:00', 'ESTIC Nut Runner 01', 'Demo Operator', 24.00, 3, 1, 'CH-ESTIC-01', 26.00, 22.00, 26.70, 9.10, 3
FROM engine_models model
WHERE model.engine_model = 'TS230V'
ON DUPLICATE KEY UPDATE
    engine_model_id = VALUES(engine_model_id),
    engine_number = VALUES(engine_number),
    barcode_scan = VALUES(barcode_scan),
    check_date = VALUES(check_date),
    check_time = VALUES(check_time),
    machine_name = VALUES(machine_name),
    operator_name = VALUES(operator_name),
    parameter_pressure = VALUES(parameter_pressure),
    process_no = VALUES(process_no),
    step_no = VALUES(step_no),
    channel_no = VALUES(channel_no),
    press_set_up = VALUES(press_set_up),
    press_set_low = VALUES(press_set_low),
    pressure_input = VALUES(pressure_input),
    cycle_time_leak_test_minutes = VALUES(cycle_time_leak_test_minutes),
    judgement_code = VALUES(judgement_code),
    updated_at = CURRENT_TIMESTAMP;
