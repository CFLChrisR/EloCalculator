using System.Globalization;
using System.Text;

namespace ThreePLAudit.Core.Auditing;

public static class ReportWriter
{
    private static readonly string[] Columns =
    [
        "FindingId", "RuleId", "Category", "Severity", "Disposition", "Counterparty", "EntityRef", "Title",
        "BilledAmount", "ExpectedAmount", "RecoverableAmount", "Confidence", "Deadline", "Regulations", "Detail",
    ];

    public static string ToCsv(IEnumerable<Finding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Columns));
        foreach (var f in findings)
            sb.AppendLine(string.Join(",", new[]
            {
                f.Id, f.RuleId, f.Category.ToString(), f.Severity.ToString(), f.Disposition.ToString(), f.Counterparty ?? "",
                f.EntityRef, f.Title, Num(f.BilledAmount), Num(f.ExpectedAmount), Num(f.RecoverableAmount),
                f.Confidence.ToString("0.00", CultureInfo.InvariantCulture), f.Deadline?.ToString("yyyy-MM-dd") ?? "",
                string.Join(" ", f.RegulationIds), f.Detail,
            }.Select(Escape)));
        return sb.ToString();
    }

    private static string Num(decimal? d) => d?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";

    private static string Escape(string s) =>
        s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
