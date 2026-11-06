namespace VirtualBar.Application.DTOs.Products;

/// <summary>Another vintage or edition of a product's line, as the product page lists it (#59).</summary>
public sealed class ProductYearDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Its vintage year or release year, the same kind of year as the product whose page lists it.</summary>
    public int Year { get; set; }

    /// <summary>Average over all reviews of all its linked bottles, 1 decimal; null when there are none.</summary>
    public double? AverageScore { get; set; }

    /// <summary>Its current listings, the same count as <see cref="ProductCardDto.ListingsCount"/>.</summary>
    public int ListingsCount { get; set; }
}
