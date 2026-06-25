using VirtualBar.Domain.Enums;

namespace VirtualBar.Application.DTOs.Distillery;

public sealed class DistilleryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string? Country { get; set; }

    public string? Region { get; set; }

    public List<SpiritCategory> Categories { get; set; } = [];
}
