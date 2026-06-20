using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using VirtualBar.Application.Common;
using VirtualBar.Application.DTOs.Products;
using VirtualBar.Application.Interfaces;
using VirtualBar.Infrastructure.Options;

namespace VirtualBar.Infrastructure.Services;

public sealed class ProductLookupService(
    HttpClient http,
    IWebHostEnvironment env,
    IOptions<ProductLookupOptions> options) : IProductLookupService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<BarcodeProductDto>> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var url = $"{options.Value.LookupUrl}?upc={Uri.EscapeDataString(barcode)}";
        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return Result<BarcodeProductDto>.NotFound("Product not found.");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<UpcResponse>(json, JsonOpts);

        if (data?.Items is null || data.Items.Length == 0)
            return Result<BarcodeProductDto>.NotFound("Product not found.");

        var item = data.Items[0];
        string? localImageUrl = null;

        if (item.Images?.Length > 0)
        {
            try
            {
                var remoteUrl = item.Images[0];
                var imageBytes = await http.GetByteArrayAsync(remoteUrl, cancellationToken);

                var uploadsDir = Path.Combine(
                    env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"),
                    "uploads", "bottles");
                Directory.CreateDirectory(uploadsDir);

                var ext = Path.GetExtension(new Uri(remoteUrl).LocalPath).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 5) ext = ".jpg";

                var filename = $"{Guid.NewGuid()}{ext}";
                await File.WriteAllBytesAsync(Path.Combine(uploadsDir, filename), imageBytes, cancellationToken);
                localImageUrl = $"/uploads/bottles/{filename}";
            }
            catch
            {
                // Image download failed — proceed without image
            }
        }

        return Result<BarcodeProductDto>.Ok(new BarcodeProductDto
        {
            Name = item.Title ?? string.Empty,
            Brand = item.Brand,
            ImageUrl = localImageUrl,
            VolumeMl = ParseVolumeMl(item.Size),
            AbvPercent = ParseAbv(item.Title, item.Description),
        });
    }

    private static int? ParseVolumeMl(string? size)
    {
        if (string.IsNullOrWhiteSpace(size)) return null;

        var m = Regex.Match(size.Trim(), @"(\d+(?:[.,]\d+)?)\s*(ml|cl|l)\b", RegexOptions.IgnoreCase);

        if (!m.Success) 
            return null;

        var raw = m.Groups[1].Value.Replace(',', '.');

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        return m.Groups[2].Value.ToLowerInvariant() switch
        {
            "ml" => (int)Math.Round(value),
            "cl" => (int)Math.Round(value * 10),
            "l"  => (int)Math.Round(value * 1000),
            _    => null,
        };
    }

    private static double? ParseAbv(string? title, string? description)
    {
        var text = $"{title} {description}";
        // Matches: "40%", "40% ABV", "40.5% vol", "40% alc/vol"
        var m = Regex.Match(text, @"(\d+(?:[.,]\d+)?)\s*%\s*(?:abv|vol|alc)?", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var raw = m.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        // Sanity check: spirits are 5–96% ABV
        if (value < 5 || value > 96) return null;
        return Math.Round(value, 1);
    }

    private sealed record UpcResponse(string Code, UpcItem[] Items);

    private sealed record UpcItem(
        string? Title,
        string? Brand,
        string? Description,
        string? Size,
        string[]? Images);
}
