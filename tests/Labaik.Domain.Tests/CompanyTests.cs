using Labaik.Domain.Announcements;
using Labaik.Domain.Announcements.Enums;
using Labaik.Domain.Companies;
using Labaik.Domain.Companies.Enums;
using Labaik.Domain.Companies.Events;

namespace Labaik.Domain.Tests;

public class CompanyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 1);

    internal static CompanyDetails Details(string name = "Al Noor Travel") => new(name, "CR-1010", "Khalid Saleh");

    internal static Company NewCompany(Guid? owner = null) => Company.Create(Details(), owner ?? Guid.NewGuid(), Now).Value;

    [Fact]
    public void Create_trims_fields_and_raises_event()
    {
        var details = Details("  Al Noor Travel ") with { City = "  Makkah ", VatId = " ", LogoUrl = "https://cdn.example.com/logo.png" };

        var company = Company.Create(details, Guid.NewGuid(), Now).Value;

        Assert.Equal("Al Noor Travel", company.Name);
        Assert.Equal("Makkah", company.City);
        Assert.Null(company.VatId);
        Assert.Equal(CompanyServices.All, company.EnabledServices); // omitted -> every service on
        Assert.IsType<CompanyCreated>(Assert.Single(company.DomainEvents));
    }

    [Fact]
    public void Create_validates_required_fields_and_owner()
    {
        Assert.Equal("Company.NameRequired", Company.Create(Details(" "), Guid.NewGuid(), Now).Error!.Code);
        Assert.Equal("Company.NameTooLong", Company.Create(Details(new string('x', 151)), Guid.NewGuid(), Now).Error!.Code);
        Assert.Equal("Company.RegistrationNumberRequired",
            Company.Create(Details() with { CommercialRegistrationNumber = "" }, Guid.NewGuid(), Now).Error!.Code);
        Assert.Equal("Company.ContactNameRequired",
            Company.Create(Details() with { ContactFullName = " " }, Guid.NewGuid(), Now).Error!.Code);
        Assert.Equal("Company.OwnerRequired", Company.Create(Details(), Guid.Empty, Now).Error!.Code);
    }

    [Theory]
    [InlineData("http://insecure.example.com/logo.png", null, null, null, "Company.InvalidUrl")]
    [InlineData(null, -1, null, null, "Company.NegativeNumber")]
    [InlineData(null, null, "2026-12-01", "2026-11-01", "Company.ContractEndBeforeStart")]
    public void Create_validates_optional_fields(string? logoUrl, int? maxPilgrims, string? start, string? end, string expected)
    {
        var details = Details() with
        {
            LogoUrl = logoUrl,
            MaxPilgrims = maxPilgrims,
            ContractStartDate = start is null ? null : DateOnly.Parse(start),
            ContractEndDate = end is null ? null : DateOnly.Parse(end)
        };

        Assert.Equal(expected, Company.Create(details, Guid.NewGuid(), Now).Error!.Code);
    }

    [Fact]
    public void Update_validates_first_and_keeps_old_values_on_failure()
    {
        var company = NewCompany();

        Assert.True(company.Update(Details("New") with { ContactFullName = "" }).IsFailure);
        Assert.Equal("Al Noor Travel", company.Name);
        Assert.True(company.Update(Details("New")).IsSuccess);
        Assert.Equal("New", company.Name);
    }

    [Fact]
    public void Company_profile_edit_keeps_admin_only_fields()
    {
        var company = Company.Create(Details() with { MaxPilgrims = 500, ContractEndDate = Today.AddDays(30) }, Guid.NewGuid(), Now).Value;

        var edit = company.UpdateProfile(new CompanyProfile("Renamed", "CR-2", "New Contact", null, "SA", "Jeddah", null, null));

        Assert.True(edit.IsSuccess);
        Assert.Equal("Renamed", company.Name);
        Assert.Equal("Jeddah", company.City);
        Assert.Equal(500, company.MaxPilgrims);
        Assert.Equal(Today.AddDays(30), company.ContractEndDate);
    }

    [Fact]
    public void Supervisor_roster_rejects_duplicates_and_the_owner()
    {
        var owner = Guid.NewGuid();
        var company = NewCompany(owner);
        var supervisor = Guid.NewGuid();

        Assert.True(company.AddSupervisor(supervisor, Now).IsSuccess);
        Assert.True(company.HasSupervisor(supervisor));
        Assert.Equal("Company.SupervisorAlreadyAdded", company.AddSupervisor(supervisor, Now).Error!.Code);
        Assert.Equal("Company.OwnerCannotBeSupervisor", company.AddSupervisor(owner, Now).Error!.Code);
        Assert.Equal("Company.SupervisorRequired", company.AddSupervisor(Guid.Empty, Now).Error!.Code);
        Assert.Single(company.Supervisors);
    }

    [Fact]
    public void Status_and_days_remaining_follow_the_contract_and_stop_flag()
    {
        var company = Company.Create(Details() with { ContractEndDate = Today.AddDays(10) }, Guid.NewGuid(), Now).Value;

        Assert.Equal(CompanyStatus.Active, company.StatusOn(Today));
        Assert.Equal(10, company.DaysRemainingOn(Today));
        Assert.Equal(CompanyStatus.Expired, company.StatusOn(Today.AddDays(11)));
        Assert.Equal(0, company.DaysRemainingOn(Today.AddDays(11)));

        company.Stop();
        Assert.Equal(CompanyStatus.Stopped, company.StatusOn(Today));
        company.Resume();
        Assert.Equal(CompanyStatus.Active, company.StatusOn(Today));

        Assert.Null(NewCompany().DaysRemainingOn(Today));
        Assert.Equal(CompanyStatus.Active, NewCompany().StatusOn(Today));
    }
}

public class AnnouncementContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Only_the_company_owner_announces_to_all_pilgrims()
    {
        var owner = Guid.NewGuid();
        var company = CompanyTests.NewCompany(owner);
        var content = new AnnouncementContent("Bus", "Lobby at 6");

        Assert.Equal("Announcement.NotCompanyOwner", company.PostAnnouncement(Guid.NewGuid(), content, Now).Error!.Code);

        var post = company.PostAnnouncement(owner, content, Now).Value;
        Assert.Null(post.GroupId);
        Assert.Equal(company.Id, post.CompanyId);
        Assert.Equal(AnnouncementAudience.AllCompanyPilgrims, post.Audience);
    }

    [Theory]
    [InlineData("http://insecure.example.com/a.jpg", "Announcement.InvalidImageUrl")]
    [InlineData("not a url", "Announcement.InvalidImageUrl")]
    public void Content_is_validated(string imageUrl, string expectedError)
    {
        var company = CompanyTests.NewCompany();
        var content = new AnnouncementContent("T", "B", ImageUrl: imageUrl);

        Assert.Equal(expectedError, company.PostAnnouncement(company.OwnerUserId, content, Now).Error!.Code);
    }
}
