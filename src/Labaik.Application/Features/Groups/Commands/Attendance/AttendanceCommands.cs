using FluentValidation;
using Labaik.Application.Common.Interfaces;
using Labaik.Application.Features.Groups.Common;
using Labaik.Application.Features.Groups.Dtos;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Groups;
using Labaik.Domain.Groups.Enums;
using MediatR;

namespace Labaik.Application.Features.Groups.Commands.Attendance;

// --- Pilgrim: "I arrived" (idempotent; notifies the supervisor once) ---

public sealed record ConfirmArrivalCommand(Guid GroupId) : IRequest<Result>;

public sealed class ConfirmArrivalCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<ConfirmArrivalCommand, Result>
{
    public async Task<Result> Handle(ConfirmArrivalCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Result.Failure(Error.Unauthorized("Auth.Required", "Authentication is required."));
        }

        var group = await context.GetGroupForMemberAsync(request.GroupId, userId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(GroupErrors.NotFound);
        }

        var confirm = group.ConfirmArrival(userId, timeProvider.GetUtcNow());
        if (confirm.IsFailure)
        {
            return confirm;
        }

        await context.SaveChangesAsync(cancellationToken); // PilgrimArrived -> supervisor notification
        return Result.Success();
    }
}

// --- Supervisor: mark a pilgrim present/absent manually ---

public sealed record SetAttendanceRequest(AttendanceStatus Status);

public sealed record SetAttendanceCommand(Guid GroupId, Guid UserId, AttendanceStatus Status) : IRequest<Result<GroupAttendanceDto>>;

public sealed class SetAttendanceCommandValidator : AbstractValidator<SetAttendanceCommand>
{
    public SetAttendanceCommandValidator() => RuleFor(x => x.Status).IsInEnum();
}

public sealed class SetAttendanceCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory,
    TimeProvider timeProvider) : IRequestHandler<SetAttendanceCommand, Result<GroupAttendanceDto>>
{
    public async Task<Result<GroupAttendanceDto>> Handle(SetAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.GetGroupForMemberAsync(request.GroupId, userId, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        var set = group.SetAttendance(userId, request.UserId, request.Status, timeProvider.GetUtcNow());
        if (set.IsFailure)
        {
            return set.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await group.ToAttendanceDtoAsync(userDirectory, cancellationToken);
    }
}

// --- Supervisor: start a new roll call (everyone Absent) ---

public sealed record ResetAttendanceCommand(Guid GroupId) : IRequest<Result<GroupAttendanceDto>>;

public sealed class ResetAttendanceCommandHandler(
    IAppDbContext context,
    ICurrentUser currentUser,
    IUserDirectory userDirectory) : IRequestHandler<ResetAttendanceCommand, Result<GroupAttendanceDto>>
{
    public async Task<Result<GroupAttendanceDto>> Handle(ResetAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
        {
            return Error.Unauthorized("Auth.Required", "Authentication is required.");
        }

        var group = await context.GetGroupForMemberAsync(request.GroupId, userId, cancellationToken);
        if (group is null)
        {
            return GroupErrors.NotFound;
        }

        var reset = group.ResetAttendance(userId);
        if (reset.IsFailure)
        {
            return reset.Error!;
        }

        await context.SaveChangesAsync(cancellationToken);
        return await group.ToAttendanceDtoAsync(userDirectory, cancellationToken);
    }
}
