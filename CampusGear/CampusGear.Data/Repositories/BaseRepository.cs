using CampusGear.Data.Interfaces;

namespace CampusGear.Data.Repositories
{
    /// <summary>
    /// Gives repositories the request's shared persistence scope. Repositories stage
    /// changes; the calling service saves and commits the complete business operation.
    /// </summary>
    public abstract class BaseRepository
    {
        protected ApplicationDbContext Context { get; }

        protected IUnitOfWork UnitOfWork { get; }

        protected BaseRepository(IUnitOfWork unitOfWork, ApplicationDbContext context)
        {
            UnitOfWork = unitOfWork;
            Context = context;
        }
    }
}
