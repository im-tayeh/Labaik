using Labaik.Domain.Announcements;
using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Companies.Enums;
using Labaik.Domain.Companies.Events;

namespace Labaik.Domain.Companies;

// An Umrah/Hajj operator. Created only by a platform admin; owned by one user account
// (the account holding the Company role). Owns trips and the groups under them.
// Contract, capacity and service fields are stored for the admin dashboard only: billing,
// limits and feature flags are not enforced.
public sealed class Company : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string CommercialRegistrationNumber { get; private set; } = string.Empty;
    public CompanyCategory? Category { get; private set; }
    public string? OfficialEmail { get; private set; }
    public string? PrimaryPhone { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? Country { get; private set; }
    public string? City { get; private set; }

    public string? VatId { get; private set; }
    public string? ContractReferenceNumber { get; private set; }
    public decimal? SubscriptionPrice { get; private set; }
    public SubscriptionPeriod? SubscriptionPeriod { get; private set; }
    public DateOnly? ContractStartDate { get; private set; }
    public DateOnly? ContractEndDate { get; private set; }
    public int? MaxSupervisors { get; private set; }
    public int? MaxPilgrims { get; private set; }
    public string? ContractFileUrl { get; private set; }

    public string ContactFullName { get; private set; } = string.Empty;
    public string? ContactJobTitle { get; private set; }
    public string? ContactMobile { get; private set; }

    public CompanyServices EnabledServices { get; private set; } = CompanyServices.None;

    // Suspended by an admin (status Stopped regardless of the contract dates).
    public bool IsStopped { get; private set; }

    // Supervisor roster (accounts the company provisioned or linked).
    private readonly List<CompanySupervisor> _supervisors = [];
    public IReadOnlyList<CompanySupervisor> Supervisors => _supervisors.AsReadOnly();

    private Company() { } // EF

    private Company(Guid ownerUserId, DateTimeOffset createdAtUtc)
    {
        OwnerUserId = ownerUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public static Result<Company> Create(CompanyDetails details, Guid ownerUserId, DateTimeOffset createdAtUtc)
    {
        if (ownerUserId == Guid.Empty)
        {
            return CompanyErrors.OwnerRequired;
        }

        var company = new Company(ownerUserId, createdAtUtc);
        var update = company.Update(details);
        if (update.IsFailure)
        {
            return update.Error!;
        }

        company.Raise(new CompanyCreated(company.Id, ownerUserId));
        return company;
    }

    // Full replace (admin edit). Validates everything first, so a failure changes nothing.
    public Result Update(CompanyDetails details)
    {
        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        static bool Over(string? s, int max) => s is not null && s.Length > max;
        static bool IsHttps(string? url) => url is null
            || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);

        var name = Clean(details.Name);
        if (name is null) return Result.Failure(CompanyErrors.NameRequired);
        if (name.Length > CompanyErrors.NameMaxLength) return Result.Failure(CompanyErrors.NameTooLong);

        var registration = Clean(details.CommercialRegistrationNumber);
        if (registration is null) return Result.Failure(CompanyErrors.RegistrationNumberRequired);

        var contactName = Clean(details.ContactFullName);
        if (contactName is null) return Result.Failure(CompanyErrors.ContactNameRequired);

        var officialEmail = Clean(details.OfficialEmail);
        var primaryPhone = Clean(details.PrimaryPhone);
        var logoUrl = Clean(details.LogoUrl);
        var country = Clean(details.Country);
        var city = Clean(details.City);
        var vatId = Clean(details.VatId);
        var contractReference = Clean(details.ContractReferenceNumber);
        var contractFileUrl = Clean(details.ContractFileUrl);
        var jobTitle = Clean(details.ContactJobTitle);
        var mobile = Clean(details.ContactMobile);

        if (Over(registration, CompanyErrors.RegistrationNumberMaxLength)
            || Over(contactName, CompanyErrors.ContactNameMaxLength)
            || Over(officialEmail, CompanyErrors.EmailMaxLength)
            || Over(primaryPhone, CompanyErrors.PhoneMaxLength) || Over(mobile, CompanyErrors.PhoneMaxLength)
            || Over(country, CompanyErrors.PlaceMaxLength) || Over(city, CompanyErrors.PlaceMaxLength)
            || Over(vatId, CompanyErrors.ReferenceMaxLength) || Over(contractReference, CompanyErrors.ReferenceMaxLength)
            || Over(jobTitle, CompanyErrors.JobTitleMaxLength)
            || Over(logoUrl, CompanyErrors.UrlMaxLength) || Over(contractFileUrl, CompanyErrors.UrlMaxLength))
        {
            return Result.Failure(CompanyErrors.TooLong);
        }

        if (!IsHttps(logoUrl) || !IsHttps(contractFileUrl))
        {
            return Result.Failure(CompanyErrors.InvalidUrl);
        }

        if (details.SubscriptionPrice < 0 || details.MaxSupervisors < 0 || details.MaxPilgrims < 0)
        {
            return Result.Failure(CompanyErrors.NegativeNumber);
        }

        if (details.ContractStartDate is { } start && details.ContractEndDate is { } end && end < start)
        {
            return Result.Failure(CompanyErrors.ContractEndBeforeStart);
        }

        Name = name;
        CommercialRegistrationNumber = registration;
        Category = details.Category;
        OfficialEmail = officialEmail;
        PrimaryPhone = primaryPhone;
        LogoUrl = logoUrl;
        Country = country;
        City = city;
        VatId = vatId;
        ContractReferenceNumber = contractReference;
        SubscriptionPrice = details.SubscriptionPrice;
        SubscriptionPeriod = details.SubscriptionPeriod;
        ContractStartDate = details.ContractStartDate;
        ContractEndDate = details.ContractEndDate;
        MaxSupervisors = details.MaxSupervisors;
        MaxPilgrims = details.MaxPilgrims;
        ContractFileUrl = contractFileUrl;
        ContactFullName = contactName;
        ContactJobTitle = jobTitle;
        ContactMobile = mobile;
        EnabledServices = details.EnabledServices ?? CompanyServices.None;
        return Result.Success();
    }

    // The company's own "edit company data" screen: a subset of the fields; the rest stays as the admin set it.
    public Result UpdateProfile(CompanyProfile profile) => Update(ToDetails() with
    {
        Name = profile.Name,
        CommercialRegistrationNumber = profile.CommercialRegistrationNumber,
        LogoUrl = profile.LogoUrl,
        Country = profile.Country,
        City = profile.City,
        OfficialEmail = profile.OfficialEmail,
        PrimaryPhone = profile.PrimaryPhone,
        ContactFullName = profile.ContactFullName
    });

    public Result<CompanySupervisor> AddSupervisor(Guid userId, DateTimeOffset addedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            return CompanyErrors.SupervisorRequired;
        }

        if (userId == OwnerUserId)
        {
            return CompanyErrors.OwnerCannotBeSupervisor;
        }

        if (HasSupervisor(userId))
        {
            return CompanyErrors.SupervisorAlreadyAdded;
        }

        var supervisor = new CompanySupervisor(userId, addedAtUtc);
        _supervisors.Add(supervisor);
        return supervisor;
    }

    public bool HasSupervisor(Guid userId) => _supervisors.Any(s => s.UserId == userId);

    public void Stop() => IsStopped = true;

    public void Resume() => IsStopped = false;

    public CompanyStatus StatusOn(DateOnly todayUtc) => StatusOf(IsStopped, ContractEndDate, todayUtc);

    public int? DaysRemainingOn(DateOnly todayUtc) => DaysRemainingOf(ContractEndDate, todayUtc);

    // Static forms for read models that project columns instead of loading the aggregate.
    public static CompanyStatus StatusOf(bool isStopped, DateOnly? contractEndDate, DateOnly todayUtc) =>
        isStopped ? CompanyStatus.Stopped
        : contractEndDate is { } end && end < todayUtc ? CompanyStatus.Expired
        : CompanyStatus.Active;

    // Whole days until the contract ends (0 once ended); null when there is no end date.
    public static int? DaysRemainingOf(DateOnly? contractEndDate, DateOnly todayUtc) =>
        contractEndDate is { } end ? Math.Max(0, end.DayNumber - todayUtc.DayNumber) : null;

    // Company-wide broadcast to every pilgrim in its groups. The new Announcement is its own aggregate.
    public Result<Announcement> PostAnnouncement(Guid authorId, AnnouncementContent content, DateTimeOffset postedAtUtc)
    {
        if (authorId != OwnerUserId)
        {
            return AnnouncementErrors.NotCompanyOwner;
        }

        return Announcement.CreateForCompany(Id, authorId, content, postedAtUtc);
    }

    private CompanyDetails ToDetails() => new(
        Name, CommercialRegistrationNumber, ContactFullName,
        Category, OfficialEmail, PrimaryPhone, LogoUrl, Country, City,
        VatId, ContractReferenceNumber, SubscriptionPrice, SubscriptionPeriod, ContractStartDate, ContractEndDate,
        MaxSupervisors, MaxPilgrims, ContractFileUrl, ContactJobTitle, ContactMobile, EnabledServices);
}

// Editable fields of a company (admin create and update take the same shape).
public sealed record CompanyDetails(
    string Name,
    string CommercialRegistrationNumber,
    string ContactFullName,
    CompanyCategory? Category = null,
    string? OfficialEmail = null,
    string? PrimaryPhone = null,
    string? LogoUrl = null,
    string? Country = null,
    string? City = null,
    string? VatId = null,
    string? ContractReferenceNumber = null,
    decimal? SubscriptionPrice = null,
    SubscriptionPeriod? SubscriptionPeriod = null,
    DateOnly? ContractStartDate = null,
    DateOnly? ContractEndDate = null,
    int? MaxSupervisors = null,
    int? MaxPilgrims = null,
    string? ContractFileUrl = null,
    string? ContactJobTitle = null,
    string? ContactMobile = null,
    CompanyServices? EnabledServices = null);

// The subset the company account may edit itself.
public sealed record CompanyProfile(
    string Name,
    string CommercialRegistrationNumber,
    string ContactFullName,
    string? LogoUrl,
    string? Country,
    string? City,
    string? OfficialEmail,
    string? PrimaryPhone);

// Services enabled in the company's package. Stored only; not enforced.
public sealed record CompanyServices(
    bool GpsTracking,
    bool DuaAndRituals,
    bool InstantNotifications,
    bool ChatWithGuides,
    bool ReportsAndStats)
{
    public static CompanyServices None { get; } = new(false, false, false, false, false);
}
