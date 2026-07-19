using VirtualBar.Domain.Enums;

namespace VirtualBar.Domain.Common;

public sealed record BadgeDefinition(BadgeType Type, BadgeTrigger Trigger, BadgeCountKind CountKind, int Threshold);
