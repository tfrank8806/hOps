#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Utilities;
using hOps.web.ViewModels.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hOps.web.Controllers
{
    [Authorize]
    [AutoValidateAntiforgeryToken]
    [Route("Maintenance/Logs")]
    public class MaintenanceLogsController : BaseController
    {
        private const int MaxEntryDisplayCount = 500;
        private const int TemplateColumnImportLimit = 120;
        private const string LogsIndexView = "~/Views/Maintenance/Logs/Index.cshtml";
        private const string LogsEditorView = "~/Views/Maintenance/Logs/Editor.cshtml";
        private const string LogsDetailView = "~/Views/Maintenance/Logs/Detail.cshtml";
        private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        private const string PhotoUploadFolder = "uploads/maintenance-logs";

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private static readonly JsonSerializerOptions EntrySerializerOptions = new(JsonSerializerDefaults.Web);

        public MaintenanceLogsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
            : base(context, userManager)
        {
            _db = context;
            _environment = environment;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to view maintenance logs.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);

            var templates = await _db.MaintenanceLogTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

            var templateIds = templates.Select(t => t.Id).ToList();
            var stats = await _db.MaintenanceLogEntries
                .Where(e => templateIds.Contains(e.TemplateId))
                .GroupBy(e => e.TemplateId)
                .Select(group => new
                {
                    TemplateId = group.Key,
                    Count = group.Count(),
                    LastDate = group.Max(e => e.EntryDate)
                })
                .ToListAsync();
            var statsLookup = stats.ToDictionary(s => s.TemplateId, s => s);

            var summaries = templates.Select(template =>
            {
                statsLookup.TryGetValue(template.Id, out var templateStats);
                return new MaintenanceLogTemplateSummaryViewModel
                {
                    Id = template.Id,
                    Name = template.Name,
                    ScheduleType = template.ScheduleType,
                    ScheduleSummary = MaintenanceLogTemplateHelper.BuildScheduleSummary(template),
                    IsActive = template.IsActive,
                    EntryCount = templateStats?.Count ?? 0,
                    LastEntryDate = templateStats?.LastDate
                };
            }).ToList();

            var viewModel = new MaintenanceLogsIndexViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = canManage,
                Templates = summaries
            };

            ViewBag.MaintenanceLogMessage = TempData["MaintenanceLogMessage"];
            ViewBag.MaintenanceLogError = TempData["MaintenanceLogError"];

            return View(LogsIndexView, viewModel);
        }

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before creating a maintenance log template.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var viewModel = new MaintenanceLogTemplateEditorViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = true,
                Columns = new List<MaintenanceLogColumnEditorViewModel>
                {
                    new MaintenanceLogColumnEditorViewModel { Label = "Task / Item", Key = "task", Required = true },
                    new MaintenanceLogColumnEditorViewModel { Label = "Status", Key = "status" }
                }
            };

            return View(LogsEditorView, viewModel);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create(
            MaintenanceLogTemplateEditorViewModel viewModel,
            IFormFile? templateCsvFile = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before creating a maintenance log template.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.CanManage = true;

            if (templateCsvFile is { Length: > 0 } && (viewModel.Columns == null || !viewModel.Columns.Any()))
            {
                try
                {
                    viewModel.Columns = await ParseTemplateColumnsAsync(templateCsvFile);
                    if (!viewModel.Columns.Any())
                    {
                        ModelState.AddModelError("TemplateCsvFile", "The CSV file did not include any columns.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("TemplateCsvFile", ex.Message);
                }
            }

            var sanitizedColumns = BuildColumnDefinitions(viewModel);
            if (!sanitizedColumns.Any())
            {
                ModelState.AddModelError(string.Empty, "Add at least one column to capture log entries.");
            }

            if (!ModelState.IsValid)
            {
                return View(LogsEditorView, viewModel);
            }

            var maxDisplayOrder = await _db.MaintenanceLogTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderByDescending(t => t.DisplayOrder)
                .Select(t => (int?)t.DisplayOrder)
                .FirstOrDefaultAsync() ?? -1;

            var template = new MaintenanceLogTemplate
            {
                Name = viewModel.Name.Trim(),
                PropertyId = property.Id,
                ScheduleType = viewModel.ScheduleType,
                WeeklyDaysBitmask = viewModel.ScheduleType == MaintenanceLogScheduleType.Weekly
                    ? MaintenanceLogTemplateHelper.BuildWeeklyBitmask(GetSelectedDays(viewModel))
                    : 0,
                DayOfMonth = RequiresDayOfMonth(viewModel.ScheduleType) ? viewModel.DayOfMonth : null,
                DueTimeLocal = viewModel.DueTimeLocal,
                IsActive = viewModel.IsActive,
                DisplayOrder = maxDisplayOrder + 1,
                ColumnsJson = MaintenanceLogTemplateHelper.BuildColumnsJson(sanitizedColumns),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.MaintenanceLogTemplates.Add(template);
            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Maintenance log template created.";
            return RedirectToAction(nameof(Detail), new { id = template.Id });
        }

        [HttpGet("{id:int}/Edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before editing maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var columns = MaintenanceLogTemplateHelper.ParseColumns(template.ColumnsJson);
            var viewModel = new MaintenanceLogTemplateEditorViewModel
            {
                Id = template.Id,
                PropertyId = property.Id,
                PropertyName = property.Name,
                CanManage = true,
                Name = template.Name,
                ScheduleType = template.ScheduleType,
                DayOfMonth = template.DayOfMonth,
                WeeklyDays = BuildWeeklySelection(template.WeeklyDaysBitmask),
                DueTimeLocal = template.DueTimeLocal,
                IsActive = template.IsActive,
                Columns = BuildColumnEditors(columns)
            };

            return View(LogsEditorView, viewModel);
        }

        [HttpPost("{id:int}/Edit")]
        public async Task<IActionResult> Edit(
            int id,
            MaintenanceLogTemplateEditorViewModel viewModel,
            IFormFile? templateCsvFile = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before editing maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            viewModel.Id = id;
            viewModel.PropertyId = property.Id;
            viewModel.PropertyName = property.Name;
            viewModel.CanManage = true;

            if (templateCsvFile is { Length: > 0 } && (viewModel.Columns == null || !viewModel.Columns.Any()))
            {
                try
                {
                    viewModel.Columns = await ParseTemplateColumnsAsync(templateCsvFile);
                    if (!viewModel.Columns.Any())
                    {
                        ModelState.AddModelError("TemplateCsvFile", "The CSV file did not include any columns.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("TemplateCsvFile", ex.Message);
                }
            }

            var sanitizedColumns = BuildColumnDefinitions(viewModel);
            if (!sanitizedColumns.Any())
            {
                ModelState.AddModelError(string.Empty, "Add at least one column to capture log entries.");
            }

            if (!ModelState.IsValid)
            {
                return View(LogsEditorView, viewModel);
            }

            template.Name = viewModel.Name.Trim();
            template.ScheduleType = viewModel.ScheduleType;
            template.WeeklyDaysBitmask = viewModel.ScheduleType == MaintenanceLogScheduleType.Weekly
                ? MaintenanceLogTemplateHelper.BuildWeeklyBitmask(GetSelectedDays(viewModel))
                : 0;
                template.DayOfMonth = RequiresDayOfMonth(viewModel.ScheduleType) ? viewModel.DayOfMonth : null;
            template.DueTimeLocal = viewModel.DueTimeLocal;
            template.IsActive = viewModel.IsActive;
            template.ColumnsJson = MaintenanceLogTemplateHelper.BuildColumnsJson(sanitizedColumns);
            template.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Maintenance log template updated.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detail(int id, DateTime? start = null, DateTime? end = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property to view maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var canManage = UserCanManage(roles);

            var template = await _db.MaintenanceLogTemplates
                .Include(t => t.Property)
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var viewModel = await BuildDetailViewModelAsync(template, canManage, start, end);

            ViewBag.MaintenanceLogMessage = TempData["MaintenanceLogMessage"];
            ViewBag.MaintenanceLogError = TempData["MaintenanceLogError"];

            return View(LogsDetailView, viewModel);
        }

        [HttpPost("{id:int}/Entries")]
        public async Task<IActionResult> CreateEntry(int id, MaintenanceLogEntryInputModel input, DateTime? start = null, DateTime? end = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before recording maintenance log entries.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var template = await _db.MaintenanceLogTemplates
                .Include(t => t.Property)
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var columns = MaintenanceLogTemplateHelper.ParseColumns(template.ColumnsJson);
            if (!columns.Any())
            {
                TempData["MaintenanceLogError"] = "This template has no columns configured.";
                return RedirectToAction(nameof(Detail), new { id });
            }

            if (input.EntryDate == default)
            {
                ModelState.AddModelError(nameof(input.EntryDate), "Select a date for this entry.");
            }

            input.Values ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            input.Notes ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var photoUploads = CollectPhotoUploads(Request.Form.Files, columns);

            var normalizedValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                input.Values.TryGetValue(column.Key, out var raw);

                var normalized = NormalizeValue(raw);
                if (column.Type == "checkbox")
                {
                    normalized = raw?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ? "true" : "false";
                    if (normalizedValues.TryGetValue(column.Key, out var existing) &&
                        string.Equals(existing, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    normalizedValues[column.Key] = normalized;
                }
                else
                {
                    if (column.Type == "select" && column.Options.Any())
                    {
                        if (string.IsNullOrWhiteSpace(normalized))
                        {
                            normalized = null;
                        }
                        else if (!column.Options.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                        {
                            ModelState.AddModelError($"Values[{column.Key}]", $"Select a value from the available options for {column.Label}.");
                        }
                    }

                    if (column.Type == "number" && !string.IsNullOrWhiteSpace(normalized) && !decimal.TryParse(normalized, out _))
                    {
                        ModelState.AddModelError($"Values[{column.Key}]", $"{column.Label} must be a number.");
                    }

                    if (column.Required && string.IsNullOrWhiteSpace(normalized))
                    {
                        ModelState.AddModelError($"Values[{column.Key}]", $"{column.Label} is required.");
                    }

                    normalizedValues[column.Key] = normalized;
                }

                if (column.IncludeNotes)
                {
                    input.Notes.TryGetValue(column.Key, out var rawNote);
                    var normalizedNote = NormalizeValue(rawNote);
                    var noteKey = MaintenanceLogTemplateHelper.BuildNotesKey(column.Key);
                    if (!string.IsNullOrWhiteSpace(normalizedNote) && !string.IsNullOrWhiteSpace(noteKey))
                    {
                        normalizedValues[noteKey] = normalizedNote;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                var detailModel = await BuildDetailViewModelAsync(template, await UserCanManageAsync(user), start, end);
                ViewBag.EntryInput = input;
                return View(LogsDetailView, detailModel);
            }

            var savedPhotoPaths = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in photoUploads)
            {
                var paths = await SavePhotoFilesAsync(kvp.Value);
                if (paths.Count > 0)
                {
                    savedPhotoPaths[kvp.Key] = paths;
                }
            }

            foreach (var kvp in savedPhotoPaths)
            {
                var photoKey = MaintenanceLogTemplateHelper.BuildPhotosKey(kvp.Key);
                if (!string.IsNullOrWhiteSpace(photoKey))
                {
                    normalizedValues[photoKey] = JsonSerializer.Serialize(kvp.Value, EntrySerializerOptions);
                }
            }

            var normalizedEntryDate = DateTime.SpecifyKind(input.EntryDate.Date, DateTimeKind.Utc);

            var entry = new MaintenanceLogEntry
            {
                TemplateId = template.Id,
                EntryDate = normalizedEntryDate,
                ValuesJson = JsonSerializer.Serialize(normalizedValues, EntrySerializerOptions),
                CreatedByUserId = user.Id,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.MaintenanceLogEntries.Add(entry);
            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Log entry recorded.";
            return RedirectToAction(nameof(Detail), new
            {
                id,
                start = start?.ToString("yyyy-MM-dd"),
                end = end?.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost("{templateId:int}/Entries/{entryId:int}/Delete")]
        public async Task<IActionResult> DeleteEntry(int templateId, int entryId, DateTime? start = null, DateTime? end = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before editing maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            var entry = await _db.MaintenanceLogEntries
                .Include(e => e.Template)
                .FirstOrDefaultAsync(e => e.Id == entryId && e.TemplateId == templateId && e.Template.PropertyId == property.Id);

            if (entry == null)
            {
                TempData["MaintenanceLogError"] = "Log entry not found.";
                return RedirectToAction(nameof(Detail), new { id = templateId });
            }

            DeletePhotoFilesFromJson(entry.ValuesJson);
            _db.MaintenanceLogEntries.Remove(entry);
            await _db.SaveChangesAsync();

            TempData["MaintenanceLogMessage"] = "Log entry deleted.";
            return RedirectToAction(nameof(Detail), new
            {
                id = templateId,
                start = start?.ToString("yyyy-MM-dd"),
                end = end?.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet("{id:int}/Export.csv")]
        public async Task<IActionResult> Export(int id, DateTime? start = null, DateTime? end = null)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                TempData["MaintenanceLogError"] = "Select a property before exporting maintenance logs.";
                return RedirectToAction(nameof(Index));
            }

            var template = await _db.MaintenanceLogTemplates
                .FirstOrDefaultAsync(t => t.Id == id && t.PropertyId == property.Id);
            if (template == null)
            {
                return NotFound();
            }

            var columns = MaintenanceLogTemplateHelper.ParseColumns(template.ColumnsJson);

            var query = _db.MaintenanceLogEntries
                .Include(e => e.CreatedByUser)
                .Where(e => e.TemplateId == template.Id);

            if (start.HasValue)
            {
                var startDate = start.Value.Date;
                query = query.Where(e => e.EntryDate >= startDate);
            }

            if (end.HasValue)
            {
                var endDate = end.Value.Date;
                query = query.Where(e => e.EntryDate <= endDate);
            }

            var entries = await query
                .OrderBy(e => e.EntryDate)
                .ThenBy(e => e.Id)
                .ToListAsync();

            var header = new List<string>
            {
                "Entry Date",
                "Created By",
                "Created At"
            };
            foreach (var column in columns)
            {
                header.Add(column.Label);
                if (column.IncludeNotes)
                {
                    header.Add($"{column.Label} Notes");
                }

                if (column.IncludePhotos)
                {
                    header.Add($"{column.Label} Photos");
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", header.Select(Csv)));

            foreach (var entry in entries)
            {
                var (valueDict, noteDict, photoDict) = BuildEntryDataDictionaries(entry.ValuesJson, columns);
                var row = new List<string>
                {
                    Csv(entry.EntryDate.ToString("yyyy-MM-dd")),
                    Csv(BuildUserName(entry.CreatedByUser)),
                    Csv(entry.CreatedAtUtc.ToString("u"))
                };

                foreach (var column in columns)
                {
                    valueDict.TryGetValue(column.Key, out var value);
                    row.Add(Csv(value));

                    if (column.IncludeNotes)
                    {
                        noteDict.TryGetValue(column.Key, out var noteValue);
                        row.Add(Csv(noteValue));
                    }

                    if (column.IncludePhotos)
                    {
                        photoDict.TryGetValue(column.Key, out var photosForColumn);
                        var joinedPhotos = photosForColumn != null && photosForColumn.Count > 0
                            ? string.Join(" ", photosForColumn)
                            : null;
                        row.Add(Csv(joinedPhotos));
                    }
                }

                builder.AppendLine(string.Join(",", row));
            }

            var safeTemplate = SanitizeFileName(template.Name);
            var safeProperty = SanitizeFileName(property.Name);
            var fileName = $"{safeProperty}-{safeTemplate}-logs-{DateTime.UtcNow:yyyyMMdd}.csv";
            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv", fileName);
        }

        [HttpPost("Reorder")]
        public async Task<IActionResult> Reorder([FromBody] MaintenanceLogTemplateReorderRequest? request)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { error = "Select a property before reordering templates." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!UserCanManage(roles))
            {
                return Forbid();
            }

            if (request?.TemplateIds == null || request.TemplateIds.Count == 0)
            {
                return BadRequest(new { error = "Provide at least one template when reordering." });
            }

            var templates = await _db.MaintenanceLogTemplates
                .Where(t => t.PropertyId == property.Id)
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

            if (!templates.Any())
            {
                return BadRequest(new { error = "No templates exist for this property." });
            }

            var propertyTemplateIds = templates.Select(t => t.Id).ToHashSet();
            var invalidIds = request.TemplateIds.Where(id => !propertyTemplateIds.Contains(id)).ToList();
            if (invalidIds.Any())
            {
                return BadRequest(new { error = "One or more templates are invalid or belong to another property." });
            }

            var orderLookup = request.TemplateIds
                .Select((id, index) => (id, index))
                .GroupBy(item => item.id)
                .ToDictionary(group => group.Key, group => group.First().index);

            var nextOrder = orderLookup.Count;
            foreach (var template in templates)
            {
                if (orderLookup.TryGetValue(template.Id, out var order))
                {
                    template.DisplayOrder = order;
                }
                else
                {
                    template.DisplayOrder = nextOrder++;
                }

                template.UpdatedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet("Template/Download.csv")]
        public IActionResult DownloadTemplateCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Label,Type,Required,Options,Notes,Photos");
            builder.AppendLine("\"Area\",\"text\",\"Yes\",,\"No\",\"No\"");
            builder.AppendLine("\"Status\",\"select\",\"Yes\",\"Operational,Out of Service\",\"No\",\"No\"");
            builder.AppendLine("\"Notes\",\"text\",\"No\",,\"Yes\",\"Yes\"");

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv", "maintenance-log-template.csv");
        }

        [HttpPost("Template/Columns/Preview")]
        public async Task<IActionResult> PreviewTemplateCsv(IFormFile? csvFile)
        {
            var property = ViewBag.CurrentProperty as Property;
            if (property == null)
            {
                return BadRequest(new { error = "Select a property before importing columns." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(new { error = "Choose a CSV file to import." });
            }

            try
            {
                var columns = await ParseTemplateColumnsAsync(csvFile);
                if (!columns.Any())
                {
                    return BadRequest(new { error = "No columns were found in the uploaded template." });
                }

                return Json(new
                {
                    columns = columns.Select(column => new
                    {
                        key = column.Key,
                        label = column.Label,
                        type = column.Type,
                        required = column.Required,
                        optionsText = column.OptionsText,
                        includeNotes = column.IncludeNotes,
                        includePhotos = column.IncludePhotos
                    })
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private async Task<List<MaintenanceLogColumnEditorViewModel>> ParseTemplateColumnsAsync(IFormFile csvFile)
        {
            var rows = new List<MaintenanceLogColumnEditorViewModel>();
            using var stream = csvFile.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            var lineNumber = 0;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (rows.Count >= TemplateColumnImportLimit)
                {
                    throw new InvalidOperationException($"Templates can include up to {TemplateColumnImportLimit} columns.");
                }

                var cells = SplitCsvLine(line);
                if (lineNumber == 1 && cells.Count > 0 && cells[0].Trim().Equals("label", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var label = cells.Count > 0 ? cells[0].Trim() : string.Empty;
                var key = string.Empty;
                var index = 1;

                if (cells.Count > index)
                {
                    var candidate = cells[index].Trim();
                    var normalizedCandidate = candidate.ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(candidate) &&
                        !MaintenanceLogColumnDefinition.AllowedTypes.Contains(normalizedCandidate))
                    {
                        key = candidate;
                        index++;
                    }
                }

                var typeCell = cells.Count > index ? cells[index++].Trim() : string.Empty;
                var requiredCell = cells.Count > index ? cells[index++].Trim() : string.Empty;
                var optionsCell = cells.Count > index ? cells[index++] : string.Empty;
                var notesCell = cells.Count > index ? cells[index++].Trim() : string.Empty;
                var photosCell = cells.Count > index ? cells[index++].Trim() : string.Empty;

                if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(label) && label.Length > 160)
                {
                    label = label[..160];
                }

                if (!string.IsNullOrWhiteSpace(key) && key.Length > 64)
                {
                    key = key[..64];
                }

                var normalizedType = string.IsNullOrWhiteSpace(typeCell)
                    ? MaintenanceLogColumnDefinition.DefaultColumnType
                    : typeCell.ToLowerInvariant();

                if (!MaintenanceLogColumnDefinition.AllowedTypes.Contains(normalizedType))
                {
                    throw new InvalidOperationException($"\"{typeCell}\" is not a supported column type (row {lineNumber}).");
                }

                var optionsText = string.Empty;
                if (normalizedType == "select")
                {
                    optionsText = string.Join(
                        Environment.NewLine,
                        MaintenanceLogTemplateHelper.ParseOptions(NormalizeOptionsCell(optionsCell)));
                }

                rows.Add(new MaintenanceLogColumnEditorViewModel
                {
                    Label = label,
                    Key = key,
                    Type = normalizedType,
                    Required = ParseBooleanCell(requiredCell),
                    OptionsText = optionsText,
                    IncludeNotes = ParseBooleanCell(notesCell),
                    IncludePhotos = ParseBooleanCell(photosCell)
                });
            }

            return rows;
        }

        private static bool ParseBooleanCell(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            return normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("y", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("required", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeOptionsCell(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text
                .Replace('|', '\n')
                .Replace(';', '\n');
        }

        private static List<string> SplitCsvLine(string line)
        {
            var values = new List<string>();
            if (line == null)
            {
                return values;
            }

            var builder = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    values.Add(builder.ToString());
                    builder.Clear();
                    continue;
                }

                builder.Append(ch);
            }

            values.Add(builder.ToString());
            return values;
        }

        private async Task<bool> UserCanManageAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return UserCanManage(roles);
        }

        private static bool UserCanManage(IList<string> roles)
        {
            return roles.Any(role =>
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<DayOfWeek> GetSelectedDays(MaintenanceLogTemplateEditorViewModel viewModel)
        {
            var days = new List<DayOfWeek>();
            var selections = viewModel.WeeklyDays ?? Array.Empty<bool>();
            for (var index = 0; index < selections.Length; index++)
            {
                if (selections[index])
                {
                    days.Add((DayOfWeek)index);
                }
            }

            return days;
        }

        private static bool[] BuildWeeklySelection(int bitmask)
        {
            var selection = new bool[7];
            var days = MaintenanceLogTemplateHelper.ParseWeeklyBitmask(bitmask);
            foreach (var day in days)
            {
                selection[(int)day] = true;
            }

            return selection;
        }

        private static bool RequiresDayOfMonth(MaintenanceLogScheduleType scheduleType)
        {
            return scheduleType == MaintenanceLogScheduleType.Monthly
                || scheduleType == MaintenanceLogScheduleType.Quarterly
                || scheduleType == MaintenanceLogScheduleType.Yearly;
        }

        private static List<MaintenanceLogColumnEditorViewModel> BuildColumnEditors(IReadOnlyList<MaintenanceLogColumnDefinition> columns)
        {
            if (columns.Count == 0)
            {
                return new List<MaintenanceLogColumnEditorViewModel>
                {
                    new MaintenanceLogColumnEditorViewModel()
                };
            }

            return columns
                .Select(column => new MaintenanceLogColumnEditorViewModel
                {
                    Key = column.Key,
                    Label = column.Label,
                    Type = column.Type,
                    Required = column.Required,
                    OptionsText = string.Join(Environment.NewLine, column.Options),
                    IncludeNotes = column.IncludeNotes,
                    IncludePhotos = column.IncludePhotos
                })
                .ToList();
        }

        private static List<MaintenanceLogColumnDefinition> BuildColumnDefinitions(MaintenanceLogTemplateEditorViewModel viewModel)
        {
            var results = new List<MaintenanceLogColumnDefinition>();
            if (viewModel.Columns == null)
            {
                return results;
            }

            foreach (var editor in viewModel.Columns)
            {
                if (string.IsNullOrWhiteSpace(editor.Label) && string.IsNullOrWhiteSpace(editor.Key))
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(editor.Key)
                    ? MaintenanceLogTemplateHelper.NormalizeColumnKey(editor.Label)
                    : editor.Key.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    key = MaintenanceLogTemplateHelper.NormalizeColumnKey($"column{results.Count + 1}");
                }

                results.Add(new MaintenanceLogColumnDefinition
                {
                    Key = key,
                    Label = string.IsNullOrWhiteSpace(editor.Label) ? key : editor.Label,
                    Type = string.IsNullOrWhiteSpace(editor.Type) ? MaintenanceLogColumnDefinition.DefaultColumnType : editor.Type,
                    Required = editor.Required,
                    Options = MaintenanceLogTemplateHelper.ParseOptions(editor.OptionsText),
                    IncludeNotes = editor.IncludeNotes,
                    IncludePhotos = editor.IncludePhotos
                });
            }

            return results;
        }

        private async Task<MaintenanceLogTemplateDetailViewModel> BuildDetailViewModelAsync(
            MaintenanceLogTemplate template,
            bool canManage,
            DateTime? start,
            DateTime? end)
        {
            var columns = MaintenanceLogTemplateHelper.ParseColumns(template.ColumnsJson);

            var query = _db.MaintenanceLogEntries
                .Include(e => e.CreatedByUser)
                .Where(e => e.TemplateId == template.Id);

            if (start.HasValue)
            {
                var startDate = start.Value.Date;
                query = query.Where(e => e.EntryDate >= startDate);
            }

            if (end.HasValue)
            {
                var endDate = end.Value.Date;
                query = query.Where(e => e.EntryDate <= endDate);
            }

            var entries = await query
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.Id)
                .Take(MaxEntryDisplayCount)
                .ToListAsync();

            var entryModels = entries.Select(entry =>
            {
                var (valueDict, noteDict, photoDict) = BuildEntryDataDictionaries(entry.ValuesJson, columns);
                var photoViewModels = photoDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyList<MaintenanceLogEntryPhotoViewModel>)kvp.Value
                        .Select(path => new MaintenanceLogEntryPhotoViewModel
                        {
                            FilePath = path,
                            UploadedAtUtc = entry.CreatedAtUtc
                        })
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

                return new MaintenanceLogEntryViewModel
                {
                    Id = entry.Id,
                    EntryDate = entry.EntryDate,
                    CreatedAtUtc = entry.CreatedAtUtc,
                    CreatedByName = BuildUserName(entry.CreatedByUser),
                    Values = valueDict,
                    Notes = noteDict,
                    Photos = photoViewModels
                };
            }).ToList();

            return new MaintenanceLogTemplateDetailViewModel
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                PropertyId = template.PropertyId,
                PropertyName = template.Property.Name,
                CanManage = canManage,
                ScheduleType = template.ScheduleType,
                ScheduleSummary = MaintenanceLogTemplateHelper.BuildScheduleSummary(template),
                IsActive = template.IsActive,
                Columns = columns,
                Entries = entryModels,
                FilterStart = start?.Date,
                FilterEnd = end?.Date
            };
        }

        private Dictionary<string, List<IFormFile>> CollectPhotoUploads(IFormFileCollection files, IReadOnlyList<MaintenanceLogColumnDefinition> columns)
        {
            var uploads = new Dictionary<string, List<IFormFile>>(StringComparer.OrdinalIgnoreCase);
            if (files == null || files.Count == 0)
            {
                return uploads;
            }

            var allowedKeys = columns
                .Where(column => column.IncludePhotos)
                .Select(column => column.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!allowedKeys.Any())
            {
                return uploads;
            }

            foreach (var file in files)
            {
                var columnKey = ExtractPhotoColumnKey(file.Name);
                if (columnKey == null || !allowedKeys.Contains(columnKey))
                {
                    continue;
                }

                if (file.Length <= 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension) ||
                    !AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError($"Photos[{columnKey}]", "Upload JPG, PNG, GIF, BMP, or WebP images.");
                    continue;
                }

                if (!uploads.TryGetValue(columnKey, out var list))
                {
                    list = new List<IFormFile>();
                    uploads[columnKey] = list;
                }

                list.Add(file);
            }

            return uploads;
        }

        private async Task<List<string>> SavePhotoFilesAsync(IEnumerable<IFormFile> files)
        {
            var saved = new List<string>();
            var uploadRoot = Path.Combine(_environment.WebRootPath, PhotoUploadFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in files)
            {
                if (file.Length <= 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".jpg";
                }

                var sanitizedExtension = extension.StartsWith(".") ? extension : $".{extension}";
                var uniqueName = $"{Guid.NewGuid():N}{sanitizedExtension}";
                var physicalPath = Path.Combine(uploadRoot, uniqueName);

                using (var stream = System.IO.File.Create(physicalPath))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/{PhotoUploadFolder.Replace('\\', '/')}/{uniqueName}";
                saved.Add(relativePath);
            }

            return saved;
        }

        private void DeletePhotoFilesFromJson(string? json)
        {
            var values = ParseEntryValues(json);
            foreach (var kvp in values)
            {
                if (!kvp.Key.EndsWith("__photos", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var paths = ParsePhotoList(kvp.Value);
                foreach (var path in paths)
                {
                    DeletePhotoFile(path);
                }
            }
        }

        private void DeletePhotoFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var trimmed = relativePath.TrimStart('~').TrimStart('/');
            var physicalPath = Path.Combine(_environment.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    System.IO.File.Delete(physicalPath);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static string? ExtractPhotoColumnKey(string? fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            const string prefix = "Photos[";
            if (!fieldName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            if (!fieldName.EndsWith("]", StringComparison.Ordinal))
            {
                return null;
            }

            return fieldName.Substring(prefix.Length, fieldName.Length - prefix.Length - 1);
        }

        private static (IReadOnlyDictionary<string, string?> Values,
            IReadOnlyDictionary<string, string?> Notes,
            IReadOnlyDictionary<string, IReadOnlyList<string>> Photos) BuildEntryDataDictionaries(
                string? json,
                IReadOnlyList<MaintenanceLogColumnDefinition> columns)
        {
            var raw = ParseEntryValues(json);
            var ordered = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var notes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var photos = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                raw.TryGetValue(column.Key, out var value);
                ordered[column.Key] = value;

                if (column.IncludeNotes)
                {
                    var noteKey = MaintenanceLogTemplateHelper.BuildNotesKey(column.Key);
                    if (!string.IsNullOrWhiteSpace(noteKey) && raw.TryGetValue(noteKey, out var noteValue))
                    {
                        notes[column.Key] = noteValue;
                    }
                    else
                    {
                        notes[column.Key] = null;
                    }
                }

                if (column.IncludePhotos)
                {
                    var photoKey = MaintenanceLogTemplateHelper.BuildPhotosKey(column.Key);
                    if (!string.IsNullOrWhiteSpace(photoKey) && raw.TryGetValue(photoKey, out var photoValue))
                    {
                        photos[column.Key] = ParsePhotoList(photoValue);
                    }
                    else
                    {
                        photos[column.Key] = Array.Empty<string>();
                    }
                }
            }

            return (ordered, notes, photos);
        }

        private static IReadOnlyList<string> ParsePhotoList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<string>();
            }

            try
            {
                var paths = JsonSerializer.Deserialize<List<string>>(json, EntrySerializerOptions);
                var cleaned = paths?
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path.Replace('\\', '/'))
                    .ToList();
                return cleaned ?? new List<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static Dictionary<string, string?> ParseEntryValues(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, EntrySerializerOptions);
                return values != null
                    ? new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string? NormalizeValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length > 500 ? trimmed[..500] : trimmed;
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n');
            var sanitized = value.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{sanitized}\"" : sanitized;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sanitized = new string(value
                .Select(ch => invalid.Contains(ch) ? '-' : ch)
                .ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "logs" : sanitized;
        }

        private static string? BuildUserName(ApplicationUser? user)
        {
            if (user == null)
            {
                return null;
            }

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            return user.UserName;
        }
    }
}

