namespace PropertyManagement.Web.Helpers;

/// <summary>Image URLs for units/properties when entities have no PhotoUrl.</summary>
public static class PropertyImageUrls
{
    private static readonly string[] UnitFallback =
    [
        "maple-grove", "riverside", "pineview", "lakeside", "willow-creek", "cedar-ridge",
    ];

    private static readonly Dictionary<string, string> KnownSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Maple Grove"] = "maple-grove",
        ["Riverside Apartments"] = "riverside",
        ["Pineview Commons"] = "pineview",
        ["Lakeside Flats"] = "lakeside",
        ["Willow Creek"] = "willow-creek",
        ["Cedar Ridge"] = "cedar-ridge",
    };

    /// <summary>
    /// Curated Unsplash exteriors — modern city multi-flat apartment buildings
    /// (no interiors, no single-family suburban houses). Indexed by unique key.
    /// </summary>
    private static readonly string[] ModernCityMultiFlatExteriors =
    [
        "https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1460317442991-0ec209397118?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1554995207-c18c203602cb?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1574362848149-11496d93a7c7?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1515263487990-61b07816b324?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1580041065738-e72023775cdc?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1597047084897-51e81819a499?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1464938050520-ef2270bb8ce8?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1755896487242-23cb0847e493?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1768760906477-70190a413c6d?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1758448511487-15f69dd6107b?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1764793184249-f7eed6c21e24?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1560448075-cbc16bb4af8e?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1448630360428-65456885c650?auto=format&fit=crop&w=800&h=480&q=80",
        "https://images.unsplash.com/photo-1449844908441-8829872d2607?auto=format&fit=crop&w=800&h=480&q=80",
    ];

    public static string UnitImage(string propertyName, string? photoUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(photoUrl))
        {
            return photoUrl;
        }

        return $"/images/units/{Slug(propertyName)}.jpg";
    }

    /// <param name="uniqueKey">Property id (or 0-based list index). Must differ per property to avoid repeats.</param>
    public static string PropertyExteriorImage(int uniqueKey, string? photoUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(photoUrl))
        {
            return photoUrl;
        }

        var len = ModernCityMultiFlatExteriors.Length;
        var idx = uniqueKey % len;
        if (idx < 0)
        {
            idx += len;
        }

        return ModernCityMultiFlatExteriors[idx];
    }

    private static string Slug(string propertyName)
    {
        if (KnownSlugs.TryGetValue(propertyName.Trim(), out var slug))
        {
            return slug;
        }

        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in propertyName.Trim().ToUpperInvariant())
            {
                hash ^= c;
                hash *= 16777619;
            }

            return UnitFallback[(int)(hash % (uint)UnitFallback.Length)];
        }
    }
}
