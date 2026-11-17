namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// How long after the due time a loan can come back before it counts as late. Inside the grace period an
    /// unreturned loan is still "due" (not overdue) and a return is on time; after it, the loan is overdue and a return
    /// is late. Before the due time a return is still early. Every overdue/late check goes through here.
    /// </summary>
    public static class ReturnGrace
    {
        /// <summary>Kept as whole minutes so database queries can use DueAtUtc.AddMinutes(Minutes).</summary>
        public const int Minutes = 15;

        public static readonly TimeSpan Period = TimeSpan.FromMinutes(Minutes);

        /// <summary>The moment a loan due at <paramref name="dueAtUtc"/> becomes late.</summary>
        public static DateTime LateAfter(DateTime dueAtUtc) => dueAtUtc + Period;

        /// <summary>True when <paramref name="atUtc"/> (now, or the return time) is past the grace period.</summary>
        public static bool IsLate(DateTime dueAtUtc, DateTime atUtc) => atUtc > LateAfter(dueAtUtc);

        /// <summary>For queries: an unreturned loan due before this instant is overdue at <paramref name="nowUtc"/>.</summary>
        public static DateTime OverdueIfDueBefore(DateTime nowUtc) => nowUtc - Period;
    }
}
