using AbfuhrPilot.Domain.Common;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>
/// A photo attached to a driver problem report. Uploaded first (staged, with a
/// null <see cref="ProblemReportId"/>) and linked when the report is submitted.
/// Only an opaque storage key is kept — never a client-visible filesystem path.
/// </summary>
public class ProblemPhoto : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public Guid? ProblemReportId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
