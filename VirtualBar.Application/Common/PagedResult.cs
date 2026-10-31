namespace VirtualBar.Application.Common;

/// <summary>
/// One page of a list. <see cref="HasMore"/> comes from fetching one row more than the page size, so
/// there is never a separate count query.
/// </summary>
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    public bool HasMore { get; set; }
}
