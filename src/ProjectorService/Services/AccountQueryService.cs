using DbMigrator.Entities;
using Microsoft.EntityFrameworkCore;
using ProjectorService.Persistence;

namespace ProjectorService.Services;

public class AccountQueryService
{
    private readonly ProjectorDbContext _dbContext;

    public AccountQueryService(ProjectorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectedState?> GetBalanceAsync(string accountId, CancellationToken ct)
    {
        return await _dbContext.ProjectedStates.FindAsync([accountId], ct);
    }

    public async Task<List<EventStoreEntry>> GetEventsAsync(string accountId, CancellationToken ct)
    {
        return await _dbContext.Events
            .Where(e => e.AccountId == accountId)
            .OrderBy(e => e.SequenceNum)
            .ToListAsync(ct);
    }
}
