export type EngineModel = {
  id: number;
  engine_model: string;
  description?: string | null;
  note?: string | null;
  is_deleted?: boolean | null;
};

export type Operator = {
  id: number;
  operator_code: string;
  operator_name: string;
  department?: string | null;
  note?: string | null;
  is_deleted?: boolean | null;
  created_at: string;
  updated_at: string;
};

export type LeakTestResult = "OK" | "NG";

export type LeakTestJudgement = {
  id: number;
  judgement_code: number;
  judgement_name: string;
  result: LeakTestResult | "";
  note?: string | null;
  is_deleted?: boolean | null;
  created_at: string;
  updated_at: string;
};

export type AssemblyTool = {
  id: number;
  workstation_id: number;
  tool_code: string;
  tool_name: string;
  nut_size: string;
  program_no?: number | null;
  torque_standard: number;
  torque_min: number;
  torque_max: number;
  unit: string;
  sequence_no: number;
  is_deleted?: boolean | null;
  created_at: string;
  updated_at: string;
};

export type AssemblyWorkstation = {
  id: number;
  workstation_code: string;
  workstation_name: string;
  workstation_no: number;
  description?: string | null;
  is_deleted?: boolean | null;
  created_at: string;
  updated_at: string;
  tools: AssemblyTool[];
};

export type TorqueMasterModel = {
  id: number;
  model_name: string;
};

export type TorqueMasterSpec = {
  min?: number | null;
  max?: number | null;
  unit?: string | null;
};

export type TorqueMasterRow = {
  id: number;
  process_no?: number | null;
  step_no?: number | null;
  item?: string | null;
  tool_type: string;
  tool_index?: number | null;
  item_check?: string | null;
  nut_spec?: string | null;
  nut_usage?: number | null;
  tool?: number | null;
  sub_tool?: number | null;
  work_type?: string | null;
  work_address?: number | null;
  model_page?: string | null;
  page?: number | null;
  specs: Record<string, TorqueMasterSpec>;
};

export type TorqueMasterResponse = {
  models: TorqueMasterModel[];
  rows: TorqueMasterRow[];
};

export type LeakTestWorkRecord = {
  id: number;
  engine_model_id: number;
  engine_model: string;
  engine_number: string;
  barcode_scan?: string | null;
  channel_no?: string | null;
  check_date: string;
  check_time: string;
  machine_name: string;
  operator_code?: string | null;
  operator_name?: string | null;
  parameter_pressure: number;
  process_no?: number | null;
  step_no?: number | null;
  item?: string | null;
  press_set_up?: number | null;
  press_set_low?: number | null;
  pressure_input: number;
  cycle_time_leak_test_minutes: number;
  judgement_code?: number | null;
  judgement_name?: string | null;
  parameter_channel_no?: string | null;
  parameter_standard?: string | null;
  parameter_min?: string | null;
  parameter_max?: string | null;
  parameter_limit?: string | null;
  result: LeakTestResult;
  created_at: string;
  updated_at: string;
};

export type ReworkEngineRecord = {
  id: number;
  engine_model_id?: number | null;
  engine_model: string;
  engine_model_text?: string | null;
  engine_number: string;
  barcode_scan: string;
  rework_date: string;
  rework_time: string;
  operator_name?: string | null;
  parameter_pressure: number;
  pressure_input: number;
  parameter_channel_no?: string | null;
  parameter_standard?: string | null;
  parameter_min?: string | null;
  parameter_max?: string | null;
  parameter_limit?: string | null;
  result: LeakTestResult;
  note?: string | null;
  created_at: string;
  updated_at: string;
};

export type LeakTestMonthlySummary = {
  year: number;
  month: number;
  month_label: string;
  total_engine_inspect: number;
  ok: number;
  ng: number;
};
