using Labaik.Domain.Companies;
using Labaik.Domain.Companies.Events;

namespace Labaik.Domain.Tests;

public class CompanyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_name_and_raises_event()
    {
        var company = Company.Create("  Al Noor Travel ", Guid.NewGuid(), Now).Value;

        Assert.Equal("Al Noor Travel", company.Name);
        Assert.IsType<CompanyCreated>(Assert.Single(company.DomainEvents));
    }

    [Fact]
    public void Create_validates_name_and_owner()
    {
        Assert.Equal("Company.NameRequired", Company.Create(" ", Guid.NewGuid(), Now).Error!.Code);
        Assert.Equal("Company.NameTooLong", Company.Create(new string('x', 151), Guid.NewGuid(), Now).Error!.Code);
        Assert.Equal("Company.OwnerRequired", Company.Create("Name", Guid.Empty, Now).Error!.Code);
    }

    [Fact]
    public void Rename_validates_and_keeps_old_name_on_failure()
    {
        var company = Company.Create("Old", Guid.NewGuid(), Now).Value;

        Assert.True(company.Rename(" ").IsFailure);
        Assert.Equal("Old", company.Name);
        Assert.True(company.Rename("New").IsSuccess);
        Assert.Equal("New", company.Name);
    }
}
