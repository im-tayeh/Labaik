namespace Labaik.Application.Common.Interfaces;

public interface IUserDirectory
{
    Task<string?> GetFullNameAsync(Guid userId, CancellationToken cancellationToken = default);
}