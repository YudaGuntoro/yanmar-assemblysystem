using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using System.Data;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Web.API.Domain.Production;
using Web.API.Persistence.Context;
using Web.API.Reports;

namespace Web.API.Controllers;

[ApiController]
[Route("api/leaktester")]
public class LeaktesterController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public LeaktesterController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet("work-records")]
    public async Task<IActionResult> WorkRecords(
        [FromQuery] DateTime? date,
        [FromQuery(Name = "date_from")] DateTime? dateFrom,
        [FromQuery(Name = "date_to")] DateTime? dateTo,
        [FromQuery(Name = "engine_model")] string? engineModel,
        [FromQuery(Name = "engine_number")] string? engineNumber,
        [FromQuery(Name = "barcode_scan")] string? barcodeScan,
        [FromQuery] string? result)
    {
        await EnsureLeakTestWorkRecordHmiColumnsAsync();

        var records = await WorkRecordQuery(date, dateFrom, dateTo, engineModel, engineNumber, barcodeScan)
            .OrderByDescending(x => x.CheckDate)
            .ThenByDescending(x => x.CheckTime)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        await HydrateWorkRecordParameterContextAsync(records);
        return ApiOk(FilterWorkRecordsByResult(records, result).Take(500).ToList());
    }

    [HttpGet("work-records/export")]
    [Produces(LeakTestWorkRecordListReportBuilder.ContentType)]
    public async Task<IActionResult> ExportWorkRecords(
        [FromQuery] DateTime? date,
        [FromQuery(Name = "date_from")] DateTime? dateFrom,
        [FromQuery(Name = "date_to")] DateTime? dateTo,
        [FromQuery(Name = "engine_model")] string? engineModel,
        [FromQuery(Name = "engine_number")] string? engineNumber,
        [FromQuery(Name = "barcode_scan")] string? barcodeScan,
        [FromQuery] string? result)
    {
        try
        {
            await EnsureLeakTestWorkRecordHmiColumnsAsync();
            await EnsureLeakTestJudgementTableAsync();

            var records = await WorkRecordQuery(date, dateFrom, dateTo, engineModel, engineNumber, barcodeScan)
                .OrderByDescending(x => x.CheckDate)
                .ThenByDescending(x => x.CheckTime)
                .ThenByDescending(x => x.Id)
                .ToListAsync();
            await HydrateWorkRecordParameterContextAsync(records);
            records = FilterWorkRecordsByResult(records, result).ToList();

            var effectiveDateFrom = dateFrom ?? date;
            var effectiveDateTo = dateTo ?? date;
            var templatePath = Path.Combine(_environment.ContentRootPath, "Templates", LeakTestWorkRecordReportBuilder.TemplateFileName);
            var content = LeakTestWorkRecordListReportBuilder.Build(
                records,
                effectiveDateFrom?.Date,
                effectiveDateTo?.Date,
                templatePath);

            return File(
                content,
                LeakTestWorkRecordListReportBuilder.ContentType,
                LeakTestWorkRecordListReportBuilder.BuildFileName(effectiveDateFrom?.Date, effectiveDateTo?.Date));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("work-records/monthly-summary")]
    public async Task<IActionResult> WorkRecordMonthlySummary([FromQuery] int? year)
    {
        var selectedYear = year is >= 1 and <= 9999 ? year.Value : DateTime.Today.Year;
        var startDate = new DateTime(selectedYear, 1, 1);
        var endDate = startDate.AddYears(1);

        var records = await _db.LeakTestWorkRecords.AsNoTracking()
            .Include(x => x.EngineModel)
            .Where(x => x.CheckDate >= startDate && x.CheckDate < endDate)
            .ToListAsync();
        await HydrateWorkRecordParameterContextAsync(records);

        var summaries = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var monthRecords = records
                    .Where(x => x.CheckDate.Month == month)
                    .ToList();

                return new LeakTestMonthlySummary
                {
                    Year = selectedYear,
                    Month = month,
                    MonthLabel = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month),
                    TotalEngineInspect = monthRecords
                        .Select(x => x.EngineNumber.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Ok = monthRecords
                        .Where(x => x.Result == "OK")
                        .Select(x => x.EngineNumber.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
                    Ng = monthRecords
                        .Where(x => x.Result == "NG")
                        .Select(x => x.EngineNumber.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count()
                };
            })
            .ToList();

        return ApiOk(summaries);
    }

    [HttpGet("work-records/{id:long}/export")]
    [Produces(LeakTestWorkRecordReportBuilder.ContentType)]
    public async Task<IActionResult> ExportWorkRecord(long id)
    {
        try
        {
            await EnsureLeakTestWorkRecordHmiColumnsAsync();

            var record = await _db.LeakTestWorkRecords.AsNoTracking()
                .Include(x => x.EngineModel)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (record is null)
            {
                return ApiNotFound("Leak test work record was not found.");
            }

            await HydrateWorkRecordParameterContextAsync(new[] { record });
            var templatePath = Path.Combine(_environment.ContentRootPath, "Templates", LeakTestWorkRecordReportBuilder.TemplateFileName);
            var content = LeakTestWorkRecordReportBuilder.Build(record, templatePath);
            return File(content, LeakTestWorkRecordReportBuilder.ContentType, LeakTestWorkRecordReportBuilder.BuildFileName(record));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-records")]
    public async Task<IActionResult> CreateWorkRecord([FromBody] CreateLeakTestWorkRecordRequest request)
    {
        try
        {
            await EnsureLeakTestWorkRecordHmiColumnsAsync();

            if (request.EngineModelId <= 0 ||
                string.IsNullOrWhiteSpace(request.EngineNumber) ||
                string.IsNullOrWhiteSpace(request.MachineName) ||
                string.IsNullOrWhiteSpace(request.CheckTime))
            {
                throw new ArgumentException("Engine information and leak test pressure fields are required.");
            }

            var engineModel = await _db.EngineModels
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.EngineModelId && x.IsDeleted != true);
            if (engineModel is null)
            {
                throw new ArgumentException("Engine model was not found or is inactive.");
            }

            if (request.ParameterPressure <= 0 || request.PressureInput <= 0)
            {
                throw new ArgumentException("Leak test pressure values must be greater than zero.");
            }

            if (request.CycleTimeLeakTestMinutes <= 0)
            {
                throw new ArgumentException("Cycle time leak test must be greater than zero.");
            }

            var operatorName = FirstText(request.OperatorName);

            var record = new LeakTestWorkRecord
            {
                EngineModelId = engineModel.Id,
                EngineNumber = request.EngineNumber.Trim(),
                BarcodeScan = FirstText(request.BarcodeScan, BuildBarcodeScan(engineModel.ModelName, request.EngineNumber)),
                CheckDate = request.CheckDate.Date,
                CheckTime = NormalizeCheckTime(request.CheckTime),
                MachineName = request.MachineName.Trim(),
                OperatorName = string.IsNullOrWhiteSpace(operatorName) ? null : TrimTo(operatorName, 150),
                ParameterPressure = request.ParameterPressure,
                ProcessNo = request.ProcessNo ?? request.ProcessNumber,
                StepNo = request.StepNo ?? request.StepNumber,
                ChannelNo = string.IsNullOrWhiteSpace(request.ChannelNo) ? null : TrimTo(request.ChannelNo, 20),
                PressSetUp = request.PressSetUp,
                PressSetLow = request.PressSetLow,
                PressureInput = request.PressureInput,
                CycleTimeLeakTestMinutes = request.CycleTimeLeakTestMinutes,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.LeakTestWorkRecords.Add(record);
            await _db.SaveChangesAsync();
            record.EngineModel = engineModel;
            await HydrateWorkRecordParameterContextAsync(new[] { record });
            return ApiCreated(record, "Leak test work record saved successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [AllowAnonymous]
    [HttpPost("work-records/hmi")]
    public async Task<IActionResult> CreateHmiWorkRecord([FromBody] CreateHmiLeakTestWorkRecordRequest request)
    {
        try
        {
            await EnsureLeakTestWorkRecordHmiColumnsAsync();

            var barcode = NormalizeBarcodeScan(FirstText(request.BarcodeScan, request.Barcode));
            var (barcodeEngineModel, barcodeEngineNumber) = ParseBarcodeScan(barcode);
            var engineModelText = FirstText(request.EngineModel, barcodeEngineModel);
            var engineNumber = FirstText(request.SerialNo, request.SerialNoText, request.EngineNumber, barcodeEngineNumber, barcode);

            if (string.IsNullOrWhiteSpace(engineModelText))
            {
                throw new ArgumentException("Engine model is required from HMI payload.");
            }

            if (string.IsNullOrWhiteSpace(engineNumber))
            {
                throw new ArgumentException("Serial no / engine number is required from HMI payload.");
            }

            if (request.PressureInput <= 0)
            {
                throw new ArgumentException("Pressure input must be greater than zero.");
            }

            if (request.CycleTime <= 0)
            {
                throw new ArgumentException("Cycle time must be greater than zero.");
            }

            var parameterPressure = CalculateHmiParameterPressure(request.PressSetLow, request.PressSetUp);
            if (parameterPressure <= 0)
            {
                throw new ArgumentException("Press set low/up is required from HMI payload.");
            }

            var judgement = await ResolveJudgementSnapshotAsync(request.Judgement);

            var engineModel = await FindOrCreateEngineModelAsync(engineModelText);
            var testedAt = request.TestedAt ?? DateTime.Now;

            var record = new LeakTestWorkRecord
            {
                EngineModelId = engineModel.Id,
                EngineNumber = TrimTo(engineNumber, 120),
                BarcodeScan = FirstText(barcode, BuildBarcodeScan(engineModel.ModelName, engineNumber)),
                CheckDate = testedAt.Date,
                CheckTime = testedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                MachineName = string.IsNullOrWhiteSpace(request.MachineName)
                    ? "Leak Tester Machine 1"
                    : TrimTo(request.MachineName, 150),
                OperatorName = string.IsNullOrWhiteSpace(request.Operator) ? null : TrimTo(request.Operator, 150),
                ParameterPressure = parameterPressure,
                ProcessNo = request.ProcessNo ?? request.ProcessNumber,
                StepNo = request.StepNo ?? request.StepNumber,
                ChannelNo = string.IsNullOrWhiteSpace(request.ChannelNo) ? null : TrimTo(request.ChannelNo, 20),
                PressSetUp = request.PressSetUp,
                PressSetLow = request.PressSetLow,
                PressureInput = request.PressureInput,
                CycleTimeLeakTestMinutes = request.CycleTime,
                JudgementCode = judgement.JudgementCode,
                JudgementName = judgement.JudgementName,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.LeakTestWorkRecords.Add(record);
            await _db.SaveChangesAsync();
            record.EngineModel = engineModel;
            await HydrateWorkRecordParameterContextAsync(new[] { record });
            return ApiCreated(record, "HMI leak test work record saved successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [AllowAnonymous]
    [HttpGet("rework-engine-records")]
    public async Task<IActionResult> ReworkEngineRecords(
        [FromQuery] DateTime? date,
        [FromQuery(Name = "date_from")] DateTime? dateFrom,
        [FromQuery(Name = "date_to")] DateTime? dateTo,
        [FromQuery(Name = "engine_model")] string? engineModel,
        [FromQuery(Name = "engine_number")] string? engineNumber,
        [FromQuery(Name = "barcode_scan")] string? barcodeScan,
        [FromQuery] string? result)
    {
        await EnsureReworkEngineRecordOperatorSnapshotColumnAsync();

        var records = await ReworkEngineRecordQuery(date, dateFrom, dateTo, engineModel, engineNumber, barcodeScan, result)
            .OrderByDescending(x => x.ReworkDate)
            .ThenByDescending(x => x.ReworkTime)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync();
        await HydrateReworkEngineParameterContextAsync(records);
        return ApiOk(records);
    }

    [AllowAnonymous]
    [HttpGet("rework-engine-records/export")]
    [Produces(ReworkEngineRecordListReportBuilder.ContentType)]
    public async Task<IActionResult> ExportReworkEngineRecords(
        [FromQuery] DateTime? date,
        [FromQuery(Name = "date_from")] DateTime? dateFrom,
        [FromQuery(Name = "date_to")] DateTime? dateTo,
        [FromQuery(Name = "engine_model")] string? engineModel,
        [FromQuery(Name = "engine_number")] string? engineNumber,
        [FromQuery(Name = "barcode_scan")] string? barcodeScan,
        [FromQuery] string? result)
    {
        try
        {
            await EnsureReworkEngineRecordOperatorSnapshotColumnAsync();

            var records = await ReworkEngineRecordQuery(date, dateFrom, dateTo, engineModel, engineNumber, barcodeScan, result)
                .OrderByDescending(x => x.ReworkDate)
                .ThenByDescending(x => x.ReworkTime)
                .ThenByDescending(x => x.Id)
                .ToListAsync();
            await HydrateReworkEngineParameterContextAsync(records);

            var effectiveDateFrom = dateFrom ?? date;
            var effectiveDateTo = dateTo ?? date;
            var templatePath = Path.Combine(_environment.ContentRootPath, "Templates", LeakTestWorkRecordReportBuilder.TemplateFileName);
            var content = ReworkEngineRecordListReportBuilder.Build(
                records,
                effectiveDateFrom?.Date,
                effectiveDateTo?.Date,
                templatePath);

            return File(
                content,
                ReworkEngineRecordListReportBuilder.ContentType,
                ReworkEngineRecordListReportBuilder.BuildFileName(effectiveDateFrom?.Date, effectiveDateTo?.Date));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [AllowAnonymous]
    [HttpGet("rework-engine-records/{id:long}/export")]
    [Produces(ReworkEngineRecordReportBuilder.ContentType)]
    public async Task<IActionResult> ExportReworkEngineRecord(long id)
    {
        try
        {
            await EnsureReworkEngineRecordOperatorSnapshotColumnAsync();

            var record = await _db.ReworkEngineRecords.AsNoTracking()
                .Include(x => x.EngineModel)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (record is null)
            {
                return ApiNotFound("Rework engine record was not found.");
            }

            await HydrateReworkEngineParameterContextAsync(new[] { record });
            var templatePath = Path.Combine(_environment.ContentRootPath, "Templates", LeakTestWorkRecordReportBuilder.TemplateFileName);
            var content = ReworkEngineRecordReportBuilder.Build(record, templatePath);
            return File(content, ReworkEngineRecordReportBuilder.ContentType, ReworkEngineRecordReportBuilder.BuildFileName(record));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [AllowAnonymous]
    [HttpPost("rework-engine-records")]
    public async Task<IActionResult> CreateReworkEngineRecord([FromBody] CreateReworkEngineRecordRequest request)
    {
        try
        {
            await EnsureReworkEngineRecordOperatorSnapshotColumnAsync();

            if (string.IsNullOrWhiteSpace(request.BarcodeScan))
            {
                throw new ArgumentException("Barcode scan is required.");
            }

            if (request.ParameterPressure <= 0 || request.PressureInput <= 0)
            {
                throw new ArgumentException("Rework pressure values must be greater than zero.");
            }

            var result = request.Result.Trim().ToUpperInvariant();
            if (result is not ("OK" or "NG"))
            {
                throw new ArgumentException("Result must be OK or NG.");
            }

            var operatorName = FirstText(request.OperatorName);

            var barcodeScan = NormalizeBarcodeScan(request.BarcodeScan);
            var (barcodeEngineModel, barcodeEngineNumber) = ParseBarcodeScan(barcodeScan);
            if (string.IsNullOrWhiteSpace(barcodeEngineNumber))
            {
                barcodeEngineNumber = barcodeEngineModel;
                barcodeEngineModel = null;
            }

            if (string.IsNullOrWhiteSpace(barcodeEngineNumber))
            {
                throw new ArgumentException("Engine number could not be read from barcode.");
            }

            EngineModel? engineModel = null;
            if (!string.IsNullOrWhiteSpace(barcodeEngineModel))
            {
                engineModel = await _db.EngineModels
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ModelName == barcodeEngineModel && x.IsDeleted != true);
            }

            var record = new ReworkEngineRecord
            {
                EngineModelId = engineModel?.Id,
                EngineModelText = engineModel is null ? barcodeEngineModel : null,
                EngineNumber = barcodeEngineNumber.Trim(),
                BarcodeScan = barcodeScan ?? request.BarcodeScan.Trim(),
                ReworkDate = request.ReworkDate.Date,
                ReworkTime = NormalizeCheckTime(request.ReworkTime),
                OperatorName = string.IsNullOrWhiteSpace(operatorName) ? null : TrimTo(operatorName, 150),
                ParameterPressure = request.ParameterPressure,
                PressureInput = request.PressureInput,
                Result = result,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.ReworkEngineRecords.Add(record);
            await _db.SaveChangesAsync();
            record.EngineModel = engineModel;
            await HydrateReworkEngineParameterContextAsync(new[] { record });
            return ApiCreated(record, "Rework engine record saved successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("engine-models")]
    public async Task<IActionResult> EngineModels(
        [FromQuery] string? search,
        [FromQuery(Name = "search_by")] string? searchBy,
        [FromQuery] string? status)
    {
        var query = _db.EngineModels.AsNoTracking().AsQueryable();
        var normalizedStatus = status?.Trim().ToLowerInvariant();

        query = normalizedStatus switch
        {
            "all" => query,
            "deleted" => query.Where(x => x.IsDeleted == true),
            _ => query.Where(x => x.IsDeleted != true)
        };

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalizedSearchBy = searchBy?.Trim().ToLowerInvariant();
            query = normalizedSearchBy switch
            {
                "engine_model" => query.Where(x => x.ModelName.Contains(term)),
                "description" => query.Where(x => x.Description != null && x.Description.Contains(term)),
                _ => query.Where(x =>
                    x.ModelName.Contains(term) ||
                    (x.Description != null && x.Description.Contains(term)))
            };
        }

        return ApiOk(await query
            .OrderBy(x => x.ModelName)
            .ToListAsync());
    }

    [AllowAnonymous]
    [HttpGet("operators")]
    public async Task<IActionResult> Operators(
        [FromQuery] string? search,
        [FromQuery(Name = "search_by")] string? searchBy,
        [FromQuery] string? status)
    {
        var query = _db.Operators.AsNoTracking().AsQueryable();
        var normalizedStatus = status?.Trim().ToLowerInvariant();

        query = normalizedStatus switch
        {
            "all" => query,
            "deleted" => query.Where(x => x.IsDeleted == true),
            _ => query.Where(x => x.IsDeleted != true)
        };

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalizedSearchBy = searchBy?.Trim().ToLowerInvariant();
            query = normalizedSearchBy switch
            {
                "operator_code" => query.Where(x => x.OperatorCode.Contains(term)),
                "operator_name" => query.Where(x => x.OperatorName.Contains(term)),
                "department" => query.Where(x => x.Department != null && x.Department.Contains(term)),
                _ => query.Where(x =>
                    x.OperatorCode.Contains(term) ||
                    x.OperatorName.Contains(term) ||
                    (x.Department != null && x.Department.Contains(term)))
            };
        }

        return ApiOk(await query
            .OrderBy(x => x.OperatorCode)
            .ToListAsync());
    }

    [AllowAnonymous]
    [HttpGet("settings")]
    public async Task<IActionResult> Settings()
    {
        try
        {
            await EnsureSystemSettingsTablesAsync();
            return ApiOk(await GetSystemSettingsResponseAsync());
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSystemSettingsRequest request)
    {
        try
        {
            await EnsureSystemSettingsTablesAsync();

            var pressureUnitId = await FindOrCreateMeasurementUnitAsync("pressure", request.PressureUnit, request.PressureUnit);
            var cycleTimeUnitId = await FindOrCreateMeasurementUnitAsync("cycle_time", request.CycleTimeUnit, request.CycleTimeUnit);
            var schedule = NormalizeBackupSchedule(request.BackupSchedule);

            var setting = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1);
            if (setting is null)
            {
                setting = new SystemSetting
                {
                    Id = 1,
                    CreatedAt = DateTime.Now
                };
                _db.SystemSettings.Add(setting);
            }

            setting.PressureUnitId = pressureUnitId;
            setting.CycleTimeUnitId = cycleTimeUnitId;
            setting.BackupDbLocation = string.IsNullOrWhiteSpace(request.BackupDbLocation)
                ? null
                : TrimTo(request.BackupDbLocation, 500);
            setting.BackupSchedule = schedule;
            setting.PlcIpAddress = string.IsNullOrWhiteSpace(request.PlcIpAddress)
                ? null
                : TrimTo(request.PlcIpAddress, 80);
            setting.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(await GetSystemSettingsResponseAsync(), "Settings updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("torque-master")]
    public async Task<IActionResult> TorqueMaster(
        [FromQuery(Name = "model_ids")] string? modelIds,
        [FromQuery(Name = "process_no")] int? processNo,
        [FromQuery] string? search)
    {
        try
        {
            await EnsureTorqueMasterTablesAsync();
            await SeedTorqueMasterFromLegacyDatabaseAsync();

            var selectedModelIds = ParseIdList(modelIds);
            var models = await ReadTorqueMasterModelsAsync(selectedModelIds);
            var rows = await ReadTorqueMasterRowsAsync(models, processNo, search);

            return ApiOk(new TorqueMasterResponse
            {
                Models = models,
                Rows = rows
            });
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("torque-master/import")]
    public async Task<IActionResult> ImportTorqueMaster([FromForm] IFormFile file)
    {
        try
        {
            await EnsureTorqueMasterTablesAsync();

            if (file is null || file.Length == 0)
            {
                throw new ArgumentException("Excel file is required.");
            }

            var result = await ImportTorqueMasterWorkbookAsync(file);
            return ApiOk(result, "Torque master imported successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("torque-master/rows/{id:long}")]
    public async Task<IActionResult> UpdateTorqueMasterRow(long id, [FromBody] UpdateTorqueMasterRowRequest request)
    {
        try
        {
            await EnsureTorqueMasterTablesAsync();

            if (request.ProcessNo.HasValue && request.ProcessNo.Value < 0)
            {
                throw new ArgumentException("Process no must be zero or greater.");
            }

            if (request.StepNo.HasValue && request.StepNo.Value < 0)
            {
                throw new ArgumentException("Step number must be zero or greater.");
            }
            if (request.Min.HasValue && request.Max.HasValue && request.Min > request.Max)
            {
                throw new ArgumentException("Minimum cannot be greater than maximum.");
            }

            int? toolIndex = string.IsNullOrWhiteSpace(request.ToolType) ? null : request.ToolType.Trim() switch
            {
                "Nut Runner" => 1,
                _ => 3
            };

            var updated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE assembly_torque_standard_rows
SET
    process_no = COALESCE({request.ProcessNo}, process_no),
    step_no = COALESCE({request.StepNo}, step_no),
    item = COALESCE({request.Item}, item),
    tool_index = COALESCE({toolIndex}, tool_index),
    tool_category = COALESCE({request.ToolType}, tool_category),
    item_check = COALESCE({request.ItemCheck}, item_check),
    nut_spec = COALESCE({request.NutSpec}, nut_spec),
    nut_usage = COALESCE({request.NutUsage}, nut_usage),
    tool = COALESCE({request.Tool}, tool),
    model_page = COALESCE({request.ModelPage}, model_page),
    page = COALESCE({request.Page}, page),
    updated_at = CURRENT_TIMESTAMP
WHERE id = {id}
    AND is_deleted != 1");

            if (updated == 0)
            {
                return ApiNotFound("Torque master row was not found.");
            }

            if (request.ModelId.HasValue && request.ModelId.Value > 0)
            {
                await UpsertTorqueStandardSpecAsync(id, request.ModelId.Value, request.Min, request.Max, request.Unit);
            }

            return ApiOk(new { id }, "Torque master row updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpDelete("torque-master/rows/{id:long}")]
    public async Task<IActionResult> DeleteTorqueMasterRow(long id)
    {
        try
        {
            await EnsureTorqueMasterTablesAsync();
            var updated = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE assembly_torque_standard_rows
SET is_deleted = 1, updated_at = CURRENT_TIMESTAMP
WHERE id = {id} AND is_deleted != 1");
            if (updated == 0) return ApiNotFound("Torque master row was not found.");
            return ApiOk(new { id }, "Torque master row deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("torque-master/rows")]
    public async Task<IActionResult> CreateTorqueMasterRow([FromBody] CreateTorqueMasterRowRequest request)
    {
        try
        {
            await EnsureTorqueMasterTablesAsync();
            if (request.ModelId <= 0) throw new ArgumentException("Engine model is required.");
            if (string.IsNullOrWhiteSpace(request.Item)) throw new ArgumentException("Item is required.");
            if (request.ProcessNo < 0 || request.StepNo < 0) throw new ArgumentException("Process and step number must be zero or greater.");
            if (request.Min.HasValue && request.Max.HasValue && request.Min > request.Max) throw new ArgumentException("Minimum cannot be greater than maximum.");

            var modelName = Convert.ToString(await ExecuteScalarAsync("SELECT model_name FROM assembly_torque_models WHERE id = @id AND is_deleted != 1 LIMIT 1", ("@id", request.ModelId)), CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(modelName)) return ApiNotFound("Engine model was not found.");

            var toolIndex = request.ToolType.Trim() switch
            {
                "Nut Runner" => 1,
                "Torque Wrench" => 2,
                _ => 3
            };
            var rowKey = Guid.NewGuid().ToString("N");
            var rowId = await UpsertTorqueStandardRowAsync(rowKey, request.ProcessNo, request.StepNo, request.Item, toolIndex, request.ToolType, request.ItemCheck, request.NutSpec, request.NutUsage, request.Tool, null, null, request.ModelPage ?? modelName, request.Page);
            await UpsertTorqueStandardSpecAsync(rowId, request.ModelId, request.Min, request.Max, request.Unit);
            return ApiCreated(new { id = rowId }, "Torque master row created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("assembly-workstations")]
    public async Task<IActionResult> AssemblyWorkstations([FromQuery] string? status)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();

            var normalizedStatus = status?.Trim().ToLowerInvariant();
            var query = _db.AssemblyWorkstations
                .AsNoTracking()
                .Include(x => x.Tools)
                .AsQueryable();

            query = normalizedStatus switch
            {
                "all" => query,
                "deleted" => query.Where(x => x.IsDeleted == true),
                _ => query.Where(x => x.IsDeleted != true)
            };

            var items = await query
                .OrderBy(x => x.WorkstationNo)
                .ThenBy(x => x.WorkstationCode)
                .ToListAsync();

            foreach (var workstation in items)
            {
                var tools = normalizedStatus == "all"
                    ? workstation.Tools
                    : workstation.Tools.Where(x => normalizedStatus == "deleted" ? x.IsDeleted == true : x.IsDeleted != true).ToList();

                workstation.Tools = tools
                    .OrderBy(x => x.SequenceNo)
                    .ThenBy(x => x.ToolCode)
                    .ToList();
            }

            return ApiOk(items);
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("assembly-workstations")]
    public async Task<IActionResult> CreateAssemblyWorkstation([FromBody] CreateAssemblyWorkstationRequest request)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();

            if (string.IsNullOrWhiteSpace(request.WorkstationCode) ||
                string.IsNullOrWhiteSpace(request.WorkstationName) ||
                request.WorkstationNo <= 0)
            {
                throw new ArgumentException("Workstation code, name, and number are required.");
            }

            var item = new AssemblyWorkstation
            {
                WorkstationCode = TrimTo(request.WorkstationCode.Trim(), 50),
                WorkstationName = TrimTo(request.WorkstationName.Trim(), 120),
                WorkstationNo = request.WorkstationNo,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : TrimTo(request.Description.Trim(), 255),
                IsDeleted = request.IsDeleted == true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.AssemblyWorkstations.Add(item);
            await _db.SaveChangesAsync();
            return ApiCreated(item, "Workstation saved successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("assembly-workstations/{id:int}")]
    public async Task<IActionResult> UpdateAssemblyWorkstation(int id, [FromBody] CreateAssemblyWorkstationRequest request)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();

            var item = await _db.AssemblyWorkstations.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Workstation was not found.");
            }

            if (string.IsNullOrWhiteSpace(request.WorkstationCode) ||
                string.IsNullOrWhiteSpace(request.WorkstationName) ||
                request.WorkstationNo <= 0)
            {
                throw new ArgumentException("Workstation code, name, and number are required.");
            }

            item.WorkstationCode = TrimTo(request.WorkstationCode.Trim(), 50);
            item.WorkstationName = TrimTo(request.WorkstationName.Trim(), 120);
            item.WorkstationNo = request.WorkstationNo;
            item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : TrimTo(request.Description.Trim(), 255);
            item.IsDeleted = request.IsDeleted == true;
            item.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(item, "Workstation updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpDelete("assembly-workstations/{id:int}")]
    public async Task<IActionResult> DeleteAssemblyWorkstation(int id)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();

            var item = await _db.AssemblyWorkstations.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Workstation was not found.");
            }

            item.IsDeleted = true;
            item.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return ApiOk(item, "Workstation deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("assembly-tools")]
    public async Task<IActionResult> CreateAssemblyTool([FromBody] CreateAssemblyToolRequest request)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();
            var item = await BuildAssemblyToolAsync(new AssemblyTool(), request);
            _db.AssemblyTools.Add(item);
            await _db.SaveChangesAsync();
            return ApiCreated(item, "Tool saved successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("assembly-tools/{id:int}")]
    public async Task<IActionResult> UpdateAssemblyTool(int id, [FromBody] CreateAssemblyToolRequest request)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();

            var item = await _db.AssemblyTools.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Tool was not found.");
            }

            await BuildAssemblyToolAsync(item, request);
            item.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return ApiOk(item, "Tool updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpDelete("assembly-tools/{id:int}")]
    public async Task<IActionResult> DeleteAssemblyTool(int id)
    {
        try
        {
            await EnsureAssemblyWorkstationMasterTablesAsync();

            var item = await _db.AssemblyTools.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Tool was not found.");
            }

            item.IsDeleted = true;
            item.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return ApiOk(item, "Tool deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("judgements")]
    public async Task<IActionResult> Judgements()
    {
        try
        {
            await EnsureLeakTestJudgementTableAsync();

            var items = await _db.LeakTestJudgements
                .AsNoTracking()
                .Where(x => x.IsDeleted != true)
                .OrderBy(x => x.JudgementCode)
                .ToListAsync();

            if (items.Count == 0)
            {
                await SeedDefaultHmiJudgementsAsync();
                items = await _db.LeakTestJudgements
                    .AsNoTracking()
                    .Where(x => x.IsDeleted != true)
                    .OrderBy(x => x.JudgementCode)
                    .ToListAsync();
            }

            return ApiOk(items);
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("judgements/{id:int}")]
    public async Task<IActionResult> UpdateJudgement(int id, [FromBody] UpdateLeakTestJudgementRequest request)
    {
        try
        {
            await EnsureLeakTestJudgementTableAsync();

            var result = request.Result.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(result) && result is not ("OK" or "NG"))
            {
                throw new ArgumentException("Result must be empty, OK, or NG.");
            }

            var item = await _db.LeakTestJudgements.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Judgement was not found.");
            }

            item.JudgementName = string.IsNullOrWhiteSpace(request.JudgementName) ? string.Empty : TrimTo(request.JudgementName, 80);
            item.Result = result;
            item.Note = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : TrimTo(request.Note, 150);
            item.IsDeleted = request.IsDeleted ?? false;
            item.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(item, "Judgement updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("operators")]
    public async Task<IActionResult> CreateOperator([FromBody] CreateOperatorRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.OperatorName))
            {
                throw new ArgumentException("Operator name is required.");
            }

            var operatorCode = await BuildNextOperatorCodeAsync();
            var operatorName = request.OperatorName.Trim();

            var item = new Operator
            {
                OperatorCode = operatorCode,
                OperatorName = operatorName,
                Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                IsDeleted = request.IsDeleted ?? false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Operators.Add(item);
            await _db.SaveChangesAsync();
            return ApiCreated(item, "Operator created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("operators/{id:int}")]
    public async Task<IActionResult> UpdateOperator(int id, [FromBody] CreateOperatorRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.OperatorName))
            {
                throw new ArgumentException("Operator name is required.");
            }

            if (string.IsNullOrWhiteSpace(request.OperatorCode))
            {
                throw new ArgumentException("Operator code is required.");
            }

            var item = await _db.Operators.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Operator was not found.");
            }

            var operatorCode = TrimTo(request.OperatorCode.Trim(), 50);
            var operatorName = request.OperatorName.Trim();
            var codeExists = await _db.Operators.AnyAsync(x => x.Id != id && x.OperatorCode == operatorCode);
            if (codeExists)
            {
                throw new ArgumentException("Operator code already exists.");
            }

            item.OperatorCode = operatorCode;
            item.OperatorName = operatorName;
            item.Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim();
            item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            item.IsDeleted = request.IsDeleted ?? false;
            item.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(item, "Operator updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpDelete("operators/{id:int}")]
    public async Task<IActionResult> DeleteOperator(int id)
    {
        try
        {
            var item = await _db.Operators.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Operator was not found.");
            }

            item.IsDeleted = true;
            item.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return ApiOk(item, "Operator deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    private async Task<string> BuildNextOperatorCodeAsync()
    {
        const string prefix = "LT-OP-";
        var codes = await _db.Operators
            .AsNoTracking()
            .Where(x => x.OperatorCode.StartsWith(prefix))
            .Select(x => x.OperatorCode)
            .ToListAsync();

        var maxNumber = codes
            .Select(code => code[prefix.Length..])
            .Select(value => int.TryParse(value, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{maxNumber + 1:0000}";
    }

    [HttpPost("engine-models")]
    public async Task<IActionResult> CreateEngineModel([FromBody] CreateEngineModelRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                throw new ArgumentException("Engine model is required.");
            }

            var item = new EngineModel
            {
                ModelName = request.ModelName.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                IsDeleted = request.IsDeleted ?? false
            };

            _db.EngineModels.Add(item);
            await _db.SaveChangesAsync();
            return ApiCreated(item, "Engine model created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("engine-models/{id:int}")]
    public async Task<IActionResult> UpdateEngineModel(int id, [FromBody] CreateEngineModelRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                throw new ArgumentException("Engine model is required.");
            }

            var item = await _db.EngineModels.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Engine model was not found.");
            }

            var modelName = request.ModelName.Trim();
            var modelExists = await _db.EngineModels.AnyAsync(x => x.Id != id && x.ModelName == modelName);
            if (modelExists)
            {
                throw new ArgumentException("Engine model already exists.");
            }

            item.ModelName = modelName;
            item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            item.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            item.IsDeleted = request.IsDeleted ?? false;

            await _db.SaveChangesAsync();
            return ApiOk(item, "Engine model updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpDelete("engine-models/{id:int}")]
    public async Task<IActionResult> DeleteEngineModel(int id)
    {
        try
        {
            var item = await _db.EngineModels.FirstOrDefaultAsync(x => x.Id == id);
            if (item is null)
            {
                return ApiNotFound("Engine model was not found.");
            }

            var hasWorkRecord = await _db.LeakTestWorkRecords
                .AsNoTracking()
                .AnyAsync(x => x.EngineModelId == id);
            if (hasWorkRecord)
            {
                throw new InvalidOperationException("Tidak bisa dihapus, karena ada data di Nut Runner Work Record.");
            }

            item.IsDeleted = true;
            await _db.SaveChangesAsync();
            return ApiOk(item, "Engine model deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var lastMqttAt = await _db.LeakTestWorkRecords.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync();

        return ApiOk(new
        {
            last_mqtt_at = lastMqttAt,
            server_time = DateTime.Now
        });
    }

    [AllowAnonymous]
    [HttpGet("plc/status")]
    public async Task<IActionResult> PlcStatus()
    {
        try
        {
            await EnsureSystemSettingsTablesAsync();
            var settings = await GetSystemSettingsResponseAsync();
            var plcIpAddress = settings.PlcIpAddress.Trim();
            var isOnline = await CheckPlcReachableAsync(plcIpAddress);

            return ApiOk(new
            {
                plc_ip_address = plcIpAddress,
                configured = !string.IsNullOrWhiteSpace(plcIpAddress),
                online = isOnline,
                checked_at = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [AllowAnonymous]
    [HttpGet("mqtt-broker/status")]
    public async Task<IActionResult> MqttBrokerStatus()
    {
        try
        {
            var settings = LoadMqttBrokerStatusSettings();
            var isOnline = await CheckTcpReachableAsync(settings.Host, settings.Port);

            return ApiOk(new
            {
                host = settings.Host,
                port = settings.Port,
                configured = true,
                online = isOnline,
                checked_at = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    private async Task EnsureLeakTestWorkRecordHmiColumnsAsync()
    {
        await EnsureColumnAsync(
            "leak_test_work_records",
            "barcode_scan",
            "ALTER TABLE leak_test_work_records ADD COLUMN barcode_scan VARCHAR(180) NULL AFTER engine_number");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "channel_no",
            "ALTER TABLE leak_test_work_records ADD COLUMN channel_no VARCHAR(20) NULL AFTER parameter_pressure");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "process_no",
            "ALTER TABLE leak_test_work_records ADD COLUMN process_no INT NULL AFTER parameter_pressure");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "step_no",
            "ALTER TABLE leak_test_work_records ADD COLUMN step_no INT NULL AFTER process_no");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "press_set_up",
            "ALTER TABLE leak_test_work_records ADD COLUMN press_set_up DECIMAL(8, 2) NULL AFTER channel_no");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "press_set_low",
            "ALTER TABLE leak_test_work_records ADD COLUMN press_set_low DECIMAL(8, 2) NULL AFTER press_set_up");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "operator_name",
            "ALTER TABLE leak_test_work_records ADD COLUMN operator_name VARCHAR(150) NULL AFTER machine_name");
        await EnsureColumnAsync(
            "leak_test_work_records",
            "judgement_code",
            "ALTER TABLE leak_test_work_records ADD COLUMN judgement_code INT NULL AFTER cycle_time_leak_test_minutes");
        await DropHistoryOperatorIdColumnsAsync();
        await DropWorkRecordJudgementNameColumnAsync();
        await DropWorkRecordResultColumnAsync();
        await EnsureIndexAsync(
            "leak_test_work_records",
            "ix_leak_test_work_records_barcode_scan",
            "CREATE INDEX ix_leak_test_work_records_barcode_scan ON leak_test_work_records (barcode_scan)");
        await EnsureIndexAsync(
            "leak_test_work_records",
            "ix_leak_test_work_records_channel_no",
            "CREATE INDEX ix_leak_test_work_records_channel_no ON leak_test_work_records (channel_no)");
        await EnsureIndexAsync(
            "leak_test_work_records",
            "ix_leak_test_work_records_judgement_code",
            "CREATE INDEX ix_leak_test_work_records_judgement_code ON leak_test_work_records (judgement_code)");
    }

    private async Task EnsureReworkEngineRecordOperatorSnapshotColumnAsync()
    {
        await EnsureColumnAsync(
            "rework_engine_records",
            "operator_name",
            "ALTER TABLE rework_engine_records ADD COLUMN operator_name VARCHAR(150) NULL AFTER rework_time");
        await DropHistoryOperatorIdColumnsAsync();
    }

    private async Task DropHistoryOperatorIdColumnsAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
SET @has_work_operator_id := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND COLUMN_NAME = 'operator_id'
);
SET @sql := IF(
    @has_work_operator_id > 0,
    'UPDATE leak_test_work_records records JOIN operators operators_master ON operators_master.id = records.operator_id SET records.operator_name = operators_master.operator_name WHERE records.operator_name IS NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_rework_operator_id := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'rework_engine_records'
      AND COLUMN_NAME = 'operator_id'
);
SET @sql := IF(
    @has_rework_operator_id > 0,
    'UPDATE rework_engine_records records JOIN operators operators_master ON operators_master.id = records.operator_id SET records.operator_name = operators_master.operator_name WHERE records.operator_name IS NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_work_fk := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND CONSTRAINT_NAME = 'fk_leak_test_work_records_operator'
);
SET @sql := IF(@has_work_fk > 0, 'ALTER TABLE leak_test_work_records DROP FOREIGN KEY fk_leak_test_work_records_operator', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_rework_fk := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'rework_engine_records'
      AND CONSTRAINT_NAME = 'fk_rework_engine_records_operator'
);
SET @sql := IF(@has_rework_fk > 0, 'ALTER TABLE rework_engine_records DROP FOREIGN KEY fk_rework_engine_records_operator', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_work_index := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND INDEX_NAME = 'ix_leak_test_work_records_operator_id'
);
SET @sql := IF(@has_work_index > 0, 'DROP INDEX ix_leak_test_work_records_operator_id ON leak_test_work_records', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_rework_index := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'rework_engine_records'
      AND INDEX_NAME = 'ix_rework_engine_records_operator_id'
);
SET @sql := IF(@has_rework_index > 0, 'DROP INDEX ix_rework_engine_records_operator_id ON rework_engine_records', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql := IF(@has_work_operator_id > 0, 'ALTER TABLE leak_test_work_records DROP COLUMN operator_id', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql := IF(@has_rework_operator_id > 0, 'ALTER TABLE rework_engine_records DROP COLUMN operator_id', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
    }

    private async Task DropWorkRecordResultColumnAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
SET @has_work_result_index := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND INDEX_NAME = 'ix_leak_test_work_records_result'
);

SET @sql := IF(@has_work_result_index > 0, 'DROP INDEX ix_leak_test_work_records_result ON leak_test_work_records', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_work_result := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND COLUMN_NAME = 'result'
);

SET @sql := IF(@has_work_result > 0, 'ALTER TABLE leak_test_work_records DROP COLUMN result', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
    }

    private async Task DropWorkRecordJudgementNameColumnAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
SET @has_work_judgement_name := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'leak_test_work_records'
      AND COLUMN_NAME = 'judgement_name'
);

SET @sql := IF(@has_work_judgement_name > 0, 'ALTER TABLE leak_test_work_records DROP COLUMN judgement_name', 'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;");
    }

    private async Task EnsureColumnAsync(string tableName, string columnName, string alterSql)
    {
        var exists = await _db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = {0}
                  AND COLUMN_NAME = {1}
                """,
                tableName,
                columnName)
            .SingleAsync();

        if (exists == 0)
        {
            await _db.Database.ExecuteSqlRawAsync(alterSql);
        }
    }

    private async Task EnsureColumnAbsentAsync(string tableName, string columnName, string alterSql)
    {
        var exists = await _db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = {0}
                  AND COLUMN_NAME = {1}
                """,
                tableName,
                columnName)
            .SingleAsync();

        if (exists > 0)
        {
            await _db.Database.ExecuteSqlRawAsync(alterSql);
        }
    }

    private async Task EnsureIndexAsync(string tableName, string indexName, string createSql)
    {
        var exists = await _db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = {0}
                  AND INDEX_NAME = {1}
                """,
                tableName,
                indexName)
            .SingleAsync();

        if (exists == 0)
        {
            await _db.Database.ExecuteSqlRawAsync(createSql);
        }
    }

    private async Task<EngineModel> FindOrCreateEngineModelAsync(string engineModelName)
    {
        var modelName = TrimTo(engineModelName, 45);
        var engineModel = await _db.EngineModels
            .FirstOrDefaultAsync(x => x.ModelName == modelName);

        if (engineModel is not null)
        {
            if (engineModel.IsDeleted == true)
            {
                engineModel.IsDeleted = false;
            }

            return engineModel;
        }

        engineModel = new EngineModel
        {
            ModelName = modelName,
            Description = "HMI",
            Note = "Created by HMI payload",
            IsDeleted = false
        };
        _db.EngineModels.Add(engineModel);
        await _db.SaveChangesAsync();
        return engineModel;
    }

    private async Task<Operator?> FindOrCreateOperatorAsync(string? operatorText)
    {
        var value = FirstText(operatorText);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var operatorItem = await _db.Operators
            .FirstOrDefaultAsync(x => x.OperatorCode == value || x.OperatorName == value);
        if (operatorItem is not null)
        {
            if (operatorItem.IsDeleted == true)
            {
                operatorItem.IsDeleted = false;
            }

            return operatorItem;
        }

        var operatorCode = await BuildUniqueOperatorCodeAsync(value);
        operatorItem = new Operator
        {
            OperatorCode = operatorCode,
            OperatorName = TrimTo(value, 150),
            Department = "Production",
            Note = "Created by HMI payload",
            IsDeleted = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.Operators.Add(operatorItem);
        await _db.SaveChangesAsync();
        return operatorItem;
    }

    private async Task<string> BuildUniqueOperatorCodeAsync(string operatorText)
    {
        var alphanumeric = new string(operatorText
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        var baseCode = TrimTo($"HMI-{(string.IsNullOrWhiteSpace(alphanumeric) ? "OPERATOR" : alphanumeric)}", 50);
        var code = baseCode;
        var suffix = 1;

        while (await _db.Operators.AnyAsync(x => x.OperatorCode == code))
        {
            var suffixText = $"-{suffix}";
            var prefixLength = Math.Min(baseCode.Length, 50 - suffixText.Length);
            code = $"{baseCode[..prefixLength]}{suffixText}";
            suffix++;
        }

        return code;
    }

    private static decimal CalculateHmiParameterPressure(decimal? pressSetLow, decimal? pressSetUp)
    {
        if (pressSetLow.HasValue && pressSetUp.HasValue)
        {
            return Math.Round((NormalizeCosmoPressure(pressSetLow.Value) + NormalizeCosmoPressure(pressSetUp.Value)) / 2, 2);
        }

        if (pressSetLow.HasValue)
        {
            return NormalizeCosmoPressure(pressSetLow.Value);
        }

        return pressSetUp.HasValue ? NormalizeCosmoPressure(pressSetUp.Value) : 0;
    }

    private static decimal NormalizeCosmoPressure(decimal value)
    {
        return Math.Abs(value) >= 10 ? Math.Round(value / 100, 2) : value;
    }

    private static string FormatNormalizedPressure(decimal value)
    {
        return $"{NormalizeCosmoPressure(value).ToString("0.00", CultureInfo.InvariantCulture)} MPa";
    }

    private static string? FormatHmiPressureLimit(decimal? pressSetLow, decimal? pressSetUp)
    {
        if (pressSetLow.HasValue && pressSetUp.HasValue)
        {
            return $"{FormatNormalizedPressureAmount(pressSetLow.Value)} ~ {FormatNormalizedPressureAmount(pressSetUp.Value)} MPa";
        }

        if (pressSetLow.HasValue)
        {
            return $"Min {FormatNormalizedPressure(pressSetLow.Value)}";
        }

        if (pressSetUp.HasValue)
        {
            return $"Max {FormatNormalizedPressure(pressSetUp.Value)}";
        }

        return null;
    }

    private static string FormatNormalizedPressureAmount(decimal value)
    {
        return NormalizeCosmoPressure(value).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string? FirstText(params string?[] values)
    {
        return values
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? BuildBarcodeScan(string? engineModel, string? serialNo)
    {
        var model = engineModel?.Trim().TrimStart('.');
        var serial = serialNo?.Trim();

        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        return TrimTo($"{model} {serial}", 180);
    }

    private static string? NormalizeBarcodeScan(string? barcodeScan)
    {
        if (string.IsNullOrWhiteSpace(barcodeScan))
        {
            return null;
        }

        var normalized = barcodeScan.Trim().TrimStart('.');
        return string.IsNullOrWhiteSpace(normalized) ? null : TrimTo(normalized, 180);
    }

    private static string? NormalizeResult(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "OK" or "PASS" or "PASSED" or "TRUE" or "2" => "OK",
            "NG" or "NOK" or "FAIL" or "FAILED" or "FALSE" or "0" or "1" or "3" or "4" or "5" or "6" or "7" => "NG",
            _ => null
        };
    }

    private sealed record LeakTestJudgementSnapshot(int? JudgementCode, string? JudgementName, string? Result);

    private async Task<LeakTestJudgementSnapshot> ResolveJudgementSnapshotAsync(string? value)
    {
        if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var judgementCode))
        {
            var masterJudgement = await _db.LeakTestJudgements
                .AsNoTracking()
                .Where(x => x.JudgementCode == judgementCode && x.IsDeleted != true)
                .Select(x => new { x.JudgementCode, x.JudgementName, x.Result })
                .FirstOrDefaultAsync();

            if (masterJudgement?.Result is "OK" or "NG")
            {
                return new LeakTestJudgementSnapshot(
                    masterJudgement.JudgementCode,
                    string.IsNullOrWhiteSpace(masterJudgement.JudgementName) ? null : masterJudgement.JudgementName,
                    masterJudgement.Result);
            }

            return new LeakTestJudgementSnapshot(
                judgementCode,
                string.IsNullOrWhiteSpace(masterJudgement?.JudgementName) ? null : masterJudgement.JudgementName,
                NormalizeResult(value));
        }

        return new LeakTestJudgementSnapshot(null, null, NormalizeResult(value));
    }

    private async Task HydrateWorkRecordParameterContextAsync(IReadOnlyCollection<LeakTestWorkRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        await HydrateWorkRecordJudgementsAsync(records);
        await HydrateWorkRecordOperatorsAsync(records);
        await HydrateWorkRecordTorqueMasterItemsAsync(records);

        foreach (var record in records)
        {
            record.BarcodeScan = FirstText(record.BarcodeScan, BuildBarcodeScan(record.EngineModelName, record.EngineNumber));
            record.ParameterChannelNo = FirstText(record.ChannelNo);
            record.ParameterStandard = FormatNormalizedPressure(record.ParameterPressure);
            record.ParameterMin = record.PressSetLow.HasValue ? FormatNormalizedPressure(record.PressSetLow.Value) : null;
            record.ParameterMax = record.PressSetUp.HasValue ? FormatNormalizedPressure(record.PressSetUp.Value) : null;
            record.ParameterLimit = FormatHmiPressureLimit(record.PressSetLow, record.PressSetUp);
            record.Result = EvaluateWorkRecordResult(record);
        }
    }

    private async Task HydrateWorkRecordTorqueMasterItemsAsync(IReadOnlyCollection<LeakTestWorkRecord> records)
    {
        var processStepKeys = records
            .Where(x => x.ProcessNo.HasValue && x.StepNo.HasValue)
            .Select(x => (ProcessNo: x.ProcessNo!.Value, StepNo: x.StepNo!.Value))
            .Distinct()
            .ToList();
        if (processStepKeys.Count == 0)
        {
            return;
        }

        await EnsureTorqueMasterTablesAsync();

        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        var filters = new List<string>();
        for (var index = 0; index < processStepKeys.Count; index++)
        {
            var processParameter = command.CreateParameter();
            processParameter.ParameterName = $"@process_no_{index}";
            processParameter.Value = processStepKeys[index].ProcessNo;
            command.Parameters.Add(processParameter);

            var stepParameter = command.CreateParameter();
            stepParameter.ParameterName = $"@step_no_{index}";
            stepParameter.Value = processStepKeys[index].StepNo;
            command.Parameters.Add(stepParameter);

            filters.Add($"(rows_master.process_no = @process_no_{index} AND rows_master.step_no = @step_no_{index})");
        }

        command.CommandText = $@"
SELECT
    rows_master.process_no,
    rows_master.step_no,
    rows_master.item,
    torque_models.model_name
FROM assembly_torque_standard_rows rows_master
LEFT JOIN assembly_torque_standard_specs specs
    ON specs.standard_row_id = rows_master.id
LEFT JOIN assembly_torque_models torque_models
    ON torque_models.id = specs.torque_model_id
    AND torque_models.is_deleted != 1
WHERE rows_master.is_deleted != 1
  AND ({string.Join(" OR ", filters)})
ORDER BY
    rows_master.process_no,
    rows_master.step_no,
    CASE WHEN torque_models.model_name IS NULL THEN 1 ELSE 0 END,
    rows_master.item
LIMIT 300";

        var exactItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fallbackItems = new Dictionary<(int ProcessNo, int StepNo), string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2))
            {
                continue;
            }

            var processNo = reader.GetInt32(0);
            var stepNo = reader.GetInt32(1);
            var item = reader.GetString(2);
            var modelName = reader.IsDBNull(3) ? null : reader.GetString(3);

            fallbackItems.TryAdd((processNo, stepNo), item);
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                exactItems.TryAdd(BuildWorkRecordTorqueItemKey(modelName, processNo, stepNo), item);
            }
        }

        foreach (var record in records)
        {
            if (!record.ProcessNo.HasValue || !record.StepNo.HasValue)
            {
                continue;
            }

            var processNo = record.ProcessNo.Value;
            var stepNo = record.StepNo.Value;
            record.Item = exactItems.TryGetValue(BuildWorkRecordTorqueItemKey(record.EngineModelName, processNo, stepNo), out var exactItem)
                ? exactItem
                : fallbackItems.GetValueOrDefault((processNo, stepNo));
        }
    }

    private static string BuildWorkRecordTorqueItemKey(string modelName, int processNo, int stepNo)
    {
        return string.Join("|", modelName.Trim().ToUpperInvariant(), processNo, stepNo);
    }

    private static IEnumerable<LeakTestWorkRecord> FilterWorkRecordsByResult(
        IEnumerable<LeakTestWorkRecord> records,
        string? result)
    {
        var resultTerm = result?.Trim().ToUpperInvariant();
        return resultTerm is "OK" or "NG"
            ? records.Where(x => string.Equals(x.Result, resultTerm, StringComparison.OrdinalIgnoreCase))
            : records;
    }

    private static string EvaluateWorkRecordResult(LeakTestWorkRecord record)
    {
        var lowerLimit = ParsePressureValue(record.ParameterMin) ??
            (record.PressSetLow.HasValue ? NormalizeCosmoPressure(record.PressSetLow.Value) : null);
        var upperLimit = ParsePressureValue(record.ParameterMax) ??
            (record.PressSetUp.HasValue ? NormalizeCosmoPressure(record.PressSetUp.Value) : null);

        return EvaluateWorkRecordResult(record.PressureInput, lowerLimit, upperLimit);
    }

    private static string EvaluateWorkRecordResult(decimal pressureInput, decimal? lowerLimit, decimal? upperLimit)
    {
        var normalizedInput = NormalizeCosmoPressure(pressureInput);
        var normalizedLowerLimit = lowerLimit.HasValue ? NormalizeCosmoPressure(lowerLimit.Value) : (decimal?)null;
        var normalizedUpperLimit = upperLimit.HasValue ? NormalizeCosmoPressure(upperLimit.Value) : (decimal?)null;

        if (normalizedLowerLimit.HasValue && normalizedInput < normalizedLowerLimit.Value)
        {
            return "NG";
        }

        if (normalizedUpperLimit.HasValue && normalizedInput > normalizedUpperLimit.Value)
        {
            return "NG";
        }

        return "OK";
    }

    private static decimal? ParsePressureValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var chars = value
            .Trim()
            .TakeWhile(character => char.IsDigit(character) || character is '-' or '+' or '.' or ',')
            .ToArray();
        if (chars.Length == 0)
        {
            return null;
        }

        var numberText = new string(chars).Replace(',', '.');
        return decimal.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? NormalizeCosmoPressure(parsed)
            : null;
    }

    private async Task HydrateWorkRecordJudgementsAsync(IReadOnlyCollection<LeakTestWorkRecord> records)
    {
        var judgementCodes = records
            .Select(x => x.JudgementCode)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (judgementCodes.Count == 0)
        {
            return;
        }

        await EnsureLeakTestJudgementTableAsync();
        var judgementMap = await _db.LeakTestJudgements
            .AsNoTracking()
            .Where(x => judgementCodes.Contains(x.JudgementCode) && x.IsDeleted != true)
            .Select(x => new { x.JudgementCode, x.JudgementName })
            .ToDictionaryAsync(x => x.JudgementCode, x => x.JudgementName);

        foreach (var record in records)
        {
            if (record.JudgementCode.HasValue &&
                judgementMap.TryGetValue(record.JudgementCode.Value, out var judgementName) &&
                !string.IsNullOrWhiteSpace(judgementName))
            {
                record.JudgementName = judgementName;
            }
        }
    }

    private async Task HydrateWorkRecordOperatorsAsync(IReadOnlyCollection<LeakTestWorkRecord> records)
    {
        var operatorTexts = records
            .Select(x => FirstText(x.OperatorName))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (operatorTexts.Count == 0)
        {
            return;
        }

        var operators = await _db.Operators
            .AsNoTracking()
            .Where(x => x.IsDeleted != true &&
                (operatorTexts.Contains(x.OperatorCode) || operatorTexts.Contains(x.OperatorName)))
            .Select(x => new { x.OperatorCode, x.OperatorName })
            .ToListAsync();

        foreach (var record in records)
        {
            var operatorText = FirstText(record.OperatorName);
            if (string.IsNullOrWhiteSpace(operatorText))
            {
                continue;
            }

            var matchedOperator = operators.FirstOrDefault(x =>
                string.Equals(x.OperatorCode, operatorText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.OperatorName, operatorText, StringComparison.OrdinalIgnoreCase));

            if (matchedOperator is not null)
            {
                record.OperatorCode = matchedOperator.OperatorCode;
                record.OperatorName = matchedOperator.OperatorName;
                continue;
            }

            if (LooksLikeOperatorCode(operatorText))
            {
                record.OperatorCode = operatorText;
                record.OperatorName = null;
            }
        }
    }

    private static bool LooksLikeOperatorCode(string value)
    {
        return value.StartsWith("LT-OP-", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("HMI-", StringComparison.OrdinalIgnoreCase) ||
               value.All(char.IsDigit);
    }

    private async Task HydrateReworkEngineParameterContextAsync(IReadOnlyCollection<ReworkEngineRecord> records)
    {
        if (records.Count == 0)
        {
            return;
        }

        foreach (var record in records)
        {
            record.ParameterStandard = FormatNormalizedPressure(record.ParameterPressure);
        }
    }

    private static string NormalizeModelKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static string? FormatParameterLimit(string? minValue, string? maxValue)
    {
        var min = NormalizeSpaces(minValue);
        var max = NormalizeSpaces(maxValue);

        if (!string.IsNullOrWhiteSpace(min) && !string.IsNullOrWhiteSpace(max))
        {
            var (minAmount, minUnit) = SplitParameterValue(min);
            var (maxAmount, maxUnit) = SplitParameterValue(max);

            return !string.IsNullOrWhiteSpace(minUnit) &&
                   minUnit.Equals(maxUnit, StringComparison.OrdinalIgnoreCase)
                ? $"{minAmount} ~ {maxAmount} {minUnit}"
                : $"{min} ~ {max}";
        }

        if (!string.IsNullOrWhiteSpace(min))
        {
            return $"Min {min}";
        }

        if (!string.IsNullOrWhiteSpace(max))
        {
            return $"Max {max}";
        }

        return null;
    }

    private static (string Amount, string Unit) SplitParameterValue(string value)
    {
        var normalized = NormalizeSpaces(value);
        var lastSpaceIndex = normalized.LastIndexOf(' ');

        return lastSpaceIndex <= 0 || lastSpaceIndex >= normalized.Length - 1
            ? (normalized, string.Empty)
            : (normalized[..lastSpaceIndex].Trim(), normalized[(lastSpaceIndex + 1)..].Trim());
    }

    private IQueryable<LeakTestWorkRecord> WorkRecordQuery(
        DateTime? date,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? engineModel,
        string? engineNumber,
        string? barcodeScan)
    {
        IQueryable<LeakTestWorkRecord> query = _db.LeakTestWorkRecords.AsNoTracking()
            .Include(x => x.EngineModel);

        if (dateFrom.HasValue || dateTo.HasValue)
        {
            if (dateFrom.HasValue)
            {
                var startDate = dateFrom.Value.Date;
                query = query.Where(x => x.CheckDate >= startDate);
            }

            if (dateTo.HasValue)
            {
                var endDate = dateTo.Value.Date.AddDays(1);
                query = query.Where(x => x.CheckDate < endDate);
            }
        }
        else if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            var nextDate = selectedDate.AddDays(1);
            query = query.Where(x => x.CheckDate >= selectedDate && x.CheckDate < nextDate);
        }

        var (barcodeEngineModel, barcodeEngineNumber) = ParseBarcodeScan(barcodeScan);
        var barcodeTerm = NormalizeBarcodeScan(barcodeScan);
        var hasBarcodeEngineModel = !string.IsNullOrWhiteSpace(barcodeEngineModel);
        var hasBarcodeEngineNumber = !string.IsNullOrWhiteSpace(barcodeEngineNumber);
        var parsedBarcodeEngineModel = barcodeEngineModel ?? string.Empty;
        var parsedBarcodeEngineNumber = barcodeEngineNumber ?? string.Empty;

        var modelTerm = engineModel?.Trim();
        if (!string.IsNullOrWhiteSpace(modelTerm))
        {
            query = query.Where(x => x.EngineModel != null && x.EngineModel.ModelName.Contains(modelTerm));
        }

        var engineNumberTerm = engineNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(engineNumberTerm))
        {
            query = query.Where(x => x.EngineNumber.Contains(engineNumberTerm));
        }

        if (!string.IsNullOrWhiteSpace(barcodeTerm))
        {
            query = hasBarcodeEngineModel && hasBarcodeEngineNumber
                ? query.Where(x =>
                    (x.BarcodeScan != null && x.BarcodeScan.Contains(barcodeTerm)) ||
                    (x.EngineModel != null &&
                        x.EngineModel.ModelName.Contains(parsedBarcodeEngineModel) &&
                        x.EngineNumber.Contains(parsedBarcodeEngineNumber)))
                : query.Where(x =>
                    (x.BarcodeScan != null && x.BarcodeScan.Contains(barcodeTerm)) ||
                    x.EngineNumber.Contains(barcodeTerm) ||
                    (x.EngineModel != null && x.EngineModel.ModelName.Contains(barcodeTerm)));
        }

        return query;
    }

    private IQueryable<ReworkEngineRecord> ReworkEngineRecordQuery(
        DateTime? date,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? engineModel,
        string? engineNumber,
        string? barcodeScan,
        string? result)
    {
        IQueryable<ReworkEngineRecord> query = _db.ReworkEngineRecords.AsNoTracking()
            .Include(x => x.EngineModel);

        if (dateFrom.HasValue || dateTo.HasValue)
        {
            if (dateFrom.HasValue)
            {
                var startDate = dateFrom.Value.Date;
                query = query.Where(x => x.ReworkDate >= startDate);
            }

            if (dateTo.HasValue)
            {
                var endDate = dateTo.Value.Date.AddDays(1);
                query = query.Where(x => x.ReworkDate < endDate);
            }
        }
        else if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            var nextDate = selectedDate.AddDays(1);
            query = query.Where(x => x.ReworkDate >= selectedDate && x.ReworkDate < nextDate);
        }

        var (barcodeEngineModel, barcodeEngineNumber) = ParseBarcodeScan(barcodeScan);
        var modelTerm = engineModel?.Trim();
        if (!string.IsNullOrWhiteSpace(modelTerm))
        {
            query = query.Where(x =>
                (x.EngineModel != null && x.EngineModel.ModelName.Contains(modelTerm)) ||
                (x.EngineModelText != null && x.EngineModelText.Contains(modelTerm)));
        }

        if (!string.IsNullOrWhiteSpace(barcodeEngineModel))
        {
            query = query.Where(x =>
                (x.EngineModel != null && x.EngineModel.ModelName.Contains(barcodeEngineModel)) ||
                (x.EngineModelText != null && x.EngineModelText.Contains(barcodeEngineModel)));
        }

        var engineNumberTerm = engineNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(engineNumberTerm))
        {
            query = query.Where(x => x.EngineNumber.Contains(engineNumberTerm));
        }

        if (!string.IsNullOrWhiteSpace(barcodeEngineNumber))
        {
            query = query.Where(x => x.EngineNumber.Contains(barcodeEngineNumber));
        }

        var resultTerm = result?.Trim().ToUpperInvariant();
        if (resultTerm is "OK" or "NG")
        {
            query = query.Where(x => x.Result == resultTerm);
        }

        return query;
    }

    private async Task EnsureLeakTestJudgementTableAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS leak_test_judgements (
    id INT AUTO_INCREMENT PRIMARY KEY,
    judgement_code INT NOT NULL,
    judgement_name VARCHAR(80) NOT NULL,
    result VARCHAR(10) NOT NULL,
    note VARCHAR(150) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_leak_test_judgements_code (judgement_code),
    KEY ix_leak_test_judgements_result (result)
)");

        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO leak_test_judgements
    (judgement_code, judgement_name, result, note, is_deleted)
VALUES
    (1, 'LL NG', 'NG', 'HMI judgement', 0),
    (2, 'PASS', 'OK', 'HMI judgement', 0),
    (3, 'UL NG', 'NG', 'HMI judgement', 0),
    (4, 'LL2 NG', 'NG', 'HMI judgement', 0),
    (5, 'UL2 NG', 'NG', 'HMI judgement', 0),
    (6, 'ERROR', 'NG', 'HMI judgement', 0),
    (7, '', '', '', 0),
    (8, '', '', '', 0),
    (9, '', '', '', 0),
    (10, '', '', '', 0),
    (11, '', '', '', 0),
    (12, '', '', '', 0),
    (13, '', '', '', 0),
    (14, '', '', '', 0),
    (15, '', '', '', 0),
    (16, '', '', '', 0),
    (17, '', '', '', 0),
    (18, '', '', '', 0),
    (19, '', '', '', 0),
    (20, '', '', '', 0)
ON DUPLICATE KEY UPDATE
    result = IF(is_deleted = 1 OR judgement_name LIKE 'DUMMY-%' OR judgement_name IN ('OK', 'NG'), VALUES(result), result),
    note = IF(is_deleted = 1 OR note LIKE 'Temporary dummy%' OR note IN ('Gateway judgement OK', 'Gateway judgement NG'), VALUES(note), note),
    is_deleted = VALUES(is_deleted),
    judgement_name = IF(is_deleted = 1 OR judgement_name LIKE 'DUMMY-%' OR judgement_name IN ('OK', 'NG'), VALUES(judgement_name), judgement_name),
    updated_at = CURRENT_TIMESTAMP");

        await _db.Database.ExecuteSqlRawAsync(@"
UPDATE leak_test_judgements
SET is_deleted = 1, updated_at = CURRENT_TIMESTAMP
WHERE judgement_code > 20");
    }

    private async Task SeedDefaultHmiJudgementsAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO leak_test_judgements
    (judgement_code, judgement_name, result, note, is_deleted)
VALUES
    (1, 'LL NG', 'NG', 'HMI judgement', 0),
    (2, 'PASS', 'OK', 'HMI judgement', 0),
    (3, 'UL NG', 'NG', 'HMI judgement', 0),
    (4, 'LL2 NG', 'NG', 'HMI judgement', 0),
    (5, 'UL2 NG', 'NG', 'HMI judgement', 0),
    (6, 'ERROR', 'NG', 'HMI judgement', 0),
    (7, '', '', '', 0),
    (8, '', '', '', 0),
    (9, '', '', '', 0),
    (10, '', '', '', 0),
    (11, '', '', '', 0),
    (12, '', '', '', 0),
    (13, '', '', '', 0),
    (14, '', '', '', 0),
    (15, '', '', '', 0),
    (16, '', '', '', 0),
    (17, '', '', '', 0),
    (18, '', '', '', 0),
    (19, '', '', '', 0),
    (20, '', '', '', 0)
ON DUPLICATE KEY UPDATE
    result = IF(is_deleted = 1 OR judgement_name LIKE 'DUMMY-%' OR judgement_name IN ('OK', 'NG'), VALUES(result), result),
    note = IF(is_deleted = 1 OR note LIKE 'Temporary dummy%' OR note IN ('Gateway judgement OK', 'Gateway judgement NG'), VALUES(note), note),
    is_deleted = VALUES(is_deleted),
    judgement_name = IF(is_deleted = 1 OR judgement_name LIKE 'DUMMY-%' OR judgement_name IN ('OK', 'NG'), VALUES(judgement_name), judgement_name),
    updated_at = CURRENT_TIMESTAMP");

        await _db.Database.ExecuteSqlRawAsync(@"
UPDATE leak_test_judgements
SET is_deleted = 1, updated_at = CURRENT_TIMESTAMP
WHERE judgement_code > 20");
    }

    private static string CellText(IXLWorksheet worksheet, int rowNumber, int columnNumber)
    {
        var value = worksheet.Cell(rowNumber, columnNumber).GetFormattedString();
        return NormalizeSpaces(value);
    }

    private static string NormalizeSpaces(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string TrimTo(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static (string? EngineModel, string? EngineNumber) ParseBarcodeScan(string? barcodeScan)
    {
        if (string.IsNullOrWhiteSpace(barcodeScan))
        {
            return (null, null);
        }

        var normalized = NormalizeBarcodeScan(barcodeScan);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (null, null);
        }

        var separatorIndex = normalized.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        if (separatorIndex < 0)
        {
            return (string.IsNullOrWhiteSpace(normalized) ? null : normalized, null);
        }

        var engineModel = normalized[..separatorIndex].Trim();
        var engineNumber = normalized[(separatorIndex + 1)..].Trim();
        return (
            string.IsNullOrWhiteSpace(engineModel) ? null : engineModel,
            string.IsNullOrWhiteSpace(engineNumber) ? null : engineNumber);
    }

    private static string NormalizeCheckTime(string checkTime)
    {
        var trimmed = checkTime.Trim();
        return trimmed.Length == 5 ? $"{trimmed}:00" : trimmed;
    }

    private async Task<AssemblyTool> BuildAssemblyToolAsync(AssemblyTool item, CreateAssemblyToolRequest request)
    {
        if (request.WorkstationId <= 0 ||
            string.IsNullOrWhiteSpace(request.ToolCode) ||
            string.IsNullOrWhiteSpace(request.ToolName) ||
            string.IsNullOrWhiteSpace(request.NutSize))
        {
            throw new ArgumentException("Workstation, tool code, tool name, and nut size are required.");
        }

        if (request.TorqueMin > request.TorqueStandard || request.TorqueStandard > request.TorqueMax)
        {
            throw new ArgumentException("Torque standard must be between torque min and torque max.");
        }

        var workstationExists = await _db.AssemblyWorkstations.AnyAsync(x => x.Id == request.WorkstationId && x.IsDeleted != true);
        if (!workstationExists)
        {
            throw new ArgumentException("Workstation was not found or is inactive.");
        }

        item.WorkstationId = request.WorkstationId;
        item.ToolCode = TrimTo(request.ToolCode.Trim(), 50);
        item.ToolName = TrimTo(request.ToolName.Trim(), 120);
        item.NutSize = TrimTo(request.NutSize.Trim(), 40);
        item.ProgramNo = request.ProgramNo;
        item.TorqueStandard = request.TorqueStandard;
        item.TorqueMin = request.TorqueMin;
        item.TorqueMax = request.TorqueMax;
        item.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "N.m" : TrimTo(request.Unit.Trim(), 20);
        item.SequenceNo = request.SequenceNo;
        item.IsDeleted = request.IsDeleted == true;

        if (item.Id == 0)
        {
            item.CreatedAt = DateTime.Now;
            item.UpdatedAt = DateTime.Now;
        }

        return item;
    }

    private async Task EnsureTorqueMasterTablesAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS assembly_torque_models (
    id INT AUTO_INCREMENT PRIMARY KEY,
    legacy_model_id INT NULL,
    model_name VARCHAR(80) NOT NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_torque_models_name (model_name),
    KEY ix_assembly_torque_models_legacy_id (legacy_model_id)
)");

        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS assembly_torque_tool_categories (
    id INT AUTO_INCREMENT PRIMARY KEY,
    category_name VARCHAR(40) NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_torque_tool_categories_name (category_name)
)");

        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO assembly_torque_tool_categories (category_name, display_order, is_deleted)
VALUES
    ('Torque Wrench', 1, 0),
    ('Nut Runner', 2, 0),
    ('Visual Inspect', 3, 0)
ON DUPLICATE KEY UPDATE
    display_order = VALUES(display_order),
    is_deleted = 0,
    updated_at = CURRENT_TIMESTAMP");

        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS assembly_torque_standard_rows (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    row_key CHAR(32) NOT NULL,
    process_no INT NULL,
    step_no INT NULL,
    item VARCHAR(200) NULL,
    tool_index INT NULL,
    tool_category VARCHAR(40) NOT NULL DEFAULT 'Visual Inspect',
    item_check VARCHAR(200) NULL,
    nut_spec VARCHAR(80) NULL,
    nut_usage INT NULL,
    tool INT NULL,
    sub_tool INT NULL,
    work_type VARCHAR(10) NULL,
    model_page VARCHAR(40) NULL,
    page INT NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_torque_standard_rows_key (row_key),
    KEY ix_assembly_torque_standard_rows_process_step (process_no, step_no),
    KEY ix_assembly_torque_standard_rows_item (item)
)");

        await EnsureColumnAsync(
            "assembly_torque_standard_rows",
            "tool_category",
            "ALTER TABLE assembly_torque_standard_rows ADD COLUMN tool_category VARCHAR(40) NOT NULL DEFAULT 'Visual Inspect' AFTER tool_index");

        await _db.Database.ExecuteSqlRawAsync(@"
UPDATE assembly_torque_standard_rows
SET tool_category = CASE
    WHEN tool_index = 1 THEN 'Nut Runner'
    WHEN tool_index = 2 THEN 'Torque Wrench'
    ELSE 'Visual Inspect'
END
WHERE tool_category IS NULL
    OR tool_category = ''
    OR tool_category = 'No Use'
    OR tool_category NOT IN ('Torque Wrench', 'Nut Runner', 'Visual Inspect')
    OR (tool_index = 1 AND tool_category != 'Nut Runner')
    OR (tool_index = 2 AND tool_category != 'Torque Wrench')
    OR ((tool_index IS NULL OR tool_index NOT IN (1, 2)) AND tool_category != 'Visual Inspect')");

        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS assembly_torque_standard_specs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    standard_row_id BIGINT NOT NULL,
    torque_model_id INT NOT NULL,
    min_value DECIMAL(10, 2) NULL,
    max_value DECIMAL(10, 2) NULL,
    unit VARCHAR(50) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_torque_standard_specs_row_model (standard_row_id, torque_model_id),
    KEY ix_assembly_torque_standard_specs_model (torque_model_id),
    CONSTRAINT fk_assembly_torque_standard_specs_row
        FOREIGN KEY (standard_row_id) REFERENCES assembly_torque_standard_rows (id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT fk_assembly_torque_standard_specs_model
        FOREIGN KEY (torque_model_id) REFERENCES assembly_torque_models (id)
        ON UPDATE CASCADE
        ON DELETE CASCADE
)");
    }

    private async Task<TorqueMasterImportResult> ImportTorqueMasterWorkbookAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var headerRow = worksheet.FirstRowUsed() ?? throw new ArgumentException("Excel header row was not found.");
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        var headers = headerRow.CellsUsed()
            .Select(cell => new { Key = NormalizeExcelHeader(cell.GetString()), Index = cell.Address.ColumnNumber })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.First().Index);

        var result = new TorqueMasterImportResult();
        int? carriedProcessNo = null;
        var importedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            result.RowsRead++;
            var processNo = ReadExcelInt(row, headers, "processno");
            if (processNo.HasValue)
            {
                carriedProcessNo = processNo;
            }
            else
            {
                processNo = carriedProcessNo;
            }

            var stepNo = ReadExcelInt(row, headers, "stepno", "stepnumber");
            var item = ReadExcelText(row, headers, "item");
            var itemCheck = ReadExcelText(row, headers, "itemcheck");
            var modelName = FirstText(
                ReadExcelText(row, headers, "modelpage"),
                ReadExcelText(row, headers, "model"),
                ReadExcelText(row, headers, "machine_model"));

            if (string.IsNullOrWhiteSpace(item) || string.IsNullOrWhiteSpace(modelName))
            {
                result.Skipped++;
                continue;
            }

            var toolIndex = ReadExcelInt(row, headers, "torquecheck", "toolindex");
            var min = ReadExcelDecimal(row, headers, "min");
            var max = ReadExcelDecimal(row, headers, "max");
            var unit = ReadExcelText(row, headers, "unit");
            var nutSpec = ReadExcelText(row, headers, "nutspec");
            var nutUsage = ReadExcelInt(row, headers, "nutusage");
            var tool = ReadExcelInt(row, headers, "tool");
            var subTool = ReadExcelInt(row, headers, "subtool");
            var workType = ReadExcelText(row, headers, "worktype");
            var page = ReadExcelInt(row, headers, "page");
            var rowKey = BuildTorqueMasterRowKey(processNo, stepNo, item, toolIndex, itemCheck, nutSpec, tool, subTool, workType);

            var modelId = await UpsertTorqueModelAsync(modelName.Trim());
            await UpsertEngineModelMasterAsync(modelName.Trim());
            if (importedModels.Add(modelName.Trim()))
            {
                result.ModelsSaved++;
            }

            var standardRowId = await UpsertTorqueStandardRowAsync(
                rowKey,
                processNo,
                stepNo,
                item,
                toolIndex,
                ResolveToolCategory(toolIndex),
                itemCheck,
                nutSpec,
                nutUsage,
                tool,
                subTool,
                workType,
                modelName,
                page);
            result.StandardsSaved++;

            if (min.HasValue || max.HasValue)
            {
                await UpsertTorqueStandardSpecAsync(standardRowId, modelId, min, max, unit);
                result.SpecsSaved++;
            }
        }

        return result;
    }

    private async Task<int> UpsertTorqueModelAsync(string modelName)
    {
        await ExecuteNonQueryAsync(@"
INSERT INTO assembly_torque_models (model_name, is_deleted)
VALUES (@model_name, 0)
ON DUPLICATE KEY UPDATE
    description = VALUES(description),
    note = VALUES(note),
    is_deleted = 0",
            ("@model_name", TrimTo(modelName, 80)));

        return Convert.ToInt32(await ExecuteScalarAsync(
            "SELECT id FROM assembly_torque_models WHERE model_name = @model_name LIMIT 1",
            ("@model_name", TrimTo(modelName, 80))) ?? 0, CultureInfo.InvariantCulture);
    }

    private async Task UpsertEngineModelMasterAsync(string modelName)
    {
        await ExecuteNonQueryAsync(@"
INSERT INTO engine_models (engine_model, description, note, is_deleted)
VALUES (@engine_model, @description, @note, 0)
ON DUPLICATE KEY UPDATE
    is_deleted = 0,
    updated_at = CURRENT_TIMESTAMP",
            ("@engine_model", TrimTo(modelName, 45)),
            ("@description", "Torque Master"),
            ("@note", "Imported from Torque Master"));
    }

    private async Task<long> UpsertTorqueStandardRowAsync(
        string rowKey,
        int? processNo,
        int? stepNo,
        string item,
        int? toolIndex,
        string toolCategory,
        string? itemCheck,
        string? nutSpec,
        int? nutUsage,
        int? tool,
        int? subTool,
        string? workType,
        string? modelPage,
        int? page)
    {
        await ExecuteNonQueryAsync(@"
INSERT INTO assembly_torque_standard_rows
    (row_key, process_no, step_no, item, tool_index, tool_category, item_check, nut_spec, nut_usage, tool, sub_tool, work_type, model_page, page, is_deleted)
VALUES
    (@row_key, @process_no, @step_no, @item, @tool_index, @tool_category, @item_check, @nut_spec, @nut_usage, @tool, @sub_tool, @work_type, @model_page, @page, 0)
ON DUPLICATE KEY UPDATE
    process_no = VALUES(process_no),
    step_no = VALUES(step_no),
    item = VALUES(item),
    tool_index = VALUES(tool_index),
    tool_category = VALUES(tool_category),
    item_check = VALUES(item_check),
    nut_spec = VALUES(nut_spec),
    nut_usage = VALUES(nut_usage),
    tool = VALUES(tool),
    sub_tool = VALUES(sub_tool),
    work_type = VALUES(work_type),
    model_page = VALUES(model_page),
    page = VALUES(page),
    is_deleted = 0,
    updated_at = CURRENT_TIMESTAMP",
            ("@row_key", rowKey),
            ("@process_no", processNo),
            ("@step_no", stepNo),
            ("@item", TrimTo(item, 200)),
            ("@tool_index", toolIndex),
            ("@tool_category", ResolveToolCategory(toolIndex, toolCategory)),
            ("@item_check", string.IsNullOrWhiteSpace(itemCheck) ? null : TrimTo(itemCheck, 200)),
            ("@nut_spec", string.IsNullOrWhiteSpace(nutSpec) ? null : TrimTo(nutSpec, 80)),
            ("@nut_usage", nutUsage),
            ("@tool", tool),
            ("@sub_tool", subTool),
            ("@work_type", string.IsNullOrWhiteSpace(workType) ? null : TrimTo(workType, 10)),
            ("@model_page", string.IsNullOrWhiteSpace(modelPage) ? null : TrimTo(modelPage, 40)),
            ("@page", page));

        return Convert.ToInt64(await ExecuteScalarAsync(
            "SELECT id FROM assembly_torque_standard_rows WHERE row_key = @row_key LIMIT 1",
            ("@row_key", rowKey)) ?? 0, CultureInfo.InvariantCulture);
    }

    private async Task UpsertTorqueStandardSpecAsync(long standardRowId, int modelId, decimal? min, decimal? max, string? unit)
    {
        await ExecuteNonQueryAsync(@"
INSERT INTO assembly_torque_standard_specs
    (standard_row_id, torque_model_id, min_value, max_value, unit)
VALUES
    (@standard_row_id, @torque_model_id, @min_value, @max_value, @unit)
ON DUPLICATE KEY UPDATE
    min_value = VALUES(min_value),
    max_value = VALUES(max_value),
    unit = VALUES(unit),
    updated_at = CURRENT_TIMESTAMP",
            ("@standard_row_id", standardRowId),
            ("@torque_model_id", modelId),
            ("@min_value", min),
            ("@max_value", max),
            ("@unit", string.IsNullOrWhiteSpace(unit) ? null : TrimTo(unit, 50)));
    }

    private async Task SeedTorqueMasterFromLegacyDatabaseAsync()
    {
        var hasRows = await ScalarLongAsync("SELECT COUNT(*) FROM assembly_torque_standard_rows");
        if (hasRows > 0)
        {
            return;
        }

        var hasLegacyDatabase = await ScalarLongAsync("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = 'yanmartightening'");
        if (hasLegacyDatabase == 0)
        {
            return;
        }

        var hasLegacyTable = await ScalarLongAsync(@"
SELECT COUNT(*)
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'yanmartightening' AND TABLE_NAME = 'standardmaster'");
        if (hasLegacyTable == 0)
        {
            return;
        }

        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO assembly_torque_models (legacy_model_id, model_name, is_deleted)
SELECT mm.Id, COALESCE(NULLIF(TRIM(mm.Model), ''), CONCAT('Model ', sm.MachineModelId)), COALESCE(mm.IsDeleted, 0)
FROM yanmartightening.standardmaster sm
LEFT JOIN yanmartightening.machinemodel mm ON mm.Id = sm.MachineModelId
WHERE sm.MachineModelId IS NOT NULL
GROUP BY mm.Id, mm.Model, sm.MachineModelId, mm.IsDeleted
ON DUPLICATE KEY UPDATE
    legacy_model_id = VALUES(legacy_model_id),
    is_deleted = VALUES(is_deleted),
    updated_at = CURRENT_TIMESTAMP");

        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO assembly_torque_standard_rows
    (row_key, process_no, step_no, item, tool_index, tool_category, item_check, nut_spec, nut_usage, tool, sub_tool, work_type, model_page, page, is_deleted)
SELECT
    MD5(CONCAT_WS('|',
        COALESCE(sm.ProcessNo, -1),
        COALESCE(sm.StepNo, -1),
        COALESCE(TRIM(sm.Item), ''),
        COALESCE(sm.ToolIndex, -1),
        COALESCE(TRIM(sm.ItemCheck), ''),
        COALESCE(TRIM(sm.NutSpec), ''),
        COALESCE(sm.Tool, -1),
        COALESCE(sm.SubTool, -1),
        COALESCE(TRIM(sm.WorkType), '')
    )) AS row_key,
    MIN(sm.ProcessNo),
    MIN(sm.StepNo),
    MAX(NULLIF(TRIM(sm.Item), '')),
    MIN(sm.ToolIndex),
    CASE
        WHEN MIN(sm.ToolIndex) = 1 THEN 'Nut Runner'
        WHEN MIN(sm.ToolIndex) = 2 THEN 'Torque Wrench'
        ELSE 'Visual Inspect'
    END,
    MAX(NULLIF(TRIM(sm.ItemCheck), '')),
    MAX(NULLIF(TRIM(sm.NutSpec), '')),
    MAX(sm.NutUsage),
    MAX(sm.Tool),
    MAX(sm.SubTool),
    NULLIF(TRIM(MAX(sm.WorkType)), ''),
    NULLIF(TRIM(MAX(sm.ModelPage)), ''),
    MIN(sm.Page),
    0
FROM yanmartightening.standardmaster sm
GROUP BY
    MD5(CONCAT_WS('|',
        COALESCE(sm.ProcessNo, -1),
        COALESCE(sm.StepNo, -1),
        COALESCE(TRIM(sm.Item), ''),
        COALESCE(sm.ToolIndex, -1),
        COALESCE(TRIM(sm.ItemCheck), ''),
        COALESCE(TRIM(sm.NutSpec), ''),
        COALESCE(sm.Tool, -1),
        COALESCE(sm.SubTool, -1),
        COALESCE(TRIM(sm.WorkType), '')
    ))
ON DUPLICATE KEY UPDATE
    process_no = VALUES(process_no),
    step_no = VALUES(step_no),
    item = VALUES(item),
    tool_index = VALUES(tool_index),
    tool_category = VALUES(tool_category),
    item_check = VALUES(item_check),
    nut_spec = VALUES(nut_spec),
    nut_usage = VALUES(nut_usage),
    tool = VALUES(tool),
    sub_tool = VALUES(sub_tool),
    work_type = VALUES(work_type),
    model_page = VALUES(model_page),
    page = VALUES(page),
    updated_at = CURRENT_TIMESTAMP");

        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO assembly_torque_standard_specs
    (standard_row_id, torque_model_id, min_value, max_value, unit)
SELECT
    rows_master.id,
    models.id,
    MIN(sm.Min),
    MAX(sm.Max),
    NULLIF(TRIM(MAX(sm.Unit)), '')
FROM yanmartightening.standardmaster sm
JOIN assembly_torque_models models ON models.legacy_model_id = sm.MachineModelId
JOIN assembly_torque_standard_rows rows_master ON rows_master.row_key = MD5(CONCAT_WS('|',
        COALESCE(sm.ProcessNo, -1),
        COALESCE(sm.StepNo, -1),
        COALESCE(TRIM(sm.Item), ''),
        COALESCE(sm.ToolIndex, -1),
        COALESCE(TRIM(sm.ItemCheck), ''),
        COALESCE(TRIM(sm.NutSpec), ''),
        COALESCE(sm.Tool, -1),
        COALESCE(sm.SubTool, -1),
        COALESCE(TRIM(sm.WorkType), '')
    ))
WHERE sm.Min IS NOT NULL OR sm.Max IS NOT NULL
GROUP BY rows_master.id, models.id
ON DUPLICATE KEY UPDATE
    min_value = VALUES(min_value),
    max_value = VALUES(max_value),
    unit = VALUES(unit),
    updated_at = CURRENT_TIMESTAMP");
    }

    private async Task<List<TorqueMasterModelResponse>> ReadTorqueMasterModelsAsync(IReadOnlyList<int> selectedModelIds)
    {
        var models = new List<TorqueMasterModelResponse>();
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        if (selectedModelIds.Count > 0)
        {
            command.CommandText = $@"
SELECT torque_models.id, engine_models.engine_model
FROM engine_models
JOIN assembly_torque_models torque_models
    ON torque_models.model_name = engine_models.engine_model COLLATE utf8mb4_unicode_ci
    AND torque_models.is_deleted != 1
WHERE engine_models.is_deleted != 1
    AND torque_models.id IN ({string.Join(",", selectedModelIds)})
ORDER BY engine_models.engine_model
LIMIT 8";
        }
        else
        {
            command.CommandText = @"
SELECT torque_models.id, engine_models.engine_model
FROM engine_models
JOIN assembly_torque_models torque_models
    ON torque_models.model_name = engine_models.engine_model COLLATE utf8mb4_unicode_ci
    AND torque_models.is_deleted != 1
LEFT JOIN assembly_torque_standard_specs specs ON specs.torque_model_id = torque_models.id
WHERE engine_models.is_deleted != 1
GROUP BY torque_models.id, engine_models.engine_model
ORDER BY
    engine_models.engine_model
LIMIT 6";
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            models.Add(new TorqueMasterModelResponse
            {
                Id = reader.GetInt32(0),
                ModelName = reader.GetString(1)
            });
        }

        return models;
    }

    private async Task<List<TorqueMasterRowResponse>> ReadTorqueMasterRowsAsync(IReadOnlyList<TorqueMasterModelResponse> models, int? processNo, string? search)
    {
        var modelIds = models.Select(x => x.Id).ToList();
        if (modelIds.Count == 0)
        {
            return [];
        }

        var rows = new List<TorqueMasterRowResponse>();
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        var filters = new List<string>
        {
            "rows_master.is_deleted != 1"
        };

        if (models.Count == 1)
        {
            filters.Add("(rows_master.model_page = @model_page OR specs.torque_model_id IS NOT NULL)");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@model_page";
            parameter.Value = models[0].ModelName;
            command.Parameters.Add(parameter);
        }
        else
        {
            filters.Add("specs.torque_model_id IS NOT NULL");
        }

        if (processNo.HasValue)
        {
            filters.Add("rows_master.process_no = @process_no");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@process_no";
            parameter.Value = processNo.Value;
            command.Parameters.Add(parameter);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filters.Add("(rows_master.item LIKE @search OR rows_master.item_check LIKE @search OR rows_master.nut_spec LIKE @search)");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@search";
            parameter.Value = $"%{search.Trim()}%";
            command.Parameters.Add(parameter);
        }

        command.CommandText = $@"
SELECT
    rows_master.id,
    rows_master.process_no,
    rows_master.step_no,
    rows_master.item,
    rows_master.tool_index,
    rows_master.tool_category,
    rows_master.item_check,
    rows_master.nut_spec,
    rows_master.nut_usage,
    rows_master.tool,
    rows_master.sub_tool,
    rows_master.work_type,
    rows_master.model_page,
    rows_master.page,
    specs.torque_model_id,
    specs.min_value,
    specs.max_value,
    specs.unit
FROM assembly_torque_standard_rows rows_master
LEFT JOIN assembly_torque_standard_specs specs
    ON specs.standard_row_id = rows_master.id
    AND specs.torque_model_id IN ({string.Join(",", modelIds)})
WHERE {string.Join(" AND ", filters)}
ORDER BY
    COALESCE(rows_master.process_no, 9999),
    COALESCE(rows_master.step_no, 9999),
    rows_master.item,
    rows_master.item_check
LIMIT 600";

        var rowById = new Dictionary<long, TorqueMasterRowResponse>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var rowId = reader.GetInt64(0);
            if (!rowById.TryGetValue(rowId, out var row))
            {
                var toolIndex = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
                row = new TorqueMasterRowResponse
                {
                    Id = rowId,
                    ProcessNo = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    StepNo = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Item = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ToolIndex = toolIndex,
                    ToolType = ResolveToolCategory(toolIndex, reader.IsDBNull(5) ? null : reader.GetString(5)),
                    ItemCheck = reader.IsDBNull(6) ? null : reader.GetString(6),
                    NutSpec = reader.IsDBNull(7) ? null : reader.GetString(7),
                    NutUsage = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    Tool = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    SubTool = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                    WorkType = reader.IsDBNull(11) ? null : reader.GetString(11),
                    ModelPage = reader.IsDBNull(12) ? null : reader.GetString(12),
                    Page = reader.IsDBNull(13) ? null : reader.GetInt32(13)
                };
                rowById.Add(rowId, row);
                rows.Add(row);
            }

            if (!reader.IsDBNull(14))
            {
                var modelId = reader.GetInt32(14);
                row.Specs[modelId] = new TorqueMasterSpecResponse
                {
                    Min = reader.IsDBNull(15) ? null : reader.GetDecimal(15),
                    Max = reader.IsDBNull(16) ? null : reader.GetDecimal(16),
                    Unit = reader.IsDBNull(17) ? null : reader.GetString(17)
                };
            }
        }

        return rows;
    }

    private async Task<long> ScalarLongAsync(string sql)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(value ?? 0, CultureInfo.InvariantCulture);
    }

    private async Task<object?> ExecuteScalarAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddCommandParameters(command, parameters);
        return await command.ExecuteScalarAsync();
    }

    private async Task ExecuteNonQueryAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddCommandParameters(command, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddCommandParameters(IDbCommand command, params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static string BuildTorqueMasterRowKey(
        int? processNo,
        int? stepNo,
        string? item,
        int? toolIndex,
        string? itemCheck,
        string? nutSpec,
        int? tool,
        int? subTool,
        string? workType)
    {
        var raw = string.Join("|",
            processNo?.ToString(CultureInfo.InvariantCulture) ?? "-1",
            stepNo?.ToString(CultureInfo.InvariantCulture) ?? "-1",
            item?.Trim() ?? string.Empty,
            toolIndex?.ToString(CultureInfo.InvariantCulture) ?? "-1",
            itemCheck?.Trim() ?? string.Empty,
            nutSpec?.Trim() ?? string.Empty,
            tool?.ToString(CultureInfo.InvariantCulture) ?? "-1",
            subTool?.ToString(CultureInfo.InvariantCulture) ?? "-1",
            workType?.Trim() ?? string.Empty);

        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string NormalizeExcelHeader(string? value)
    {
        return new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static string? ReadExcelText(IXLRow row, IReadOnlyDictionary<string, int> headers, params string[] names)
    {
        foreach (var name in names.Select(NormalizeExcelHeader))
        {
            if (headers.TryGetValue(name, out var column))
            {
                var text = row.Cell(column).GetFormattedString().Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }

        return null;
    }

    private static int? ReadExcelInt(IXLRow row, IReadOnlyDictionary<string, int> headers, params string[] names)
    {
        var text = ReadExcelText(row, headers, names);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue)
            ? (int)decimalValue
            : null;
    }

    private static decimal? ReadExcelDecimal(IXLRow row, IReadOnlyDictionary<string, int> headers, params string[] names)
    {
        var text = ReadExcelText(row, headers, names);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static List<int> ParseIdList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .Take(8)
            .ToList();
    }

    private static string ResolveToolCategory(int? toolIndex, string? category = null)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalized = category.Trim();
            if (normalized.Equals("Torque Wrench", StringComparison.OrdinalIgnoreCase))
            {
                return "Torque Wrench";
            }

            if (normalized.Equals("Nut Runner", StringComparison.OrdinalIgnoreCase))
            {
                return "Nut Runner";
            }

            if (normalized.Equals("Visual Inspect", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("No Use", StringComparison.OrdinalIgnoreCase))
            {
                return "Visual Inspect";
            }
        }

        return toolIndex switch
        {
            1 => "Nut Runner",
            2 => "Torque Wrench",
            _ => "Visual Inspect"
        };
    }

    private async Task EnsureAssemblyWorkstationMasterTablesAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS assembly_workstations (
    id INT AUTO_INCREMENT PRIMARY KEY,
    workstation_code VARCHAR(50) NOT NULL,
    workstation_name VARCHAR(120) NOT NULL,
    workstation_no INT NOT NULL,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_workstations_code (workstation_code),
    KEY ix_assembly_workstations_no (workstation_no)
)");

        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS assembly_tools (
    id INT AUTO_INCREMENT PRIMARY KEY,
    workstation_id INT NOT NULL,
    tool_code VARCHAR(50) NOT NULL,
    tool_name VARCHAR(120) NOT NULL,
    nut_size VARCHAR(40) NOT NULL,
    program_no INT NULL,
    torque_standard DECIMAL(8, 2) NOT NULL DEFAULT 0,
    torque_min DECIMAL(8, 2) NOT NULL DEFAULT 0,
    torque_max DECIMAL(8, 2) NOT NULL DEFAULT 0,
    unit VARCHAR(20) NOT NULL DEFAULT 'N.m',
    sequence_no INT NOT NULL DEFAULT 0,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_tools_workstation_tool (workstation_id, tool_code),
    KEY ix_assembly_tools_nut_size (nut_size),
    KEY ix_assembly_tools_sequence_no (sequence_no),
    CONSTRAINT fk_assembly_tools_workstation
        FOREIGN KEY (workstation_id) REFERENCES assembly_workstations (id)
        ON UPDATE CASCADE
        ON DELETE CASCADE
)");

        await EnsureColumnAbsentAsync(
            "assembly_tools",
            "drive_size",
            "ALTER TABLE assembly_tools DROP COLUMN drive_size");
    }

    private async Task EnsureSystemSettingsTablesAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS measurement_units (
    id INT AUTO_INCREMENT PRIMARY KEY,
    unit_category VARCHAR(50) NOT NULL,
    unit_symbol VARCHAR(20) NOT NULL,
    unit_name VARCHAR(80) NOT NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_measurement_units_category_symbol (unit_category, unit_symbol)
)");

        await _db.Database.ExecuteSqlRawAsync(@"
INSERT INTO measurement_units
    (unit_category, unit_symbol, unit_name, is_deleted)
VALUES
    ('pressure', 'MPa', 'Megapascal', 0),
    ('cycle_time', 's', 'Second', 0)
ON DUPLICATE KEY UPDATE
    unit_name = VALUES(unit_name),
    is_deleted = VALUES(is_deleted),
    updated_at = CURRENT_TIMESTAMP");

        await _db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS system_settings (
    id INT PRIMARY KEY,
    pressure_unit_id INT NOT NULL,
    cycle_time_unit_id INT NOT NULL,
    backup_db_location VARCHAR(500) NULL,
    backup_schedule VARCHAR(20) NOT NULL DEFAULT 'daily',
    plc_ip_address VARCHAR(80) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_system_settings_pressure_unit
        FOREIGN KEY (pressure_unit_id) REFERENCES measurement_units (id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,
    CONSTRAINT fk_system_settings_cycle_time_unit
        FOREIGN KEY (cycle_time_unit_id) REFERENCES measurement_units (id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT
)");

        await EnsureColumnAsync(
            "system_settings",
            "plc_ip_address",
            "ALTER TABLE system_settings ADD COLUMN plc_ip_address VARCHAR(80) NULL AFTER backup_schedule");

        await EnsureDefaultSystemSettingAsync();
    }

    private async Task EnsureDefaultSystemSettingAsync()
    {
        var exists = await _db.SystemSettings.AsNoTracking().AnyAsync(x => x.Id == 1);
        if (exists)
        {
            return;
        }

        var pressureUnitId = await FindOrCreateMeasurementUnitAsync("pressure", "MPa", "Megapascal");
        var cycleTimeUnitId = await FindOrCreateMeasurementUnitAsync("cycle_time", "s", "Second");

        _db.SystemSettings.Add(new SystemSetting
        {
            Id = 1,
            PressureUnitId = pressureUnitId,
            CycleTimeUnitId = cycleTimeUnitId,
            BackupSchedule = "daily",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }

    private async Task<SystemSettingsResponse> GetSystemSettingsResponseAsync()
    {
        var setting = await _db.SystemSettings
            .AsNoTracking()
            .Include(x => x.PressureUnit)
            .Include(x => x.CycleTimeUnit)
            .FirstOrDefaultAsync(x => x.Id == 1);

        return new SystemSettingsResponse
        {
            PressureUnit = setting?.PressureUnit?.UnitSymbol ?? "MPa",
            CycleTimeUnit = setting?.CycleTimeUnit?.UnitSymbol ?? "s",
            BackupDbLocation = setting?.BackupDbLocation ?? string.Empty,
            BackupSchedule = setting?.BackupSchedule ?? "daily",
            PlcIpAddress = setting?.PlcIpAddress ?? string.Empty
        };
    }

    private static async Task<bool> CheckPlcReachableAsync(string plcIpAddress)
    {
        if (string.IsNullOrWhiteSpace(plcIpAddress))
        {
            return false;
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(plcIpAddress, 1000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckTcpReachableAsync(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
        {
            return false;
        }

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(1000));
            if (completedTask != connectTask)
            {
                return false;
            }

            await connectTask;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static MqttBrokerStatusSettings LoadMqttBrokerStatusSettings()
    {
        var settingsPath = ResolveMqttBrokerSettingsPath();
        var host = ReadIniValue(settingsPath, "Broker", "Host") ?? "localhost";
        var portText = ReadIniValue(settingsPath, "Broker", "Port");
        var port = int.TryParse(portText, out var parsedPort) && parsedPort > 0
            ? parsedPort
            : 1883;

        if (host == "0.0.0.0" || host == "::")
        {
            host = "localhost";
        }

        return new MqttBrokerStatusSettings(host, port);
    }

    private static string ResolveMqttBrokerSettingsPath()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Settings.ini"),
            Path.Combine(Directory.GetCurrentDirectory(), "Backend", "MqttBrokerService", "Settings.ini"),
            Path.Combine(Directory.GetCurrentDirectory(), "MqttBrokerService", "Settings.ini"),
            Path.Combine(Directory.GetCurrentDirectory(), "Settings.ini")
        };

        return candidatePaths.FirstOrDefault(System.IO.File.Exists) ?? candidatePaths[0];
    }

    private static string? ReadIniValue(string path, string section, string key)
    {
        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        var currentSection = string.Empty;
        foreach (var rawLine in System.IO.File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            if (!string.Equals(currentSection, section, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var entryKey = line[..separatorIndex].Trim();
            var entryValue = line[(separatorIndex + 1)..].Trim();
            if (string.Equals(entryKey, key, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(entryValue))
            {
                return entryValue;
            }
        }

        return null;
    }

    private sealed record MqttBrokerStatusSettings(string Host, int Port);

    private async Task<int> FindOrCreateMeasurementUnitAsync(string category, string? symbol, string? name)
    {
        var unitSymbol = string.IsNullOrWhiteSpace(symbol) ? (category == "pressure" ? "MPa" : "s") : TrimTo(symbol, 20);
        var unitName = string.IsNullOrWhiteSpace(name) ? unitSymbol : TrimTo(name, 80);

        var existing = await _db.MeasurementUnits
            .FirstOrDefaultAsync(x => x.UnitCategory == category && x.UnitSymbol == unitSymbol);
        if (existing is not null)
        {
            if (existing.IsDeleted == true)
            {
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            return existing.Id;
        }

        var unit = new MeasurementUnit
        {
            UnitCategory = category,
            UnitSymbol = unitSymbol,
            UnitName = unitName,
            IsDeleted = false,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.MeasurementUnits.Add(unit);
        await _db.SaveChangesAsync();
        return unit.Id;
    }

    private static string NormalizeBackupSchedule(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "weekly" => "weekly",
            "monthly" => "monthly",
            _ => "daily"
        };
    }
}
