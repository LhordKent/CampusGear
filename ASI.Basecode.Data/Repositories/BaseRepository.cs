using ASI.Basecode.Data.Interfaces;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Gives repositories the request's shared persistence scope. Repositories stage
    /// changes; the calling service saves and commits the complete business operation.
    /// </summary>
    public abstract class BaseRepository
    {
        protected AsiBasecodeDBContext Context { get; }

        protected IUnitOfWork UnitOfWork { get; }

        protected BaseRepository(IUnitOfWork unitOfWork, AsiBasecodeDBContext context)
        {
            UnitOfWork = unitOfWork;
            Context = context;
        }
    }
}
