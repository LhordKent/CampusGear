using System.Text.Json;
using CampusGear.Models;
using Microsoft.EntityFrameworkCore;

namespace CampusGear.Data;

/// <summary>
/// Sample records for Demo Mode: a fuller catalog plus a few months of reservations, loans, returns, maintenance, and
/// audit history, so every dashboard, chart, queue, and calendar has something to show.
/// <para>Isolation rules:</para>
/// <list type="bullet">
/// <item>Runs only when the caller says Demo Mode is on, which Program.cs allows in the Development environment alone.</item>
/// <item>Creates no accounts and changes none. Activity is attached only to accounts that already exist on the
/// reserved demo domain (<see cref="AccountDomain"/>), which cannot belong to real people; with none, only the
/// catalog is added.</item>
/// <item>Every demo item's asset code starts with <see cref="AssetPrefix"/>, and seeding is skipped once any exists,
/// so restarting never duplicates anything. <see cref="RemoveAsync"/> takes it all back out.</item>
/// </list>
/// </summary>
public static class DemoData
{
    public const string AssetPrefix = "DM-";
    public const string AccountDomain = "@campusgear.local";
    private const string SerialPrefix = "DEMO-DM-";

    private sealed record ItemSpec(string Code, string Name, string Category, string Location, EquipmentCondition Condition,
        string Description, int AddedDaysAgo, bool Active = true);

    private static readonly (string Name, string Description)[] Categories =
    [
        ("Cameras & Photography", "Cameras, lenses, tripods, and lighting for coursework and events"),
        ("Laptops & Tablets", "Portable computers for labs, fieldwork, and presentations"),
        ("Projectors & Displays", "Projectors, portable screens, and presentation displays"),
        ("Audio Equipment", "Microphones, recorders, speakers, and mixers"),
        ("Lab & Measurement", "Multimeters, oscilloscopes, and sensor kits for engineering labs"),
        ("Networking & Accessories", "Adapters, extension reels, routers, and presentation clickers"),
    ];

    private static readonly ItemSpec[] Items =
    [
        new("DM-CAM-01", "Canon EOS R50 Mirrorless Camera", "Cameras & Photography", "Media Room 101, Cabinet A", EquipmentCondition.Excellent, "24 MP mirrorless body with 18-45 mm kit lens, two batteries, and a 64 GB card.", 80),
        new("DM-CAM-02", "Canon EOS R50 Mirrorless Camera", "Cameras & Photography", "Media Room 101, Cabinet A", EquipmentCondition.Good, "24 MP mirrorless body with 18-45 mm kit lens, two batteries, and a 64 GB card.", 80),
        new("DM-CAM-03", "Sony ZV-E10 Vlog Camera", "Cameras & Photography", "Media Room 101, Cabinet A", EquipmentCondition.Good, "Interchangeable-lens vlogging camera with shotgun mic windscreen.", 64),
        new("DM-CAM-04", "Manfrotto Aluminum Tripod", "Cameras & Photography", "Media Room 101, Rack 2", EquipmentCondition.Fair, "Three-section tripod with fluid video head; one leg lock is stiff.", 120),
        new("DM-CAM-05", "Godox LED Light Panel Kit", "Cameras & Photography", "Media Room 101, Rack 2", EquipmentCondition.Good, "Two bi-colour LED panels with stands and diffusers.", 9),
        new("DM-LAP-01", "Lenovo ThinkPad E14 Laptop", "Laptops & Tablets", "IT Help Desk, Locker 3", EquipmentCondition.Good, "Intel Core i5, 16 GB RAM, Windows 11 Education, charger included.", 150),
        new("DM-LAP-02", "Lenovo ThinkPad E14 Laptop", "Laptops & Tablets", "IT Help Desk, Locker 3", EquipmentCondition.Good, "Intel Core i5, 16 GB RAM, Windows 11 Education, charger included.", 150),
        new("DM-LAP-03", "Lenovo ThinkPad E14 Laptop", "Laptops & Tablets", "IT Help Desk, Locker 4", EquipmentCondition.Damaged, "Intel Core i5, 16 GB RAM. Cracked hinge reported on return.", 150),
        new("DM-LAP-04", "Apple MacBook Air M2", "Laptops & Tablets", "IT Help Desk, Locker 1", EquipmentCondition.Excellent, "13-inch, 8 GB RAM, with Final Cut Pro and Logic Pro installed.", 40),
        new("DM-TAB-01", "Apple iPad 10th Gen with Pencil", "Laptops & Tablets", "IT Help Desk, Locker 1", EquipmentCondition.Good, "64 GB Wi-Fi iPad with Apple Pencil and folio case.", 22),
        new("DM-PRJ-01", "Epson EB-X51 Projector", "Projectors & Displays", "Equipment Room B, Shelf 1", EquipmentCondition.Good, "3,800-lumen XGA projector with HDMI and VGA cables.", 200),
        new("DM-PRJ-02", "Epson EB-X51 Projector", "Projectors & Displays", "Equipment Room B, Shelf 1", EquipmentCondition.Fair, "3,800-lumen XGA projector; lamp at 70% life.", 200),
        new("DM-PRJ-03", "ViewSonic Portable Mini Projector", "Projectors & Displays", "Equipment Room B, Shelf 2", EquipmentCondition.Unserviceable, "Pocket LED projector. Will not power on; awaiting replacement board.", 95),
        new("DM-SCR-01", "Elite Screens 100-inch Tripod Screen", "Projectors & Displays", "Equipment Room B, Floor", EquipmentCondition.Good, "Free-standing 16:9 projection screen with carry bag.", 110),
        new("DM-AUD-01", "Shure SM58 Vocal Microphone", "Audio Equipment", "Audio Closet, Bin 1", EquipmentCondition.Excellent, "Dynamic cardioid microphone with XLR cable and clip.", 70),
        new("DM-AUD-02", "Rode Wireless GO II Mic Kit", "Audio Equipment", "Audio Closet, Bin 2", EquipmentCondition.Good, "Dual-channel wireless lavalier kit with charging case.", 33),
        new("DM-AUD-03", "Zoom H5 Portable Recorder", "Audio Equipment", "Audio Closet, Bin 2", EquipmentCondition.Good, "Four-track handheld recorder with XY capsule and windscreen.", 58),
        new("DM-AUD-04", "JBL EON One Compact PA Speaker", "Audio Equipment", "Audio Closet, Floor", EquipmentCondition.Fair, "Battery-powered PA with four-channel mixer; grille dented.", 130),
        new("DM-LAB-01", "Fluke 117 Digital Multimeter", "Lab & Measurement", "Engineering Lab 2, Drawer 4", EquipmentCondition.Good, "True-RMS multimeter with test leads and holster.", 175),
        new("DM-LAB-02", "Rigol DS1054Z Oscilloscope", "Lab & Measurement", "Engineering Lab 2, Bench 1", EquipmentCondition.Excellent, "Four-channel 50 MHz digital oscilloscope with probes.", 48),
        new("DM-LAB-03", "Arduino Sensor Starter Kit", "Lab & Measurement", "Engineering Lab 2, Drawer 1", EquipmentCondition.Good, "Arduino Uno with 37 sensors, breadboard, and jumper wires.", 5),
        new("DM-NET-01", "TP-Link Portable Travel Router", "Networking & Accessories", "IT Help Desk, Drawer 2", EquipmentCondition.Good, "Pocket router for event Wi-Fi, with USB-C power.", 90),
        new("DM-ACC-01", "Logitech R500 Presentation Clicker", "Networking & Accessories", "IT Help Desk, Drawer 1", EquipmentCondition.Excellent, "Laser presenter with Bluetooth and USB receiver.", 14),
        new("DM-ACC-02", "HDMI and USB-C Adapter Kit", "Networking & Accessories", "IT Help Desk, Drawer 1", EquipmentCondition.Good, "USB-C to HDMI, VGA, and Ethernet adapters in a zip case.", 2),
        new("DM-ACC-03", "25 m Extension Cord Reel", "Networking & Accessories", "Equipment Room B, Floor", EquipmentCondition.Fair, "Four-outlet reel with overload breaker. Retired from lending.", 300, Active: false),
    ];

    /// <summary>Adds the demo catalog and activity if they are not there yet. Returns what was added.</summary>
    public static async Task<string> SeedAsync(ApplicationDbContext db, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (await db.EquipmentItems.AnyAsync(x => x.AssetCode.StartsWith(AssetPrefix), cancellationToken))
            return "Demo data is already present.";

        var demoUsers = await db.Users.AsNoTracking()
            .Where(x => x.Email != null && x.Email.EndsWith(AccountDomain) && x.IsActive && x.ApprovalStatus == AccountApprovalStatus.Approved)
            .Select(x => new { x.Id, x.Email, Roles = db.UserRoles.Where(r => r.UserId == x.Id).Join(db.Roles, r => r.RoleId, r => r.Id, (_, r) => r.Name).ToList() })
            .ToListAsync(cancellationToken);
        string? FirstIn(string role) => demoUsers.Where(x => x.Roles.Contains(role)).OrderBy(x => x.Email).Select(x => x.Id).FirstOrDefault();
        var custodianId = FirstIn("Custodian");
        var adminId = FirstIn("Administrator") ?? custodianId;
        var borrowerUserIds = demoUsers.Where(x => x.Roles.Contains("Borrower")).Select(x => x.Id).ToList();
        var borrowerProfiles = await db.BorrowerProfiles.AsNoTracking().Where(x => borrowerUserIds.Contains(x.UserId) && x.IsEligible)
            .OrderBy(x => x.CreatedAtUtc).Select(x => new { x.Id, x.UserId }).ToListAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var audit = new List<AuditEvent>();
        void Audit(string? actor, string action, string type, Guid id, DateTime at, object? after, object? before = null) => audit.Add(new AuditEvent
        {
            ActorUserId = actor, Action = action, EntityType = type, EntityId = id.ToString(), Outcome = "Succeeded",
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before), AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = "127.0.0.1", CorrelationId = "demo-data", CreatedAtUtc = at
        });

        // Catalog: reuse a category that already has the same name, otherwise add it.
        var categories = new Dictionary<string, EquipmentCategory>();
        foreach (var (name, description) in Categories)
        {
            var category = await db.EquipmentCategories.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
            if (category is null)
            {
                category = new EquipmentCategory { Name = name, Description = description, CreatedAtUtc = nowUtc.AddDays(-320), UpdatedAtUtc = nowUtc.AddDays(-320) };
                db.EquipmentCategories.Add(category);
                if (adminId is not null) Audit(adminId, "category.created", nameof(EquipmentCategory), category.Id, category.CreatedAtUtc, new { category.Name });
            }
            categories[name] = category;
        }
        var items = new Dictionary<string, EquipmentItem>();
        foreach (var spec in Items)
        {
            var added = nowUtc.AddDays(-spec.AddedDaysAgo).AddHours(-3);
            var item = new EquipmentItem
            {
                Category = categories[spec.Category], AssetCode = spec.Code, Name = spec.Name, Description = spec.Description,
                SerialNumber = SerialPrefix + spec.Code[AssetPrefix.Length..], Location = spec.Location, Condition = spec.Condition,
                IsActive = spec.Active, CreatedAtUtc = added, UpdatedAtUtc = added,
                // Damaged and unserviceable units are held until a repair is recorded, as a return in that condition would do.
                IsMaintenanceHold = spec.Condition is EquipmentCondition.Damaged or EquipmentCondition.Unserviceable
            };
            items[spec.Code] = item;
            db.EquipmentItems.Add(item);
            if (adminId is not null) Audit(adminId, "equipment.created", nameof(EquipmentItem), item.Id, added, new { item.AssetCode, item.Name, Condition = item.Condition.ToString() });
        }

        var summary = $"Added {Items.Length} demo items in {Categories.Length} categories.";
        if (custodianId is null || borrowerProfiles.Count == 0)
        {
            db.AuditEvents.AddRange(audit);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return summary + $" No reservations were added: they need an existing custodian and borrower account on {AccountDomain}.";
        }

        // Activity. Times are whole Manila hours (UTC+8) relative to now, so the data always looks current.
        var manilaToday = nowUtc.AddHours(8).Date;
        DateTime At(int dayOffset, int manilaHour) => manilaToday.AddDays(dayOffset).AddHours(manilaHour - 8);
        var borrowerIndex = 0;
        var borrowerUsers = borrowerProfiles.ToDictionary(x => x.Id, x => x.UserId);
        Guid NextBorrower() => borrowerProfiles[borrowerIndex++ % borrowerProfiles.Count].Id;
        var counts = new Dictionary<ReservationStatus, int>();

        Reservation Book(string code, int startDay, int startHour, int hours, ReservationStatus status, string purpose,
            int requestedDaysBefore = 3, string? reason = null)
        {
            var start = At(startDay, startHour);
            var created = start.AddDays(-requestedDaysBefore).AddHours(-2);
            if (created > nowUtc) created = nowUtc.AddHours(-1);
            var reservation = new Reservation
            {
                BorrowerProfileId = NextBorrower(), EquipmentItem = items[code], StartAtUtc = start, EndAtUtc = start.AddHours(hours),
                Status = status, Purpose = purpose, CreatedAtUtc = created, UpdatedAtUtc = created,
                HoldExpiresAtUtc = status == ReservationStatus.Pending ? Earlier(created.AddHours(24) > nowUtc ? created.AddHours(24) : nowUtc.AddHours(20), start) : null
            };
            db.Reservations.Add(reservation);
            counts[status] = counts.GetValueOrDefault(status) + 1;
            Audit(borrowerUsers[reservation.BorrowerProfileId], "reservation.created", nameof(Reservation), reservation.Id, created, new { Status = "Pending", EquipmentItemId = reservation.EquipmentItem.Id, reservation.BorrowerProfileId });
            if (status is ReservationStatus.Approved or ReservationStatus.Released or ReservationStatus.Completed or ReservationStatus.Rejected)
            {
                var decided = Earlier(created.AddHours(5), start.AddMinutes(-30));
                reservation.DecidedByUserId = custodianId;
                reservation.DecidedAtUtc = decided;
                reservation.DecisionReason = reason;
                reservation.UpdatedAtUtc = decided;
                var approved = status != ReservationStatus.Rejected;
                Audit(custodianId, approved ? "reservation.approved" : "reservation.rejected", nameof(Reservation), reservation.Id, decided,
                    new { Status = approved ? "Approved" : "Rejected" }, new { Status = "Pending" });
            }
            if (status == ReservationStatus.Cancelled)
            {
                reservation.CancelledAtUtc = created.AddHours(6);
                reservation.UpdatedAtUtc = reservation.CancelledAtUtc.Value;
                Audit(borrowerUsers[reservation.BorrowerProfileId], "reservation.cancelled", nameof(Reservation), reservation.Id, reservation.CancelledAtUtc.Value, new { Status = "Cancelled" }, new { Status = "Pending" });
            }
            if (status == ReservationStatus.Expired)
            {
                reservation.UpdatedAtUtc = created.AddHours(24);
                Audit(null, "reservation.expired", nameof(Reservation), reservation.Id, reservation.UpdatedAtUtc, new { Status = "Expired" }, new { Status = "Pending" });
            }
            return reservation;
        }

        Loan Lend(Reservation reservation, int releaseLateMinutes, EquipmentCondition atRelease, double? returnedAfterHours = null,
            EquipmentCondition? atReturn = null, string? returnNotes = null)
        {
            var released = reservation.StartAtUtc.AddMinutes(releaseLateMinutes);
            var loan = new Loan
            {
                Reservation = reservation, ReleasedAtUtc = released, DueAtUtc = reservation.EndAtUtc, ReleasedByUserId = custodianId,
                ConditionAtRelease = atRelease, ReleaseNotes = "Checked contents with the borrower at the desk.", CreatedAtUtc = released, UpdatedAtUtc = released
            };
            reservation.UpdatedAtUtc = released;
            Audit(custodianId, "reservation.released", nameof(Reservation), reservation.Id, released, new { Status = "Released" }, new { Status = "Approved" });
            if (returnedAfterHours is { } after)
            {
                var returned = released.AddHours(after);
                loan.ReturnedAtUtc = returned;
                loan.ReceivedByUserId = custodianId;
                loan.ConditionAtReturn = atReturn ?? atRelease;
                loan.ReturnNotes = returnNotes;
                loan.UpdatedAtUtc = returned;
                reservation.UpdatedAtUtc = returned;
                Audit(custodianId, "reservation.returned", nameof(Reservation), reservation.Id, returned,
                    new { Status = "Completed", Condition = loan.ConditionAtReturn.ToString(), Late = returned > loan.DueAtUtc }, new { Status = "Released" });
            }
            db.Loans.Add(loan);
            return loan;
        }

        // Completed loans across the last twelve weeks, so the weekly and daily charts have a shape.
        var returnedLoans = new (string Code, int Day, int Hour, int Hours, double ReturnAfter, EquipmentCondition AtReturn, string Purpose, string? Notes)[]
        {
            ("DM-PRJ-01", -80, 8, 6, 5.5, EquipmentCondition.Good, "Thesis defense rehearsal, Room 304", null),
            ("DM-CAM-01", -74, 9, 30, 29, EquipmentCondition.Excellent, "Photojournalism field assignment", null),
            ("DM-LAP-01", -66, 7, 10, 9.5, EquipmentCondition.Good, "Programming lab practical exam", null),
            ("DM-AUD-01", -59, 13, 5, 4.5, EquipmentCondition.Excellent, "Student council general assembly", null),
            ("DM-LAB-01", -52, 8, 8, 7.5, EquipmentCondition.Good, "Circuits lab: resistor network measurements", null),
            ("DM-LAP-03", -45, 8, 34, 33, EquipmentCondition.Damaged, "Capstone data collection at partner school", "Hinge cracked on the left side; screen flickers when opened past 90 degrees."),
            ("DM-PRJ-02", -38, 13, 4, 3.5, EquipmentCondition.Fair, "Org recruitment week booth", "Lamp dimmer than last loan; still usable."),
            ("DM-CAM-03", -31, 9, 26, 25, EquipmentCondition.Good, "Documentary film class shoot", null),
            ("DM-AUD-03", -24, 10, 6, 5.5, EquipmentCondition.Good, "Oral history interviews", null),
            ("DM-LAP-04", -17, 8, 9, 8.5, EquipmentCondition.Excellent, "Video editing workshop", null),
            ("DM-LAB-02", -12, 9, 5, 4.75, EquipmentCondition.Excellent, "Signals lab: waveform analysis", null),
            ("DM-AUD-02", -9, 14, 4, 3.75, EquipmentCondition.Good, "Podcast recording for journalism class", null),
            ("DM-SCR-01", -6, 7, 11, 10.5, EquipmentCondition.Good, "Department film showing, AVR", null),
            ("DM-CAM-02", -4, 8, 8, 7.5, EquipmentCondition.Good, "Sports fest photo coverage", null),
            ("DM-PRJ-01", -2, 13, 4, 3.75, EquipmentCondition.Good, "Guest lecture in the mini theater", null),
        };
        Loan? damagedLoan = null;
        foreach (var (code, day, hour, hours, after, condition, purpose, notes) in returnedLoans)
        {
            var reservation = Book(code, day, hour, hours, ReservationStatus.Completed, purpose);
            var loan = Lend(reservation, 10, items[code].Condition is EquipmentCondition.Damaged ? EquipmentCondition.Good : items[code].Condition, after, condition, notes);
            if (condition == EquipmentCondition.Damaged) damagedLoan = loan;
        }
        // Two returned late.
        foreach (var (code, day, hour, hours, lateHours, purpose) in new[]
        {
            ("DM-AUD-04", -27, 15, 4, 20.0, "Acoustic night at the quadrangle"),
            ("DM-TAB-01", -8, 8, 9, 26.0, "Field survey for environmental science"),
        })
        {
            var reservation = Book(code, day, hour, hours, ReservationStatus.Completed, purpose);
            Lend(reservation, 5, items[code].Condition, hours + lateHours, items[code].Condition, "Returned after the due time; borrower notified.");
        }

        // Out on loan right now: one overdue, one due back today, one due in two days.
        Lend(Book("DM-LAP-02", -3, 8, 30, ReservationStatus.Released, "Research paper writing sprint"), 15, EquipmentCondition.Good);
        var dueTodayStart = nowUtc.AddHours(-3);
        var dueToday = Book("DM-CAM-05", 0, 8, 1, ReservationStatus.Released, "Interview lighting for broadcast class", 2);
        dueToday.StartAtUtc = dueTodayStart;
        dueToday.EndAtUtc = Earlier(nowUtc.AddHours(4), At(1, 0).AddMinutes(-30)) > nowUtc.AddMinutes(30) ? Earlier(nowUtc.AddHours(4), At(1, 0).AddMinutes(-30)) : nowUtc.AddHours(1);
        Lend(dueToday, 5, EquipmentCondition.Good);
        Lend(Book("DM-AUD-02", -1, 9, 72, ReservationStatus.Released, "Three-day leadership summit recordings"), 10, EquipmentCondition.Good);

        // Approved and waiting: one whose time has started (ready to release), three later this week and next.
        var ready = Book("DM-PRJ-01", 0, 8, 1, ReservationStatus.Approved, "Faculty meeting presentation", 2);
        ready.StartAtUtc = nowUtc.AddMinutes(-40);
        ready.EndAtUtc = nowUtc.AddHours(3);
        Book("DM-CAM-01", 2, 9, 8, ReservationStatus.Approved, "Campus tour promotional video");
        Book("DM-LAB-02", 3, 13, 4, ReservationStatus.Approved, "Electronics lab final project testing");
        Book("DM-SCR-01", 6, 15, 5, ReservationStatus.Approved, "Freshmen orientation movie night");

        // Waiting for a decision.
        Book("DM-LAP-04", 1, 13, 5, ReservationStatus.Pending, "Graphic design portfolio review", 0);
        Book("DM-CAM-03", 2, 8, 9, ReservationStatus.Pending, "Wedding videography practicum", 0);
        Book("DM-AUD-01", 4, 17, 4, ReservationStatus.Pending, "Battle of the bands sound check", 0);
        Book("DM-LAB-03", 5, 9, 6, ReservationStatus.Pending, "IoT workshop for senior high outreach", 0);

        // Closed without a loan.
        Book("DM-CAM-02", -20, 9, 8, ReservationStatus.Rejected, "Personal travel photography", reason: "Equipment is for academic and organization use only.");
        Book("DM-LAP-01", -11, 8, 48, ReservationStatus.Rejected, "Weekend gaming tournament", reason: "Requested period exceeds the two-day limit for laptops.");
        Book("DM-PRJ-02", -15, 13, 3, ReservationStatus.Cancelled, "Class reporting, Section B");
        Book("DM-NET-01", -5, 8, 6, ReservationStatus.Cancelled, "Intramurals live-stream Wi-Fi");
        Book("DM-ACC-01", -18, 10, 3, ReservationStatus.Expired, "Thesis proposal defense");
        Book("DM-AUD-03", -3, 14, 5, ReservationStatus.Expired, "Choir rehearsal recording");

        // Maintenance: the damaged laptop and the dead mini projector are open cases; three repairs were completed earlier.
        MaintenanceCase Case(string code, DateTime opened, string description, MaintenanceStatus status, string? resolution = null,
            double? closedAfterDays = null, Loan? loan = null, string? opener = null)
        {
            var maintenance = new MaintenanceCase
            {
                EquipmentItem = items[code], Loan = loan, Description = description, Status = status, Resolution = resolution,
                OpenedAtUtc = opened, OpenedByUserId = opener ?? custodianId
            };
            Audit(maintenance.OpenedByUserId, "maintenance.opened", nameof(MaintenanceCase), maintenance.Id, opened, new { EquipmentItemId = maintenance.EquipmentItem.Id, Status = "Open" });
            if (closedAfterDays is { } days)
            {
                maintenance.ClosedAtUtc = opened.AddDays(days);
                maintenance.ClosedByUserId = custodianId;
                Audit(custodianId, "maintenance.updated", nameof(MaintenanceCase), maintenance.Id, maintenance.ClosedAtUtc.Value, new { Status = "Closed" }, new { Status = "Open" });
            }
            else if (status == MaintenanceStatus.InProgress)
                Audit(custodianId, "maintenance.updated", nameof(MaintenanceCase), maintenance.Id, opened.AddDays(1), new { Status = "InProgress" }, new { Status = "Open" });
            db.MaintenanceCases.Add(maintenance);
            return maintenance;
        }
        Case("DM-LAP-03", damagedLoan?.ReturnedAtUtc ?? nowUtc.AddDays(-44), "Returned with a cracked left hinge; display flickers past 90 degrees.",
            MaintenanceStatus.InProgress, "Replacement hinge ordered from the supplier; ETA two weeks.", loan: damagedLoan);
        Case("DM-PRJ-03", nowUtc.AddDays(-6), "Unit does not power on with either adapter. Suspected power board failure.", MaintenanceStatus.Open, opener: adminId);
        Case("DM-CAM-04", nowUtc.AddDays(-70), "Pan handle loose and leg lock sticks.", MaintenanceStatus.Closed, "Tightened the pan bar and cleaned the leg locks. One lock remains stiff but holds.", 3);
        Case("DM-AUD-04", nowUtc.AddDays(-26), "Crackling from the left channel at high volume after the acoustic night.", MaintenanceStatus.Closed, "Reseated the input jack and replaced the channel 1 cable. Tested at full volume.", 2);
        Case("DM-PRJ-02", nowUtc.AddDays(-36), "Lamp brightness low and fan noisy.", MaintenanceStatus.Closed, "Cleaned the air filter and reset the lamp timer; lamp still at 70% life.", 1.5);

        db.AuditEvents.AddRange(audit);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return summary + $" Added {counts.Values.Sum()} reservations ({string.Join(", ", counts.OrderBy(x => x.Key).Select(x => $"{x.Value} {x.Key}"))}), " +
            $"{db.ChangeTracker.Entries<Loan>().Count()} loans, 5 maintenance cases, and {audit.Count} audit events.";
    }

    /// <summary>
    /// Removes the demo records again: reservations, loans, maintenance cases, and audit events on demo items, then the
    /// items themselves and any demo category left empty. A demo item that someone outside the demo accounts has
    /// booked is deactivated instead, so their record stays whole.
    /// </summary>
    public static async Task<string> RemoveAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var demoItems = await db.EquipmentItems.Where(x => x.AssetCode.StartsWith(AssetPrefix)).ToListAsync(cancellationToken);
        if (demoItems.Count == 0) return "No demo data to remove.";
        var itemIds = demoItems.Select(x => x.Id).ToList();
        var demoProfiles = await db.BorrowerProfiles.Where(x => x.User.Email != null && x.User.Email.EndsWith(AccountDomain))
            .Select(x => x.Id).ToListAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var reservations = await db.Reservations.Where(x => itemIds.Contains(x.EquipmentItemId) && demoProfiles.Contains(x.BorrowerProfileId))
            .Include(x => x.Loan).ToListAsync(cancellationToken);
        var loanIds = reservations.Where(x => x.Loan is not null).Select(x => x.Loan!.Id).ToList();
        var cases = await db.MaintenanceCases.Where(x => itemIds.Contains(x.EquipmentItemId)).ToListAsync(cancellationToken);
        var entityIds = reservations.Select(x => x.Id).Concat(cases.Select(x => x.Id)).Concat(itemIds).Select(x => x.ToString()).ToList();
        db.AuditEvents.RemoveRange(await db.AuditEvents.Where(x => entityIds.Contains(x.EntityId)).ToListAsync(cancellationToken));
        db.MaintenanceCases.RemoveRange(cases);
        db.Loans.RemoveRange(reservations.Where(x => x.Loan is not null).Select(x => x.Loan!));
        db.Reservations.RemoveRange(reservations);
        await db.SaveChangesAsync(cancellationToken);

        var stillBooked = (await db.Reservations.Where(x => itemIds.Contains(x.EquipmentItemId)).Select(x => x.EquipmentItemId)
            .Distinct().ToListAsync(cancellationToken)).ToHashSet();
        foreach (var item in demoItems)
        {
            if (stillBooked.Contains(item.Id)) { item.IsActive = false; item.UpdatedAtUtc = DateTime.UtcNow; }
            else db.EquipmentItems.Remove(item);
        }
        await db.SaveChangesAsync(cancellationToken);
        var names = Categories.Select(x => x.Name).ToList();
        var emptyCategories = await db.EquipmentCategories.Where(x => names.Contains(x.Name) && !x.Items.Any()).ToListAsync(cancellationToken);
        var categoryIds = emptyCategories.Select(x => x.Id.ToString()).ToList();
        db.AuditEvents.RemoveRange(await db.AuditEvents.Where(x => x.EntityType == nameof(EquipmentCategory) && categoryIds.Contains(x.EntityId)).ToListAsync(cancellationToken));
        db.EquipmentCategories.RemoveRange(emptyCategories);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return $"Removed {reservations.Count} demo reservations, {loanIds.Count} loans, {cases.Count} maintenance cases, " +
            $"{demoItems.Count - stillBooked.Count} items, and {emptyCategories.Count} categories" +
            (stillBooked.Count > 0 ? $"; deactivated {stillBooked.Count} item(s) that other accounts had booked." : ".");
    }

    private static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
}
