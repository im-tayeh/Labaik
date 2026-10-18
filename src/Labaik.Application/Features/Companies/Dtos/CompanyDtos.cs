using Labaik.Domain.Companies;
using Labaik.Domain.Companies.Enums;

namespace Labaik.Application.Features.Companies.Dtos;

// Company view: welcome header + "edit company data" screen (fields after name are additive).
public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    string CommercialRegistrationNumber,
    string? Country,
    string? City,
    string? OfficialEmail,
    string? PrimaryPhone,
    string ContactFullName);

// A supervisor on the company's roster. GroupsCount = the company's groups they currently supervise.
public sealed record CompanySupervisorDto(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTimeOffset AddedAtUtc,
    int GroupsCount);

public sealed record CompanyServicesDto(
    bool GpsTracking,
    bool DuaAndRituals,
    bool InstantNotifications,
    bool ChatWithGuides,
    bool ReportsAndStats)
{
    public CompanyServices ToDomain() => new(GpsTracking, DuaAndRituals, InstantNotifications, ChatWithGuides, ReportsAndStats);

    public static CompanyServicesDto From(CompanyServices s) =>
        new(s.GpsTracking, s.DuaAndRituals, s.InstantNotifications, s.ChatWithGuides, s.ReportsAndStats);
}

// Admin dashboard: live numbers for one company.
// ActivePilgrims: distinct pilgrims in groups whose trip is not over (no return date, or returning today or later).
public sealed record AdminCompanyStatsDto(
    int TripsCount,
    int ActiveTripsCount,
    int GroupsCount,
    int SupervisorsCount,
    int ActivePilgrims);

// Admin dashboard: everything on the company form + owner, computed status and numbers.
// Status: 1 Active / 2 Stopped / 3 Expired. DaysRemaining: until contractEndDate (null without one, 0 once ended).
public sealed record AdminCompanyDto(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string OwnerEmail,
    DateTimeOffset CreatedAtUtc,
    string CommercialRegistrationNumber,
    CompanyCategory? Category,
    string? OfficialEmail,
    string? PrimaryPhone,
    string? LogoUrl,
    string? Country,
    string? City,
    string? VatId,
    string? ContractReferenceNumber,
    decimal? SubscriptionPrice,
    SubscriptionPeriod? SubscriptionPeriod,
    DateOnly? ContractStartDate,
    DateOnly? ContractEndDate,
    int? MaxSupervisors,
    int? MaxPilgrims,
    string? ContractFileUrl,
    string ContactFullName,
    string? ContactJobTitle,
    string? ContactMobile,
    CompanyServicesDto EnabledServices,
    bool IsStopped,
    CompanyStatus Status,
    int? DaysRemaining,
    AdminCompanyStatsDto Stats);

// Admin dashboard companies table row.
public sealed record AdminCompanyListItemDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    int ActivePilgrims,
    DateOnly? SubscriptionStartUtc,
    DateOnly? SubscriptionEndUtc,
    CompanyStatus Status,
    int? DaysRemaining);
