namespace CampusGear.Models;

// Data for the server-rendered dashboard charts in Views/Shared/Charts. Every value comes from a database query;
// the partials only turn these numbers into geometry. Slot is the fixed categorical color (1-4) from workspace.css.

/// <summary>One labelled value: a donut segment or a horizontal bar.</summary>
public sealed record ChartSlice(string Label, int Value, int Slot = 1, string? Url = null);

/// <summary>A part-to-whole ring, such as equipment by availability.</summary>
public sealed record DonutChart(string Title, string? Subtitle, string CenterLabel, IReadOnlyList<ChartSlice> Slices)
{
    public int Total => Slices.Sum(x => x.Value);
}

/// <summary>Single-series horizontal bars, such as reservations by status.</summary>
public sealed record BarChart(string Title, string? Subtitle, IReadOnlyList<ChartSlice> Bars);

/// <summary>One named series over the shared x labels of a <see cref="SeriesChart"/>.</summary>
public sealed record ChartSeries(string Name, int Slot, IReadOnlyList<int> Values);

/// <summary>Values over time: <c>line</c> for trends of one or two series, <c>column</c> for one series of counts.</summary>
public sealed record SeriesChart(string Title, string? Subtitle, string Kind,
    IReadOnlyList<string> Labels, IReadOnlyList<ChartSeries> Series)
{
    public int Max => Series.SelectMany(x => x.Values).DefaultIfEmpty(0).Max();

    /// <summary>A clean axis ceiling (1, 2, 5 × 10ⁿ) with four gridline steps.</summary>
    public int AxisMax
    {
        get
        {
            var max = Math.Max(Max, 4);
            var step = (int)Math.Ceiling(max / 4d);
            var magnitude = (int)Math.Pow(10, Math.Floor(Math.Log10(step)));
            var nice = new[] { 1, 2, 5, 10 }.Select(x => x * magnitude).First(x => x >= step);
            return nice * 4;
        }
    }
}

/// <summary>What waits at the custodian desk; the dashboard header and its buttons show these.</summary>
public sealed record DeskCounts(int Pending, int ReadyToRelease, int Overdue, int DueToday)
{
    public int Waiting => Pending + ReadyToRelease + Overdue + DueToday;
}

/// <summary>One row in a dashboard activity feed.</summary>
public sealed record ActivityItem(string Title, string Detail, string Status, string When, string Url);
