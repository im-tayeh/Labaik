using Labaik.Application.Features.Companies.Commands.UpdateMyCompany;
using Labaik.Application.Features.Companies.Dtos;

namespace Labaik.Api.OpenApi;

// OpenAPI examples for the company view endpoints.
internal static class CompanyExamples
{
    public static readonly Guid CompanyId = Guid.Parse("3f2b8c1e-7d4a-4e0b-9a51-2c6d8e9f1a01");

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
}
