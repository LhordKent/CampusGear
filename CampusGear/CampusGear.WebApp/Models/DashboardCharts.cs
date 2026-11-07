namespace CampusGear.Models;
public static class ChartGeometry
{
    public static int AxisMax(SeriesChart chart)
    {
        var max = Math.Max(chart.Max, 4);
        var step = (int)Math.Ceiling(max / 4d);
        var magnitude = (int)Math.Pow(10, Math.Floor(Math.Log10(step)));
        return new[] { 1, 2, 5, 10 }.Select(x => x * magnitude).First(x => x >= step) * 4;
    }
}
