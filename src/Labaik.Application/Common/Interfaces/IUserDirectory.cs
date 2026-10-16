namespace Labaik.Application.Common.Interfaces;

public interface IUserDirectory
{
    Task<string?> GetFullNameAsync(Guid userId, CancellationToken cancellationToken = default);

    // One round-trip for many users; unknown ids are absent from the result.
    Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
