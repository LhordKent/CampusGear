using CampusGear.Services.ServiceModels;
namespace CampusGear.Services;
public interface ICurrentAccountService { Task<CurrentAccountDisplay> GetAsync(string? userId, string fallback); }
