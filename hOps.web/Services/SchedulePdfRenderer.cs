using System;
using System.Collections.Generic;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace hOps.web.Services
{
    public class SchedulePdfRenderer
    {
        public byte[] Render(string propertyName, string title, IReadOnlyList<DateTime> dayColumns, IReadOnlyList<SchedulePdfRow> rows)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var headerFont = new XFont("Helvetica", 16, XFontStyle.Bold);
            var subHeaderFont = new XFont("Helvetica", 11, XFontStyle.Regular);
            var cellFont = new XFont("Helvetica", 9, XFontStyle.Regular);
            var headerCellFont = new XFont("Helvetica", 10, XFontStyle.Bold);
            var pen = new XPen(XColors.LightGray, 0.5);
            double lineHeight = gfx.MeasureString("Ag", cellFont).Height + 2;

            const double margin = 36;
            double availableWidth = page.Width - (margin * 2);
            double nameColumnWidth = 130;
            double dayColumnWidth = (availableWidth - nameColumnWidth) / Math.Max(1, dayColumns.Count);
            double y = margin;

            void DrawHeader()
            {
                gfx.DrawString(propertyName, headerFont, XBrushes.Black, new XRect(margin, y, availableWidth, 24), XStringFormats.TopLeft);
                y += 22;
                gfx.DrawString(title, subHeaderFont, XBrushes.Black, new XRect(margin, y, availableWidth, 18), XStringFormats.TopLeft);
                y += 28;

                var headerHeight = 24;
                gfx.DrawRectangle(pen, margin, y, availableWidth, headerHeight);
                gfx.DrawLine(pen, margin + nameColumnWidth, y, margin + nameColumnWidth, y + headerHeight);
                gfx.DrawString("Employee", headerCellFont, XBrushes.Black, new XRect(margin + 4, y + 4, nameColumnWidth - 8, headerHeight - 8), XStringFormats.TopLeft);

                for (int i = 0; i < dayColumns.Count; i++)
                {
                    var colX = margin + nameColumnWidth + (i * dayColumnWidth);
                    gfx.DrawLine(pen, colX + dayColumnWidth, y, colX + dayColumnWidth, y + headerHeight);
                    var label = dayColumns[i].ToString("ddd\nMMM d");
                    gfx.DrawString(label, headerCellFont, XBrushes.Black, new XRect(colX, y, dayColumnWidth, headerHeight), XStringFormats.Center);
                }

                y += headerHeight;
            }

            DrawHeader();

            foreach (var row in rows)
            {
                double rowHeight = CalculateRowHeight(row, lineHeight);
                if (y + rowHeight > page.Height - margin)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    lineHeight = gfx.MeasureString("Ag", cellFont).Height + 2;
                    y = margin;
                    DrawHeader();
                }

                gfx.DrawRectangle(pen, margin, y, availableWidth, rowHeight);
                gfx.DrawLine(pen, margin + nameColumnWidth, y, margin + nameColumnWidth, y + rowHeight);
                gfx.DrawString(row.EmployeeName, cellFont, XBrushes.Black, new XRect(margin + 4, y + 4, nameColumnWidth - 8, rowHeight - 8), XStringFormats.TopLeft);

                for (int i = 0; i < dayColumns.Count; i++)
                {
                    var colX = margin + nameColumnWidth + (i * dayColumnWidth);
                    gfx.DrawLine(pen, colX + dayColumnWidth, y, colX + dayColumnWidth, y + rowHeight);
                    var lines = row.CellLines.Count > i ? row.CellLines[i] : new List<string>();
                    DrawCellLines(gfx, cellFont, lines, colX, y, dayColumnWidth, rowHeight, lineHeight);
                }

                y += rowHeight;
            }

            using var ms = new System.IO.MemoryStream();
            document.Save(ms);
            return ms.ToArray();
        }

        private static double CalculateRowHeight(SchedulePdfRow row, double lineHeight)
        {
            double maxLines = 1;

            foreach (var cell in row.CellLines)
            {
                maxLines = Math.Max(maxLines, Math.Max(1, cell.Count));
            }

            return Math.Max(32, (maxLines * lineHeight) + 10);
        }

        private static void DrawCellLines(XGraphics gfx, XFont font, IReadOnlyList<string> lines, double x, double y, double width, double height, double lineHeight)
        {
            if (lines.Count == 0)
            {
                return;
            }

            double totalLinesHeight = lines.Count * lineHeight;
            double startY = y + Math.Max(4, (height - totalLinesHeight) / 2);

            foreach (var line in lines)
            {
                gfx.DrawString(line, font, XBrushes.Black, new XRect(x + 2, startY, width - 4, lineHeight), XStringFormats.TopLeft);
                startY += lineHeight;
            }
        }
    }

    public class SchedulePdfRow
    {
        public string EmployeeName { get; set; } = string.Empty;
        public List<List<string>> CellLines { get; set; } = new();
    }
}
