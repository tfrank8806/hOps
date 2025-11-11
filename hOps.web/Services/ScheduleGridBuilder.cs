using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hOps.web.Models;

namespace hOps.web.Services
{
    public static class ScheduleGridBuilder
    {
        public static List<ScheduleGridRow> BuildRows(
            IReadOnlyList<DateTime> dayColumns,
            IEnumerable<ScheduleAssignment> assignments,
            IEnumerable<ScheduleTimeOffRequest> timeOffRequests)
        {
            var assignmentLookup = assignments
                .GroupBy(a => a.ScheduleEmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var timeOffLookup = new HashSet<(int EmployeeId, DateTime Date)>();
            foreach (var request in timeOffRequests)
            {
                var start = request.StartDate.Date;
                var end = request.EndDate.Date;
                for (var current = start; current <= end; current = current.AddDays(1))
                {
                    timeOffLookup.Add((request.ScheduleEmployeeId, current));
                }
            }

            var rows = new List<ScheduleGridRow>();
            foreach (var entry in assignmentLookup.OrderBy(e => e.Value.First().Employee.DisplayName))
            {
                var employee = entry.Value.First().Employee;
                var row = new ScheduleGridRow
                {
                    EmployeeName = employee.DisplayName,
                    ScheduleEmployeeId = employee.Id
                };

                foreach (var day in dayColumns)
                {
                    var lines = entry.Value
                        .Where(a => a.ShiftDate.Date == day.Date)
                        .OrderBy(a => a.ShiftStartTime ?? TimeSpan.Zero)
                        .Select(BuildAssignmentLine)
                        .ToList();

                    if (!lines.Any() && timeOffLookup.Contains((employee.Id, day.Date)))
                    {
                        lines.Add("(Approved time off)");
                    }

                    row.CellLines.Add(lines);
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string BuildAssignmentLine(ScheduleAssignment assignment)
        {
            var builder = new StringBuilder();
            builder.Append(assignment.ShiftName);

            var range = FormatTimeRange(assignment.ShiftStartTime, assignment.ShiftEndTime);
            if (!string.IsNullOrWhiteSpace(range))
            {
                builder.Append(" · ").Append(range);
            }

            if (!string.IsNullOrWhiteSpace(assignment.Notes))
            {
                builder.Append(" – ").Append(assignment.Notes.Trim());
            }

            return builder.ToString();
        }

        private static string? FormatTimeRange(TimeSpan? start, TimeSpan? end)
        {
            if (!start.HasValue && !end.HasValue)
            {
                return null;
            }

            string Format(TimeSpan? value)
            {
                if (!value.HasValue)
                {
                    return string.Empty;
                }

                var reference = DateTime.Today.Add(value.Value);
                return reference.ToString("h:mm tt");
            }

            if (start.HasValue && end.HasValue)
            {
                return $"{Format(start)} - {Format(end)}";
            }

            return start.HasValue ? $"Starts {Format(start)}" : $"Ends {Format(end)}";
        }
    }

    public class ScheduleGridRow
    {
        public int ScheduleEmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public List<List<string>> CellLines { get; set; } = new();
    }
}
