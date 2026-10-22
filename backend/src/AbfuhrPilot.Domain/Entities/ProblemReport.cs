using AbfuhrPilot.Domain.Common;
using AbfuhrPilot.Domain.Enums;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>A problem raised by a driver at a stop ("Problem melden").</summary>
public class ProblemReport : BaseEntity, ICompanyOwned
{
    public Guid CompanyId { get; set; }
    public Guid StopId { get; set; }
    public Guid DriverId { get; set; }
    public ProblemType ProblemType { get; set; }
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
