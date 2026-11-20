// ASSET CODE SERIES (table: AssetCodeSeries)  -  one per category: its prefix (CAM) and the next number to give out.
namespace ASI.Basecode.Data.Models
{
    public sealed class AssetCodeSeries
    {
        public string Prefix { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }
        public long NextNumber { get; set; } = 1;
    }
}
