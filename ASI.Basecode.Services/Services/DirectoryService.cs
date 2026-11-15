using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Services
{
    public sealed class DirectoryService : ServiceBase, IDirectoryService
    {
        private readonly IDirectoryReadRepository _reads;
        private readonly IEquipmentAvailabilityService _availability;
        private readonly TimeProvider _timeProvider;

        public DirectoryService(IDirectoryReadRepository reads, IEquipmentAvailabilityService availability, TimeProvider clock)
        {
            _reads = reads;
            _availability = availability;
            _timeProvider = clock;
        }

        public async Task<IReadOnlyList<DirectoryCategory>> GetCategoriesAsync(CancellationToken ct)
        {
            var statuses = await _availability.GetStatesAsync(_timeProvider.GetUtcNow().UtcDateTime, ct);
            return (await _reads.CategoriesAsync(ct))
                .Select(x => new DirectoryCategory(
                    x.Name,
                    x.Description,
                    x.IsActive,
                    x.Units.Select(u => new DirectoryUnit(
                            u.Name,
                            u.AssetCode,
                            u.Condition,
                            u.Location,
                            statuses.GetValueOrDefault(u.Id, ItemStatus.Inactive)
                        ))
                        .ToList()
                ))
                .ToList();
        }

        public async Task<DirectoryBorrowersPage> GetBorrowersAsync(string? query, string? eligibility, int page, CancellationToken ct)
        {
            query = query?.Trim() ?? "";
            if (query.Length > 120)
            {
                query = query[..120];
            }

            eligibility = eligibility is "eligible" or "paused" ? eligibility : "all";
            var data = await _reads.BorrowersAsync(query, eligibility, page, ct);
            return new(
                data.Rows.Select(x => new DirectoryBorrower(
                        x.FullName,
                        x.Email,
                        x.StudentNumber,
                        x.Department,
                        x.ContactNumber,
                        x.IsEligible,
                        x.EligibilityNotes,
                        x.IsActive
                    ))
                    .ToList(),
                query,
                eligibility,
                data.Page,
                data.Pages,
                data.Total
            );
        }
    }
}
