using Labaik.Domain.Announcements;
using Labaik.Domain.Groups;
using Labaik.Domain.Guides;
using Labaik.Domain.Journeys;
using Labaik.Domain.Rituals;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Journey> Journeys { get; }
    DbSet<Group> Groups { get; }
    DbSet<RitualStep> RitualSteps { get; }
    DbSet<RitualProgress> RitualProgresses { get; }
    DbSet<GuideArticle> GuideArticles { get; }
    DbSet<Announcement> Announcements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}