using Labaik.Domain.Common;
using Labaik.Domain.Common.Results;
using Labaik.Domain.Map.Events;

namespace Labaik.Domain.Map;

public enum EmergencyStatus { Open = 1, Resolved = 2 }

// A pilgrim's SOS from field mode: where they are and an optional note. The supervisors of the
// pilgrim's groups are notified (EmergencyReported); the position is a snapshot, not live tracking.
public sealed class EmergencyAlert : AggregateRoot
{
    public Guid UserId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public string? Note { get; private set; }
    public EmergencyStatus Status { get; private set; } = EmergencyStatus.Open;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public Guid? ResolvedBy { get; private set; }

    private EmergencyAlert() { } // EF

    public static Result<EmergencyAlert> Create(
        Guid userId, double latitude, double longitude, double? accuracyMeters, string? note, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty) return MapErrors.UserRequired;
        if (!Geo.IsValid(latitude, longitude)) return MapErrors.InvalidCoordinates;

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > MapErrors.NoteMaxLength }) return MapErrors.NoteTooLong;

        var alert = new EmergencyAlert
        {
            UserId = userId,
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters is >= 0 ? accuracyMeters : null,
            Note = note,
            CreatedAtUtc = nowUtc
        };
        alert.Raise(new EmergencyReported(alert.Id, userId));
        return alert;
    }

    // Idempotent. Who may resolve (the pilgrim or one of their supervisors) is checked by the application layer.
    public void Resolve(Guid resolvedBy, DateTimeOffset nowUtc)
    {
        if (Status == EmergencyStatus.Resolved)
        {
            return;
        }

        Status = EmergencyStatus.Resolved;
        ResolvedBy = resolvedBy;
        ResolvedAtUtc = nowUtc;
    }
}
