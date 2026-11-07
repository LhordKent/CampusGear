namespace CampusGear.Controllers;
public static class EquipmentIcon
{
    public static string For(string name, string category)
    {
        var text = $"{name} {category}".ToLowerInvariant();
        if (new[] { "laptop", "computer", "notebook", "macbook", "chromebook" }.Any(text.Contains)) return "laptop";
        if (new[] { "camera", "camcorder", "dslr", "lens", "tripod" }.Any(text.Contains)) return "camera";
        if (text.Contains("projector")) return "projector";
        if (new[] { "mic", "audio", "speaker", "sound", "headphone" }.Any(text.Contains)) return "audio";
        return "box";
    }
}
