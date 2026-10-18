using Labaik.Domain.Announcements;
using Labaik.Domain.Chat;
using Labaik.Domain.Companies;
using Labaik.Domain.Notifications;
using Labaik.Domain.Groups;
using Labaik.Domain.Guides;
using Labaik.Domain.HelpCenter;
using Labaik.Domain.Journeys;
using Labaik.Domain.Locations;
using Labaik.Domain.Map;
using Labaik.Domain.Profiles;
using Labaik.Domain.Rituals;
using Labaik.Domain.Trips;
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
    DbSet<LocationShare> LocationShares { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Company> Companies { get; }
    DbSet<Trip> Trips { get; }
    DbSet<PilgrimProfile> PilgrimProfiles { get; }
    DbSet<MapPlace> MapPlaces { get; }
    DbSet<EmergencyAlert> EmergencyAlerts { get; }
    DbSet<LostChildReport> LostChildReports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}