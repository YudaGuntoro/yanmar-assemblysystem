using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Web.API.Domain.Production;

public class EngineModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("engine_model")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }
}

public class Operator
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("operator_code")]
    public string OperatorCode { get; set; } = string.Empty;

    [JsonPropertyName("operator_name")]
    public string OperatorName { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class LeakTestJudgement
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("judgement_code")]
    public int JudgementCode { get; set; }

    [JsonPropertyName("judgement_name")]
    public string JudgementName { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = "NG";

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class MeasurementUnit
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("unit_category")]
    public string UnitCategory { get; set; } = string.Empty;

    [JsonPropertyName("unit_symbol")]
    public string UnitSymbol { get; set; } = string.Empty;

    [JsonPropertyName("unit_name")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class SystemSetting
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("pressure_unit_id")]
    public int PressureUnitId { get; set; }

    [JsonPropertyName("cycle_time_unit_id")]
    public int CycleTimeUnitId { get; set; }

    [JsonPropertyName("backup_db_location")]
    public string? BackupDbLocation { get; set; }

    [JsonPropertyName("backup_schedule")]
    public string BackupSchedule { get; set; } = "daily";

    [JsonPropertyName("plc_ip_address")]
    public string? PlcIpAddress { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public MeasurementUnit? PressureUnit { get; set; }

    [JsonIgnore]
    public MeasurementUnit? CycleTimeUnit { get; set; }
}

public class SystemSettingsResponse
{
    [JsonPropertyName("pressure_unit")]
    public string PressureUnit { get; set; } = "MPa";

    [JsonPropertyName("cycle_time_unit")]
    public string CycleTimeUnit { get; set; } = "s";

    [JsonPropertyName("backup_db_location")]
    public string BackupDbLocation { get; set; } = string.Empty;

    [JsonPropertyName("backup_schedule")]
    public string BackupSchedule { get; set; } = "daily";

    [JsonPropertyName("plc_ip_address")]
    public string PlcIpAddress { get; set; } = string.Empty;
}

public class AssemblyWorkstation
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("workstation_code")]
    public string WorkstationCode { get; set; } = string.Empty;

    [JsonPropertyName("workstation_name")]
    public string WorkstationName { get; set; } = string.Empty;

    [JsonPropertyName("workstation_no")]
    public int WorkstationNo { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("tools")]
    public List<AssemblyTool> Tools { get; set; } = [];
}

public class AssemblyTool
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("workstation_id")]
    public int WorkstationId { get; set; }

    [JsonIgnore]
    public AssemblyWorkstation? Workstation { get; set; }

    [JsonPropertyName("tool_code")]
    public string ToolCode { get; set; } = string.Empty;

    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("nut_size")]
    public string NutSize { get; set; } = string.Empty;

    [JsonPropertyName("program_no")]
    public int? ProgramNo { get; set; }

    [JsonPropertyName("torque_standard")]
    public decimal TorqueStandard { get; set; }

    [JsonPropertyName("torque_min")]
    public decimal TorqueMin { get; set; }

    [JsonPropertyName("torque_max")]
    public decimal TorqueMax { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "N.m";

    [JsonPropertyName("sequence_no")]
    public int SequenceNo { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class LeakTestWorkRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("engine_model_id")]
    public int EngineModelId { get; set; }

    [NotMapped]
    [JsonPropertyName("engine_model")]
    public string EngineModelName => EngineModel?.ModelName ?? string.Empty;

    [JsonIgnore]
    public EngineModel? EngineModel { get; set; }

    [JsonPropertyName("engine_number")]
    public string EngineNumber { get; set; } = string.Empty;

    [JsonPropertyName("barcode_scan")]
    public string? BarcodeScan { get; set; }

    [JsonPropertyName("check_date")]
    public DateTime CheckDate { get; set; } = DateTime.Today;

    [JsonPropertyName("check_time")]
    public string CheckTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");

    [JsonPropertyName("machine_name")]
    public string MachineName { get; set; } = "Leak Tester Machine";

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }

    [NotMapped]
    [JsonPropertyName("operator_code")]
    public string? OperatorCode { get; set; }

    [JsonPropertyName("parameter_pressure")]
    public decimal ParameterPressure { get; set; }

    [JsonPropertyName("process_no")]
    public int? ProcessNo { get; set; }

    [JsonPropertyName("step_no")]
    public int? StepNo { get; set; }

    [NotMapped]
    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("channel_no")]
    public string? ChannelNo { get; set; }

    [JsonPropertyName("press_set_up")]
    public decimal? PressSetUp { get; set; }

    [JsonPropertyName("press_set_low")]
    public decimal? PressSetLow { get; set; }

    [JsonPropertyName("pressure_input")]
    public decimal PressureInput { get; set; }

    [JsonPropertyName("cycle_time_leak_test_minutes")]
    public decimal CycleTimeLeakTestMinutes { get; set; }

    [JsonPropertyName("judgement_code")]
    public int? JudgementCode { get; set; }

    [NotMapped]
    [JsonPropertyName("judgement_name")]
    public string? JudgementName { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_channel_no")]
    public string? ParameterChannelNo { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_standard")]
    public string? ParameterStandard { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_min")]
    public string? ParameterMin { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_max")]
    public string? ParameterMax { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_limit")]
    public string? ParameterLimit { get; set; }

    [NotMapped]
    [JsonPropertyName("result")]
    public string Result { get; set; } = "OK";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class ReworkEngineRecord
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("engine_model_id")]
    public int? EngineModelId { get; set; }

    [NotMapped]
    [JsonPropertyName("engine_model")]
    public string EngineModelName => EngineModel?.ModelName ?? EngineModelText ?? string.Empty;

    [JsonIgnore]
    public EngineModel? EngineModel { get; set; }

    [JsonPropertyName("engine_model_text")]
    public string? EngineModelText { get; set; }

    [JsonPropertyName("engine_number")]
    public string EngineNumber { get; set; } = string.Empty;

    [JsonPropertyName("barcode_scan")]
    public string BarcodeScan { get; set; } = string.Empty;

    [JsonPropertyName("rework_date")]
    public DateTime ReworkDate { get; set; } = DateTime.Today;

    [JsonPropertyName("rework_time")]
    public string ReworkTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }

    [JsonPropertyName("parameter_pressure")]
    public decimal ParameterPressure { get; set; }

    [JsonPropertyName("pressure_input")]
    public decimal PressureInput { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_channel_no")]
    public string? ParameterChannelNo { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_standard")]
    public string? ParameterStandard { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_min")]
    public string? ParameterMin { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_max")]
    public string? ParameterMax { get; set; }

    [NotMapped]
    [JsonPropertyName("parameter_limit")]
    public string? ParameterLimit { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "OK";

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class CreateEngineModelRequest
{
    [JsonPropertyName("engine_model")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }
}

public class CreateOperatorRequest
{
    [JsonPropertyName("operator_code")]
    public string OperatorCode { get; set; } = string.Empty;

    [JsonPropertyName("operator_name")]
    public string OperatorName { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }
}

public class UpdateLeakTestJudgementRequest
{
    [JsonPropertyName("judgement_name")]
    public string JudgementName { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = "NG";

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }
}

public class UpdateSystemSettingsRequest
{
    [JsonPropertyName("pressure_unit")]
    public string PressureUnit { get; set; } = "MPa";

    [JsonPropertyName("cycle_time_unit")]
    public string CycleTimeUnit { get; set; } = "s";

    [JsonPropertyName("backup_db_location")]
    public string? BackupDbLocation { get; set; }

    [JsonPropertyName("backup_schedule")]
    public string BackupSchedule { get; set; } = "daily";

    [JsonPropertyName("plc_ip_address")]
    public string? PlcIpAddress { get; set; }
}

public class CreateAssemblyWorkstationRequest
{
    [JsonPropertyName("workstation_code")]
    public string WorkstationCode { get; set; } = string.Empty;

    [JsonPropertyName("workstation_name")]
    public string WorkstationName { get; set; } = string.Empty;

    [JsonPropertyName("workstation_no")]
    public int WorkstationNo { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }
}

public class CreateAssemblyToolRequest
{
    [JsonPropertyName("workstation_id")]
    public int WorkstationId { get; set; }

    [JsonPropertyName("tool_code")]
    public string ToolCode { get; set; } = string.Empty;

    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("nut_size")]
    public string NutSize { get; set; } = string.Empty;

    [JsonPropertyName("program_no")]
    public int? ProgramNo { get; set; }

    [JsonPropertyName("torque_standard")]
    public decimal TorqueStandard { get; set; }

    [JsonPropertyName("torque_min")]
    public decimal TorqueMin { get; set; }

    [JsonPropertyName("torque_max")]
    public decimal TorqueMax { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("sequence_no")]
    public int SequenceNo { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool? IsDeleted { get; set; }
}

public class TorqueMasterModelResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = string.Empty;
}

public class TorqueMasterSpecResponse
{
    [JsonPropertyName("min")]
    public decimal? Min { get; set; }

    [JsonPropertyName("max")]
    public decimal? Max { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

public class TorqueMasterRowResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("process_no")]
    public int? ProcessNo { get; set; }

    [JsonPropertyName("step_no")]
    public int? StepNo { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("tool_type")]
    public string ToolType { get; set; } = "Visual Inspect";

    [JsonPropertyName("tool_index")]
    public int? ToolIndex { get; set; }

    [JsonPropertyName("item_check")]
    public string? ItemCheck { get; set; }

    [JsonPropertyName("nut_spec")]
    public string? NutSpec { get; set; }

    [JsonPropertyName("nut_usage")]
    public int? NutUsage { get; set; }

    [JsonPropertyName("tool")]
    public int? Tool { get; set; }

    [JsonPropertyName("sub_tool")]
    public int? SubTool { get; set; }

    [JsonPropertyName("work_type")]
    public string? WorkType { get; set; }

    [JsonPropertyName("work_address")]
    public int? WorkAddress { get; set; }

    [JsonPropertyName("model_page")]
    public string? ModelPage { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }

    [JsonPropertyName("specs")]
    public Dictionary<int, TorqueMasterSpecResponse> Specs { get; set; } = [];
}

public class TorqueMasterResponse
{
    [JsonPropertyName("models")]
    public List<TorqueMasterModelResponse> Models { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<TorqueMasterRowResponse> Rows { get; set; } = [];
}

public class UpdateTorqueMasterRowRequest
{
    [JsonPropertyName("process_no")]
    public int? ProcessNo { get; set; }

    [JsonPropertyName("step_no")]
    public int? StepNo { get; set; }

    [JsonPropertyName("model_id")]
    public int? ModelId { get; set; }

    [JsonPropertyName("item")]
    public string? Item { get; set; }

    [JsonPropertyName("tool_type")]
    public string? ToolType { get; set; }

    [JsonPropertyName("item_check")]
    public string? ItemCheck { get; set; }

    [JsonPropertyName("nut_spec")]
    public string? NutSpec { get; set; }

    [JsonPropertyName("nut_usage")]
    public int? NutUsage { get; set; }

    [JsonPropertyName("tool")]
    public int? Tool { get; set; }

    [JsonPropertyName("min")]
    public decimal? Min { get; set; }

    [JsonPropertyName("max")]
    public decimal? Max { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("model_page")]
    public string? ModelPage { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }
}

public class CreateTorqueMasterRowRequest
{
    [JsonPropertyName("model_id")]
    public int ModelId { get; set; }

    [JsonPropertyName("process_no")]
    public int? ProcessNo { get; set; }

    [JsonPropertyName("step_no")]
    public int? StepNo { get; set; }

    [JsonPropertyName("item")]
    public string Item { get; set; } = string.Empty;

    [JsonPropertyName("tool_type")]
    public string ToolType { get; set; } = "Visual Inspect";

    [JsonPropertyName("item_check")]
    public string? ItemCheck { get; set; }

    [JsonPropertyName("nut_spec")]
    public string? NutSpec { get; set; }

    [JsonPropertyName("nut_usage")]
    public int? NutUsage { get; set; }

    [JsonPropertyName("tool")]
    public int? Tool { get; set; }

    [JsonPropertyName("min")]
    public decimal? Min { get; set; }

    [JsonPropertyName("max")]
    public decimal? Max { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("model_page")]
    public string? ModelPage { get; set; }

    [JsonPropertyName("page")]
    public int? Page { get; set; }
}

public class TorqueMasterImportResult
{
    [JsonPropertyName("rows_read")]
    public int RowsRead { get; set; }

    [JsonPropertyName("standards_saved")]
    public int StandardsSaved { get; set; }

    [JsonPropertyName("specs_saved")]
    public int SpecsSaved { get; set; }

    [JsonPropertyName("models_saved")]
    public int ModelsSaved { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }
}

public class CreateLeakTestWorkRecordRequest
{
    [JsonPropertyName("engine_model_id")]
    public int EngineModelId { get; set; }

    [JsonPropertyName("engine_number")]
    public string EngineNumber { get; set; } = string.Empty;

    [JsonPropertyName("barcode_scan")]
    public string? BarcodeScan { get; set; }

    [JsonPropertyName("check_date")]
    public DateTime CheckDate { get; set; } = DateTime.Today;

    [JsonPropertyName("check_time")]
    public string CheckTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");

    [JsonPropertyName("machine_name")]
    public string MachineName { get; set; } = "Leak Tester Machine";

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }

    [JsonPropertyName("parameter_pressure")]
    public decimal ParameterPressure { get; set; }

    [JsonPropertyName("process_no")]
    public int? ProcessNo { get; set; }

    [JsonPropertyName("process_number")]
    public int? ProcessNumber { get; set; }

    [JsonPropertyName("step_no")]
    public int? StepNo { get; set; }

    [JsonPropertyName("step_number")]
    public int? StepNumber { get; set; }

    [JsonPropertyName("channel_no")]
    public string? ChannelNo { get; set; }

    [JsonPropertyName("press_set_up")]
    public decimal? PressSetUp { get; set; }

    [JsonPropertyName("press_set_low")]
    public decimal? PressSetLow { get; set; }

    [JsonPropertyName("pressure_input")]
    public decimal PressureInput { get; set; }

    [JsonPropertyName("cycle_time_leak_test_minutes")]
    public decimal CycleTimeLeakTestMinutes { get; set; }

}

public class CreateHmiLeakTestWorkRecordRequest
{
    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    [JsonPropertyName("barcode_scan")]
    public string? BarcodeScan { get; set; }

    [JsonPropertyName("engine_model")]
    public string? EngineModel { get; set; }

    [JsonPropertyName("serial_no")]
    public string? SerialNo { get; set; }

    [JsonPropertyName("serial no")]
    public string? SerialNoText { get; set; }

    [JsonPropertyName("engine_number")]
    public string? EngineNumber { get; set; }

    [JsonPropertyName("machine_name")]
    public string? MachineName { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("process_no")]
    public int? ProcessNo { get; set; }

    [JsonPropertyName("process_number")]
    public int? ProcessNumber { get; set; }

    [JsonPropertyName("step_no")]
    public int? StepNo { get; set; }

    [JsonPropertyName("step_number")]
    public int? StepNumber { get; set; }

    [JsonPropertyName("channel_no")]
    public string? ChannelNo { get; set; }

    [JsonPropertyName("press_set_up")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? PressSetUp { get; set; }

    [JsonPropertyName("press_set_low")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? PressSetLow { get; set; }

    [JsonPropertyName("pressure_input")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal PressureInput { get; set; }

    [JsonPropertyName("cycle_time")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal CycleTime { get; set; }

    [JsonPropertyName("judgement")]
    public string? Judgement { get; set; }

    [JsonPropertyName("tested_at")]
    public DateTime? TestedAt { get; set; }
}

public class CreateReworkEngineRecordRequest
{
    [JsonPropertyName("barcode_scan")]
    public string BarcodeScan { get; set; } = string.Empty;

    [JsonPropertyName("rework_date")]
    public DateTime ReworkDate { get; set; } = DateTime.Today;

    [JsonPropertyName("rework_time")]
    public string ReworkTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");

    [JsonPropertyName("operator_name")]
    public string? OperatorName { get; set; }

    [JsonPropertyName("parameter_pressure")]
    public decimal ParameterPressure { get; set; }

    [JsonPropertyName("pressure_input")]
    public decimal PressureInput { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "OK";

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

public class LeakTestMonthlySummary
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("month")]
    public int Month { get; set; }

    [JsonPropertyName("month_label")]
    public string MonthLabel { get; set; } = string.Empty;

    [JsonPropertyName("total_engine_inspect")]
    public int TotalEngineInspect { get; set; }

    [JsonPropertyName("ok")]
    public int Ok { get; set; }

    [JsonPropertyName("ng")]
    public int Ng { get; set; }
}
