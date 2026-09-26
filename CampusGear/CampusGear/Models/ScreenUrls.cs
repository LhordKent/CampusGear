using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace CampusGear;

public static class ScreenUrls
{
    public static string Query(string path, object values) => QueryHelpers.AddQueryString(path,
        values.GetType().GetProperties().ToDictionary(property => property.Name,
            property => Convert.ToString(property.GetValue(values), CultureInfo.InvariantCulture)));
}
