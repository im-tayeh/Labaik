using Labaik.Application.Features.Announcements.Commands.PostCompanyAnnouncement;
using Labaik.Application.Features.Announcements.Dtos;
using Labaik.Application.Features.Companies.Commands.AddCompanySupervisor;
using Labaik.Application.Features.Companies.Commands.UpdateMyCompany;
using Labaik.Application.Features.Companies.Dtos;
using Labaik.Application.Features.Companies.Queries.GetCompanyHome;
using Labaik.Application.Features.Groups.Commands.AssignGroupSupervisor;
using Labaik.Application.Features.Groups.Commands.CreateTripGroup;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Application.Features.Trips.Commands.CreateTrip;
using Labaik.Application.Features.Trips.Commands.UpdateTrip;
using Labaik.Application.Features.Trips.Dtos;
using Labaik.Domain.Announcements.Enums;
using Labaik.Domain.Journeys.Enums;
using Labaik.Domain.Trips.Enums;

namespace Labaik.Api.OpenApi;

// OpenAPI examples for the company view endpoints.
internal static class CompanyExamples
{
    public static readonly Guid CompanyId = Guid.Parse("3f2b8c1e-7d4a-4e0b-9a51-2c6d8e9f1a01");
    private static readonly Guid OwnerId = Guid.Parse("9a1c2d3e-4f50-4617-8899-aabbccddeeff");
    private static readonly Guid TripId = Guid.Parse("1b2c3d4e-5f60-4718-8a9b-0c1d2e3f4a5b");
    private static readonly Guid GroupId = Guid.Parse("7e8f9a0b-1c2d-4e3f-8a5b-6c7d8e9f0a1b");
    private static readonly Guid SupervisorId = Guid.Parse("2a3b4c5d-6e7f-4809-9a1b-2c3d4e5f6a7b");

    public static readonly UpdateMyCompanyCommand UpdateMyCompany = new(
        Name: "Al Noor Hajj & Umrah",
        CommercialRegistrationNumber: "1010123456",
        ContactFullName: "Khalid Saleh",
        LogoUrl: "https://cdn.example.com/alnoor/logo.png",
        Country: "Saudi Arabia",
        City: "Makkah",
        OfficialEmail: "info@alnoor.example",
        PrimaryPhone: "+966112345678");

    public static readonly CompanyDto Company = new(
        CompanyId, UpdateMyCompany.Name, UpdateMyCompany.LogoUrl, UpdateMyCompany.CommercialRegistrationNumber,
        UpdateMyCompany.Country, UpdateMyCompany.City, UpdateMyCompany.OfficialEmail, UpdateMyCompany.PrimaryPhone,
        UpdateMyCompany.ContactFullName);

    public static readonly CompanyHomeDto Home = new(ActiveTrips: 2, PilgrimsCount: 182, SupervisorsCount: 4, GroupsCount: 5);

    public static readonly AddCompanySupervisorCommand AddSupervisor = new("Omar Haddad", "omar@alnoor.example", "+966551112233");

    public static readonly CompanySupervisorDto Supervisor = new(
        SupervisorId, AddSupervisor.FullName, AddSupervisor.Email, AddSupervisor.PhoneNumber,
        new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero), GroupsCount: 1);

    public static readonly IReadOnlyList<CompanySupervisorDto> Supervisors =
    [
        Supervisor,
        new(Guid.Parse("4c5d6e7f-8091-4a2b-8c3d-4e5f6a7b8c9d"), "Yusuf Amin", "yusuf@alnoor.example", null,
            new DateTimeOffset(2026, 10, 18, 10, 5, 0, TimeSpan.Zero), GroupsCount: 0)
    ];

    public static readonly CreateTripCommand CreateTrip = new(
        "December Umrah", PilgrimageType.Umrah, ["Makkah", "Madinah"],
        new DateOnly(2026, 12, 10), new DateOnly(2026, 12, 20), "Amman", 40, "Hotel near the Haram");

    public static readonly UpdateTripRequest UpdateTrip = new(
        CreateTrip.Name, CreateTrip.PilgrimageType, CreateTrip.Destinations,
        CreateTrip.DepartureDate, new DateOnly(2026, 12, 22), CreateTrip.DeparturePlace, 45, CreateTrip.Notes);

    public static readonly TripDto Trip = new(
        TripId, CreateTrip.Name, CreateTrip.PilgrimageType, CreateTrip.Destinations, CreateTrip.DepartureDate,
        CreateTrip.ReturnDate, CreateTrip.DeparturePlace, CreateTrip.ExpectedPilgrims, CreateTrip.Notes,
        TripStatus.Upcoming, new DateTimeOffset(2026, 10, 18, 11, 0, 0, TimeSpan.Zero));

    public static readonly IReadOnlyList<TripSummaryDto> Trips =
    [
        new(TripId, Trip.Name, Trip.PilgrimageType, Trip.DepartureDate, Trip.ReturnDate, Trip.ExpectedPilgrims, TripStatus.Upcoming)
    ];

    public static readonly CreateTripGroupRequest CreateGroup = new("Group A");

    public static readonly CompanyGroupDto Group = new(GroupId, TripId, "Group A", "K7P2QX", SupervisorId, "Omar Haddad", PilgrimCount: 38);

    public static readonly CompanyGroupDto NewGroup = Group with { SupervisorId = null, SupervisorName = null, PilgrimCount = 0 };

    public static readonly AssignGroupSupervisorRequest AssignSupervisor = new(AddSupervisor.Email);

    public static readonly PostCompanyAnnouncementCommand Announce = new(
        "Bus departure", "Buses leave the hotel lobby at 6:00 for Madinah.",
        AnnouncementImportance.Important, new DateTimeOffset(2026, 12, 14, 3, 0, 0, TimeSpan.Zero), "Hotel lobby", null);

    public static readonly AnnouncementDto Announcement = new(
        Guid.Parse("6d7e8f90-a1b2-4c3d-8e4f-5a6b7c8d9e0f"), null, Announce.Title, Announce.Body, OwnerId, "Khalid Saleh",
        new DateTimeOffset(2026, 12, 13, 18, 0, 0, TimeSpan.Zero), IsRead: true, AnnouncementImportance.Important,
        AnnouncementAudience.AllCompanyPilgrims, Announce.GatheringTimeUtc, Announce.Location, null);
}
