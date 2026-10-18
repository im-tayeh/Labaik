using Labaik.Application.Common.Models;
using Labaik.Application.Features.Admin.Commands.CreateCompany;
using Labaik.Application.Features.Admin.Commands.UpdateCompany;
using Labaik.Application.Features.Admin.Queries.GetAdminOverview;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Domain.Companies.Enums;

namespace Labaik.Api.OpenApi;

// OpenAPI examples for the admin dashboard endpoints.
internal static class AdminExamples
{
    private static readonly Guid CompanyId = Guid.Parse("3f2b8c1e-7d4a-4e0b-9a51-2c6d8e9f1a01");
    private static readonly Guid OwnerId = Guid.Parse("9a1c2d3e-4f50-4617-8899-aabbccddeeff");
    private static readonly CompanyServicesDto Services = new(true, true, true, false, true);

    public static readonly CreateCompanyCommand CreateCompany = new()
    {
        Name = "Al Noor Hajj & Umrah",
        CommercialRegistrationNumber = "1010123456",
        Category = CompanyCategory.HajjAndUmrah,
        OfficialEmail = "info@alnoor.example",
        PrimaryPhone = "+966112345678",
        LogoUrl = "https://cdn.example.com/alnoor/logo.png",
        Country = "Saudi Arabia",
        City = "Makkah",
        VatId = "300123456700003",
        ContractReferenceNumber = "CTR-2026-014",
        SubscriptionPrice = 4500m,
        SubscriptionPeriod = SubscriptionPeriod.Year,
        ContractStartDate = new DateOnly(2026, 10, 1),
        ContractEndDate = new DateOnly(2027, 9, 30),
        MaxSupervisors = 20,
        MaxPilgrims = 800,
        ContractFileUrl = "https://cdn.example.com/alnoor/contract.pdf",
        ContactFullName = "Khalid Saleh",
        ContactJobTitle = "Operations Manager",
        ContactMobile = "+966501234567",
        EnabledServices = Services,
        OwnerEmail = "owner@alnoor.example"
    };

    public static readonly UpdateCompanyRequest UpdateCompany = new()
    {
        Name = CreateCompany.Name,
        CommercialRegistrationNumber = CreateCompany.CommercialRegistrationNumber,
        Category = CreateCompany.Category,
        OfficialEmail = CreateCompany.OfficialEmail,
        PrimaryPhone = CreateCompany.PrimaryPhone,
        LogoUrl = CreateCompany.LogoUrl,
        Country = CreateCompany.Country,
        City = CreateCompany.City,
        SubscriptionPrice = 5000m,
        SubscriptionPeriod = SubscriptionPeriod.Year,
        ContractStartDate = CreateCompany.ContractStartDate,
        ContractEndDate = new DateOnly(2027, 12, 31),
        MaxSupervisors = 25,
        MaxPilgrims = 1000,
        ContactFullName = CreateCompany.ContactFullName,
        ContactMobile = CreateCompany.ContactMobile,
        EnabledServices = Services
    };

    public static readonly AdminCompanyDto Company = new(
        CompanyId, CreateCompany.Name, OwnerId, CreateCompany.OwnerEmail, new DateTimeOffset(2026, 10, 18, 9, 30, 0, TimeSpan.Zero),
        CreateCompany.CommercialRegistrationNumber, CreateCompany.Category, CreateCompany.OfficialEmail, CreateCompany.PrimaryPhone,
        CreateCompany.LogoUrl, CreateCompany.Country, CreateCompany.City, CreateCompany.VatId, CreateCompany.ContractReferenceNumber,
        CreateCompany.SubscriptionPrice, CreateCompany.SubscriptionPeriod, CreateCompany.ContractStartDate, CreateCompany.ContractEndDate,
        CreateCompany.MaxSupervisors, CreateCompany.MaxPilgrims, CreateCompany.ContractFileUrl,
        CreateCompany.ContactFullName, CreateCompany.ContactJobTitle, CreateCompany.ContactMobile, Services,
        IsStopped: false, CompanyStatus.Active, DaysRemaining: 347,
        new AdminCompanyStatsDto(TripsCount: 3, ActiveTripsCount: 2, GroupsCount: 5, SupervisorsCount: 4, ActivePilgrims: 182));

    public static readonly IReadOnlyList<AdminCompanyListItemDto> Companies =
    [
        new(CompanyId, CreateCompany.Name, CreateCompany.LogoUrl, 182,
            new DateOnly(2026, 10, 1), new DateOnly(2027, 9, 30), CompanyStatus.Active, 347),
        new(Guid.Parse("5c7e9a1b-2d3f-4a5b-8c6d-7e8f9a0b1c2d"), "Rahma Travel", null, 0,
            new DateOnly(2025, 9, 1), new DateOnly(2026, 8, 31), CompanyStatus.Expired, 0)
    ];

    public static readonly AdminOverviewDto Overview = new(
        PendingRequests: 0, ActiveSubscriptions: 12, SubscriptionsExpiringIn30Days: 2,
        ActivePilgrims: 1840, ActiveCompanies: 14, TotalCompanies: 17);

    public static readonly UserAccountDto User = new(
        OwnerId, "supervisor@alnoor.example", "Omar Haddad", EmailConfirmed: true, ["Supervisor"]);
}
