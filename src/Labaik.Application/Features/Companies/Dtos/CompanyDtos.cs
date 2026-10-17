namespace Labaik.Application.Features.Companies.Dtos;

// Company view's welcome header.
public sealed record CompanyDto(Guid Id, string Name);

// Admin view of a company.
public sealed record AdminCompanyDto(Guid Id, string Name, Guid OwnerUserId, string OwnerEmail, DateTimeOffset CreatedAtUtc);
