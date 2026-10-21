using FirebaseAdmin.Messaging;
using Labaik.Application.Common.Interfaces;
using Labaik.Domain.Notifications.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Notifications;

internal sealed class FcmPushSender(IAppDbContext context, FirebaseMessaging messaging,
    ILogger<FcmPushSender> logger) : IPushSender
{
    public async Task SendAsync(Guid userId, NotificationType type, Guid? referenceId, string? preview,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokens = await context.DeviceTokens.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => x.Token).ToListAsync(cancellationToken);
            foreach (var batch in tokens.Chunk(500))
            {
                var messages = batch.Select(token => new Message
                {
#pragma warning disable CS0618 // FCM registration tokens remain the mobile client contract; Firebase FIDs are not interchangeable.
                    Token = token,
#pragma warning restore CS0618
                    Data = new Dictionary<string, string>
                    {
                        ["type"] = ((int)type).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["referenceId"] = referenceId?.ToString() ?? string.Empty,
                        ["preview"] = preview ?? string.Empty
                    }
                }).ToList();
                if (messages.Count == 0) continue;
                var response = await messaging.SendEachAsync(messages, cancellationToken);
                var expired = response.Responses.Select((sent, index) => (sent, index))
                    .Where(x => !x.sent.IsSuccess && x.sent.Exception is FirebaseMessagingException ex
                        && ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
                    .Select(x => batch[x.index]).ToArray();
                if (expired.Length > 0)
                {
                    await context.DeviceTokens.Where(x => expired.Contains(x.Token)).ExecuteDeleteAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FCM delivery failed for user {UserId} and notification type {Type}.", userId, type);
        }
    }
}
