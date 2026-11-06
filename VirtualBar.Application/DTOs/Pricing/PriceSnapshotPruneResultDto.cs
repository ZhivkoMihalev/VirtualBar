namespace VirtualBar.Application.DTOs.Pricing;

/// <summary>
/// What <c>POST /api/admin/prices/prune</c> found, and on a real run deleted: price snapshots no path reads any more.
/// </summary>
public sealed class PriceSnapshotPruneResultDto
{
    /// <summary>True when nothing was deleted: the report of what a real run would delete.</summary>
    public bool DryRun { get; set; }

    /// <summary>Every snapshot in the table before the run.</summary>
    public int TotalCount { get; set; }

    /// <summary>The stale snapshots whose key no non-deleted bottle or product uses.</summary>
    public int OrphanCount { get; set; }

    /// <summary>How many were deleted; always 0 on a dry run.</summary>
    public int DeletedCount { get; set; }

    /// <summary>Up to 20 orphan keys, in key order, to check by eye before approving a real run.</summary>
    public List<string> SampleKeys { get; set; } = [];
}
