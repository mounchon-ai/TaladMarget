using Microsoft.Extensions.Options;

namespace Talad.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    /// <summary>DEC-002 — product pictures are files on the server's disk; back this folder up with the database.</summary>
    public string ProductImageRoot { get; set; } = "data/product-images";
}

/// <summary>API-043 · turns a product's stored image location into a file inside the image folder, or nothing.</summary>
public sealed class ProductImageFiles(IOptions<StorageOptions> options)
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    public (string FullPath, string ContentType)? Resolve(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        var root = Path.GetFullPath(options.Value.ProductImageRoot);
        var full = Path.GetFullPath(Path.Combine(root, imagePath));
        // a stored path may never climb out of the image folder
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        if (!File.Exists(full)) return null;
        return ContentTypes.TryGetValue(Path.GetExtension(full), out var type) ? (full, type) : null;
    }
}
