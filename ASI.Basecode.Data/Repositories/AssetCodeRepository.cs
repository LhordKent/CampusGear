using System.Globalization;
using System.Text;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

// =====================================================================
// ASSET CODE REPOSITORY  -  the codes printed on equipment labels, e.g. CAM-001.
// Each category has one "series": a 3-letter prefix (from its name) and the next number to give out.
// =====================================================================
namespace ASI.Basecode.Data.Repositories
{
    public sealed class AssetCodeRepository : BaseRepository, IAssetCodeRepository
    {
        private const string PrefixLock = "CampusGear:AssetCodePrefixes";
        private readonly IWorkflowLockManager _locks;

        public AssetCodeRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext db, IWorkflowLockManager locks)
            : base(unitOfWork, db)
        {
            _locks = locks;
        }

        public async Task InitializeMissingAsync(CancellationToken ct)
        {
            await _locks.AcquireAsync(PrefixLock, ct);
            var categories = await Context.EquipmentCategories
                .Where(x => x.AssetCodeSeries == null)
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);
            if (categories.Count == 0)
            {
                return;
            }

            var usedPrefixes = await UsedPrefixesAsync(ct);
            foreach (var category in categories)
            {
                await AddSeriesAsync(category, usedPrefixes, ct);
            }
        }

        // New category: pick a free prefix (CAM, then CAM2, CAM3... if taken) and start its numbering.
        public async Task CreateSeriesAsync(EquipmentCategory category, CancellationToken ct)
        {
            await _locks.AcquireAsync(PrefixLock, ct);
            await AddSeriesAsync(category, await UsedPrefixesAsync(ct), ct);
        }

        // Inventory holds the category lock until the unit, audit event and counter commit together.
        // New item: returns the next code in the category's series (CAM-004) and moves the counter on.
        public async Task<string> AllocateAsync(Guid categoryId, CancellationToken ct)
        {
            var series = await Context.AssetCodeSeries.SingleAsync(x => x.CategoryId == categoryId, ct);
            while (true)
            {
                var code = series.Prefix + "-" + series.NextNumber.ToString("D3", CultureInfo.InvariantCulture);
                series.NextNumber = checked(series.NextNumber + 1);
                if (!await Context.EquipmentItems.AnyAsync(x => x.AssetCode == code, ct))
                {
                    return code;
                }
            }
        }

        private async Task<HashSet<string>> UsedPrefixesAsync(CancellationToken ct)
        {
            var prefixes = await Context.AssetCodeSeries.Select(x => x.Prefix).ToListAsync(ct);
            return new HashSet<string>(prefixes, StringComparer.OrdinalIgnoreCase);
        }

        private async Task AddSeriesAsync(EquipmentCategory category, HashSet<string> usedPrefixes, CancellationToken ct)
        {
            var letters = category.Name.Normalize(NormalizationForm.FormD).ToUpperInvariant()
                .Where(c => c is >= 'A' and <= 'Z')
                .Take(3);
            var stem = string.Concat(letters);
            stem = stem.Length == 0 ? "EQP" : stem.PadRight(3, 'X');

            var prefix = stem;
            for (var suffix = 2; ; suffix++)
            {
                if (usedPrefixes.Add(prefix))
                {
                    var nextNumber = await NextAfterExistingCodesAsync(prefix, ct);
                    if (nextNumber is { } number)
                    {
                        Context.AssetCodeSeries.Add(new AssetCodeSeries
                        {
                            Prefix = prefix,
                            CategoryId = category.Id,
                            NextNumber = number,
                        });
                        return;
                    }
                }

                prefix = stem + suffix.ToString(CultureInfo.InvariantCulture);
            }
        }

        private async Task<long?> NextAfterExistingCodesAsync(string prefix, CancellationToken ct)
        {
            var start = prefix + "-";
            var codes = await Context.EquipmentItems
                .Where(x => x.AssetCode.StartsWith(start))
                .Select(x => x.AssetCode)
                .ToListAsync(ct);
            var next = 1L;
            foreach (var code in codes)
            {
                var suffix = code[start.Length..].TrimEnd();
                if (suffix.Length == 0 || !suffix.All(char.IsAsciiDigit))
                {
                    continue;
                }

                if (!long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number >= long.MaxValue - 1)
                {
                    return null;
                }

                next = Math.Max(next, number + 1);
            }

            return next;
        }
    }
}
