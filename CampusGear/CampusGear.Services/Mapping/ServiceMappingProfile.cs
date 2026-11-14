using AutoMapper;
using CampusGear.Data.QueryModels;
using CampusGear.Services.ServiceModels;

namespace CampusGear.Services.Mapping
{
    /// <summary>Read-model mappings only. Identity and state-transition writes are explicit.</summary>
    public sealed class ServiceMappingProfile : Profile
    {
        public ServiceMappingProfile()
        {
            CreateMap<CategoryListRecord, CategoryListItem>();
        }
    }
}
