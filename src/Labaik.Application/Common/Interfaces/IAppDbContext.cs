using Labaik.Domain.Groups;
using Labaik.Domain.Journeys;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Journey> Journeys { get; }
    DbSet<Group> Groups { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}