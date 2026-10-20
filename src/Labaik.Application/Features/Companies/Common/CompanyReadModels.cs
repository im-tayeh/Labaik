using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Companies;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Companies.Common;

// Shared read-side queries for the admin dashboard and the company view.
internal static class CompanyReadModels
{
    // Company groups under a trip that is not over yet (no return date, or returning today or later, UTC).
    public static IQueryable<Group> ActiveTripGroups(this IAppDbContext context, DateOnly todayUtc)
        => context.Groups.Where(g => g.CompanyId != null
            && context.Trips.Any(t => t.Id == g.TripId && (t.ReturnDate == null || t.ReturnDate >= todayUtc)));

    // Distinct pilgrims (not supervisors) per company, in groups of active trips.
    public static async Task<Dictionary<Guid, int>> CountActivePilgrimsByCompanyAsync(
        this IAppDbContext context, DateOnly todayUtc, CancellationToken cancellationToken, Guid? companyId = null)
    {
        var groups = context.ActiveTripGroups(todayUtc);
        if (companyId is { } id)
        {
            groups = groups.Where(g => g.CompanyId == id);
        }

        return await groups
            .SelectMany(g => g.Members.Where(m => m.Role == GroupMemberRole.Member),
                (g, m) => new { CompanyId = g.CompanyId!.Value, m.UserId })
            .Distinct()
            .GroupBy(x => x.CompanyId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
    }

    // Supervisors on the company's roster (assigned to a group or not).
    public static Task<int> CountRosterSupervisorsAsync(
        this IAppDbContext context, Guid companyId, CancellationToken cancellationToken)
        => context.Companies
            .Where(c => c.Id == companyId)
            .SelectMany(c => c.Supervisors)
            .CountAsync(cancellationToken);

    public static async Task<AdminCompanyStatsDto> GetCompanyStatsAsync(
        this IAppDbContext context, Guid companyId, DateOnly todayUtc, CancellationToken cancellationToken)
    {
        var trips = context.Trips.Where(t => t.CompanyId == companyId);
        var groups = context.Groups.Where(g => g.CompanyId == companyId);

        var tripsCount = await trips.CountAsync(cancellationToken);
        var activeTrips = await trips.CountAsync(t => t.ReturnDate == null || t.ReturnDate >= todayUtc, cancellationToken);
        var groupsCount = await groups.CountAsync(cancellationToken);
        var supervisors = await context.CountRosterSupervisorsAsync(companyId, cancellationToken);
        var pilgrims = await context.CountActivePilgrimsByCompanyAsync(todayUtc, cancellationToken, companyId);

        return new AdminCompanyStatsDto(tripsCount, activeTrips, groupsCount, supervisors, pilgrims.GetValueOrDefault(companyId));
    }

    public static async Task<AdminCompanyDto> ToAdminDtoAsync(
        this Company c,
        IAppDbContext context,
        IUserDirectory userDirectory,
        DateOnly todayUtc,
        CancellationToken cancellationToken)
    {
        var owner = await userDirectory.GetContactAsync(c.OwnerUserId, cancellationToken);
        var stats = await context.GetCompanyStatsAsync(c.Id, todayUtc, cancellationToken);

        return new AdminCompanyDto(
            c.Id, c.Name, c.OwnerUserId, owner?.Email ?? string.Empty, c.CreatedAtUtc,
            c.CommercialRegistrationNumber, c.Category, c.OfficialEmail, c.PrimaryPhone, c.LogoUrl, c.Country, c.City,
            c.VatId, c.ContractReferenceNumber, c.SubscriptionPrice, c.SubscriptionPeriod,
            c.ContractStartDate, c.ContractEndDate, c.MaxSupervisors, c.MaxPilgrims, c.ContractFileUrl,
            c.ContactFullName, c.ContactJobTitle, c.ContactMobile, CompanyServicesDto.From(c.EnabledServices),
            c.IsStopped, c.StatusOn(todayUtc), c.DaysRemainingOn(todayUtc), stats);
    }

    public static CompanyDto ToDto(this Company c, DateOnly todayUtc, string? supportEmail = null) => new(
        c.Id, c.Name, c.LogoUrl, c.CommercialRegistrationNumber, c.Country, c.City,
        c.OfficialEmail, c.PrimaryPhone, c.ContactFullName, c.StatusOn(todayUtc), c.ContractEndDate,
        CompanyServicesDto.From(c.EnabledServices), supportEmail);
}
