using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Announcements;
using Labaik.Domain.Chat;
using Labaik.Domain.Companies;
using Labaik.Domain.FamilyCircles;
using Labaik.Domain.Notifications;
using Labaik.Domain.Groups;
using Labaik.Domain.Guides;
using Labaik.Domain.HelpCenter;
using Labaik.Domain.Identity;
using Labaik.Domain.Journeys;
using Labaik.Domain.Locations;
using Labaik.Domain.Map;
using Labaik.Domain.Profiles;
using Labaik.Domain.Rituals;
using Labaik.Domain.Trips;
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
    public DbSet<LocationShare> LocationShares => Set<LocationShare>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<PilgrimProfile> PilgrimProfiles => Set<PilgrimProfile>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<MapPlace> MapPlaces => Set<MapPlace>();
    public DbSet<EmergencyAlert> EmergencyAlerts => Set<EmergencyAlert>();
    public DbSet<LostChildReport> LostChildReports => Set<LostChildReport>();
    public DbSet<FamilyCircle> FamilyCircles => Set<FamilyCircle>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(user => user.MustChangePassword).HasDefaultValue(false);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
