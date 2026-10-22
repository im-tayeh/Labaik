using Labaik.Application.Common.Interfaces;

namespace Labaik.Application.Common.Security;

// After a change that may block accounts (company stopped, supervisor suspended), tells each affected user
// who is now actually blocked (same checks as login) so their signed-in app signs out at once.
// A supervisor still active on another company is not blocked and gets nothing.
public static class AccountBlockPush
{
    public static async Task PushAsync(
        IIdentityService identityService, IRealtimeNotifier notifier, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            var block = await identityService.GetAccessBlockAsync(userId, cancellationToken);
            if (block is not null)
            {
                await notifier.AccountBlockedAsync(userId, block.Code, cancellationToken);
            }
        }
    }
}
