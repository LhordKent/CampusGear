using System.Text.RegularExpressions;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Picks the placeholder icon (a symbol in Views/Borrower/_EquipmentIcons.cshtml) for equipment without a photo.
    /// The name is matched first, so "Apple iPad" in "Laptops &amp; Tablets" is a tablet, not a laptop; the category
    /// is only a fallback. Rules are ordered: earlier, more specific rules win ("Tripod Screen" is a screen).
    /// </summary>
    public static class EquipmentIcon
    {
        private static readonly (string Icon, Regex Pattern)[] NameRules =
        [
            ("screen", Rule(@"\bscreens?\b|whiteboard")),
            ("projector", Rule(@"projector")),
            ("display", Rule(@"\bmonitor|\bdisplay|\btv\b|television")),
            ("tablet", Rule(@"\bipad|tablet|galaxy tab|\be-?reader")),
            ("laptop", Rule(@"laptop|notebook|macbook|chromebook|thinkpad|computer|\bpc\b|desktop")),
            ("tripod", Rule(@"tripod|gimbal|monopod|\bstand\b")),
            ("light", Rule(@"\blights?\b|\bled\b|lighting|lamp|softbox|ring light")),
            ("camera", Rule(@"camera|camcorder|dslr|mirrorless|\blens|gopro|webcam")),
            ("speaker", Rule(@"speaker|\bpa\b|amplifier|\bamp\b|soundbar")),
            ("headphones", Rule(@"headphone|headset|earphone|earbud")),
            ("recorder", Rule(@"recorder|mixer|audio interface")),
            ("audio", Rule(@"\bmic\b|\bmics\b|microphone|lavalier|lapel")),
            ("oscilloscope", Rule(@"oscilloscope|signal generator|function generator")),
            ("meter", Rule(@"multimeter|\bmeter\b|tester|clamp meter")),
            ("chip", Rule(@"arduino|raspberry|microcontroller|sensor|breadboard|robot|circuit")),
            ("router", Rule(@"router|wi-?fi|access point|\bswitch\b|modem|hotspot")),
            ("clicker", Rule(@"clicker|presenter|\bremote\b|laser pointer")),
            ("adapter", Rule(@"adapter|adaptor|\bhdmi\b|\busb\b|cable|dongle|\bhub\b|converter")),
            ("power", Rule(@"extension|power|charger|battery|\bcord\b|\breel\b|\bups\b|generator")),
        ];

        private static readonly (string Icon, Regex Pattern)[] CategoryRules =
        [
            ("projector", Rule(@"projector")),
            ("display", Rule(@"display")),
            ("laptop", Rule(@"laptop|computer")),
            ("tablet", Rule(@"tablet")),
            ("camera", Rule(@"camera|photo|video")),
            ("audio", Rule(@"audio|sound|music")),
            ("meter", Rule(@"\blab\b|measurement|science|engineering")),
            ("router", Rule(@"network")),
            ("adapter", Rule(@"accessor|cable")),
        ];

        public static string For(string name, string category)
        {
            return Match(NameRules, name) ?? Match(CategoryRules, category) ?? Match(NameRules, category) ?? "box";
        }

        private static string? Match((string Icon, Regex Pattern)[] rules, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            foreach (var (icon, pattern) in rules)
            {
                if (pattern.IsMatch(text))
                {
                    return icon;
                }
            }

            return null;
        }

        private static Regex Rule(string pattern) =>
            new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }
}
