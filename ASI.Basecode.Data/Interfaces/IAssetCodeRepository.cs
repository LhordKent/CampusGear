using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAssetCodeRepository
    {
        Task InitializeMissingAsync(CancellationToken ct);
        Task CreateSeriesAsync(EquipmentCategory category, CancellationToken ct);
        Task<string> AllocateAsync(Guid categoryId, CancellationToken ct);
    }
}
