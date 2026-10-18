namespace Labaik.Application.Common.Interfaces;

public interface IUserDirectory
{
    Task<string?> GetFullNameAsync(Guid userId, CancellationToken cancellationToken = default);

    // Name + email of one account (null when it does not exist).
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken cancellationToken = default);

    // Contacts of many users in one round-trip; unknown ids are absent from the result.
    Task<IReadOnlyDictionary<Guid, UserContact>> GetContactsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    // One round-trip for many users; unknown ids are absent from the result.
    Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

public sealed record UserContact(Guid Id, string FullName, string? Email, string? PhoneNumber = null);
