using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Announcements;
using Labaik.Domain.Groups;
using Labaik.Domain.Guides;
using Labaik.Domain.Identity;
using Labaik.Domain.Journeys;
using Labaik.Domain.Rituals;
using Labaik.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext

{
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Journey> Journeys => Set<Journey>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<RitualStep> RitualSteps => Set<RitualStep>();
    public DbSet<RitualProgress> RitualProgresses => Set<RitualProgress>();
    public DbSet<GuideArticle> GuideArticles => Set<GuideArticle>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}