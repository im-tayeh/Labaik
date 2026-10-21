using Labaik.Application.Common.Interfaces;

namespace Labaik.Api.Services;

// Reset bumps a generation that is part of every cache key, so all cached answers are dropped at once
// (these changes are rare admin/company actions, so dropping everyone's entry is cheap).
internal sealed class AccountAccessCache : IAccountAccessCache
{
    private long _generation;

    public void Reset() => Interlocked.Increment(ref _generation);

    public string Key(Guid userId) => $"account-block:{Interlocked.Read(ref _generation)}:{userId}";
}
