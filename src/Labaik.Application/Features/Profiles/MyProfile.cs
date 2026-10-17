using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Profiles;
using Labaik.Domain.Profiles.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labaik.Application.Features.Profiles;

// The pilgrim's own profile. All fields optional; UpdatedAtUtc is null until first saved.
public sealed record MyProfileDto(Gender? Gender, string? Nationality, string? PhotoUrl, string? HealthNotes, DateTimeOffset? UpdatedAtUtc);

internal static class ProfileMapper
{
    public static MyProfileDto ToMyDto(this PilgrimProfile? p) => p is null
        ? new MyProfileDto(null, null, null, null, null)
        : new MyProfileDto(p.Gender, p.Nationality, p.PhotoUrl, p.HealthNotes, p.UpdatedAtUtc);
}

// --- Read ---

public sealed record GetMyProfileQuery : IRequest<Result<MyProfileDto>>;

public sealed class GetMyProfileQueryHandler(
    IAppDbContext context,
    ICurrentUser currentUser) : IRequestHandler<GetMyProfileQuery, Result<MyProfileDto>>
{
    public async Task<Result<MyProfileDto>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var profile = await context.PilgrimProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile.ToMyDto(); // nothing is created by a read
    }
}

// --- Replace (null clears a field) ---

public sealed record UpdateMyProfileCommand(Gender? Gender, string? Nationality, string? PhotoUrl, string? HealthNotes)
    : IRequest<Result<MyProfileDto>>;

public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.Gender).IsInEnum().When(x => x.Gender is not null);
        RuleFor(x => x.Nationality).Length(2).Matches("^[A-Za-z]{2}$").When(x => !string.IsNullOrWhiteSpace(x.Nationality));
        RuleFor(x => x.PhotoUrl).MaximumLength(ProfileErrors.PhotoUrlMaxLength);
        RuleFor(x => x.HealthNotes).MaximumLength(ProfileErrors.HealthNotesMaxLength);
    }
}

public sealed class UpdateMyProfileCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<UpdateMyProfileCommand, Result<MyProfileDto>>
{
    public async Task<Result<MyProfileDto>> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var profile = await context.PilgrimProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            var create = PilgrimProfile.Create(userId);
            if (create.IsFailure)
            {
                return create.Error!;
            }

            profile = create.Value;
            context.PilgrimProfiles.Add(profile);
        }

        var update = profile.Update(request.Gender, request.Nationality, request.PhotoUrl, request.HealthNotes, timeProvider.GetUtcNow());
        if (update.IsFailure)
        {
            return update.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return profile.ToMyDto();
    }
}
