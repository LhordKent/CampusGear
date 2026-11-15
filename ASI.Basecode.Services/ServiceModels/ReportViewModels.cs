namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>One row in a dashboard activity feed.</summary>
    public sealed record ActivityItem(string Title, string Detail, string Status, string When, string Url);

    public sealed record AdminDashboardViewModel(
        string FirstName,
        DonutChart Availability,
        BarChart ReservationStatus,
        SeriesChart BorrowingTrend,
        SeriesChart Registrations,
        IReadOnlyList<ActivityItem> RecentActivity,
        IReadOnlyList<ActivityItem> PendingRegistrations,
        int PendingRegistrationTotal
    );

    /// <summary>Single-series horizontal bars, such as reservations by status.</summary>
    public sealed record BarChart(string Title, string? Subtitle, IReadOnlyList<ChartSlice> Bars);

    /// <summary>One named series over the shared x labels of a <see cref = "SeriesChart"/>.</summary>
    public sealed record ChartSeries(string Name, int Slot, IReadOnlyList<int> Values);

    // Data for the server-rendered dashboard charts in Views/Shared/Charts. Every value comes from a database query;
    // the partials only turn these numbers into geometry. Slot is the fixed categorical color (1-4) from main.css.
    /// <summary>One labelled value: a donut segment or a horizontal bar.</summary>
    public sealed record ChartSlice(string Label, int Value, int Slot = 1, string? Url = null);

    /// <summary>A part-to-whole ring, such as equipment by availability.</summary>
    public sealed record DonutChart(string Title, string? Subtitle, string CenterLabel, IReadOnlyList<ChartSlice> Slices)
    {
        public int Total => Slices.Sum(x => x.Value);
    }

    /// <summary>Values over time: <c>line</c> for trends of one or two series, <c>column</c> for one series of counts.</summary>
    public sealed record SeriesChart(
        string Title,
        string? Subtitle,
        string Kind,
        IReadOnlyList<string> Labels,
        IReadOnlyList<ChartSeries> Series
    )
    {
        public int Max => Series.SelectMany(x => x.Values).DefaultIfEmpty(0).Max();
    }
}
