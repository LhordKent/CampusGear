namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// When an approved booking can be handed over. Pickup opens a short while before the booking starts, so a borrower
    /// who arrives early is not sent away, and closes when the booking ends. The loan is still due at the booking's end.
    /// The reservation service enforces this and the custodian screens use it to enable the release controls.
    /// </summary>
    public static class ReleaseWindow
    {
        /// <summary>How early pickup opens; it matches the booking picker's 30-minute slots.</summary>
        public static readonly TimeSpan EarlyPickup = TimeSpan.FromMinutes(30);

        public static DateTime OpensAt(DateTime startAtUtc) => startAtUtc - EarlyPickup;

        public static bool IsOpen(DateTime startAtUtc, DateTime endAtUtc, DateTime nowUtc) =>
            nowUtc >= OpensAt(startAtUtc) && nowUtc < endAtUtc;
    }
}
