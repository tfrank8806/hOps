using hOps.web.Models;
using hOps.web.Utilities;

var columns = new List<MaintenanceLogColumnDefinition>
{
    new() { Key = "Replace disposable Air Filters (4) Write Date on Filters RTU1", Label = "RTU 1 - Replace Disposable Air Filters (4) Write Date on Filters", Type = "checkbox", Required = true },
    new() { Key = "Clean washable filters RTU1", Label = "RTU 1 - Clean washable filters", Type = "checkbox", Required = true },
    new() { Key = "Fully operational? note any issues RTU1", Label = "RTU 1 - Fully operational? Note any issues.", Type = "text" },
    new() { Key = "Replace disposable Air Filters (4) Write Date on Filters RTU2", Label = "RTU 2 - Replace Disposable Air Filters (4) Write Date on Filters", Type = "checkbox", Required = true },
    new() { Key = "Clean washable filters RTU2", Label = "RTU 2 - Clean washable filters", Type = "checkbox", Required = true },
    new() { Key = "Fully operational? note any issues RTU2", Label = "RTU 2 - Fully operational? Note any issues.", Type = "text" }
};

var json = MaintenanceLogTemplateHelper.BuildColumnsJson(columns);
Console.WriteLine(json);

var parsed = MaintenanceLogTemplateHelper.ParseColumns(json);
Console.WriteLine($"Parsed count: {parsed.Count}");
foreach (var col in parsed)
{
    Console.WriteLine($"{col.Key} - {col.Label}");
}
