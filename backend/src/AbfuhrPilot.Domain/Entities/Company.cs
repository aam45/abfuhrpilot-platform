using AbfuhrPilot.Domain.Common;

namespace AbfuhrPilot.Domain.Entities;

/// <summary>The waste-collection company / workspace all other data belongs to.</summary>
public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
}
