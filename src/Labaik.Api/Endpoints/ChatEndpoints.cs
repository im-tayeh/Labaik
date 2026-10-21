using Asp.Versioning;
using Asp.Versioning.Builder;
using Labaik.Api.Extensions;
using Labaik.Api.OpenApi;
using Labaik.Application.Features.DirectChat;
using Labaik.Domain.Companies;
using MediatR;

namespace Labaik.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1)).ReportApiVersions().Build();
        var group = app.MapGroup("/api/v{version:apiVersion}/chats")
            .WithApiVersionSet(versionSet).WithTags("Direct chat")
            .RequireAuthorization().RequireCompanyService(s => s.ChatWithGuides);

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetMyConversationsQuery(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithName("GetMyConversations").WithSummary("List my direct conversations")
            .Returns<IReadOnlyList<ConversationDto>>().Fails(401, 403);

        group.MapPost("/direct", async (StartDirectChatRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new StartDirectChatCommand(body.UserId), ct);
            return result.IsSuccess
                ? result.Value.Created ? Results.Created($"/api/v1/chats/{result.Value.Conversation.Id}", result.Value.Conversation)
                    : Results.Ok(result.Value.Conversation)
                : result.Error!.ToProblem();
        }).WithName("StartDirectChat").WithSummary("Open an existing or new conversation with a linked supervisor or pilgrim")
            .Returns<ConversationDto>().Returns<ConversationDto>(StatusCodes.Status201Created).Fails(400, 401, 403).Validates();

        group.MapGet("/{conversationId:guid}/messages", async (Guid conversationId, ISender sender,
            CancellationToken ct, DateTimeOffset? before, int take = 50) =>
        {
            var result = await sender.Send(new GetDirectMessagesQuery(conversationId, before, take), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
        }).WithName("GetDirectMessages").WithSummary("Read direct message history (participants only)")
            .Returns<IReadOnlyList<DirectMessageDto>>().Fails(400, 401, 404).Validates();

        group.MapPost("/{conversationId:guid}/messages", async (Guid conversationId, SendDirectMessageRequest body,
            ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SendDirectMessageCommand(conversationId, body.Body), ct);
            return result.IsSuccess ? Results.Created($"/api/v1/chats/{conversationId}/messages/{result.Value.Id}", result.Value)
                : result.Error!.ToProblem();
        }).WithName("SendDirectMessage").WithSummary("Send a direct message; notifies the recipient in realtime")
            .Returns<DirectMessageDto>(StatusCodes.Status201Created).Fails(400, 401, 404).Validates();

        group.MapPost("/{conversationId:guid}/messages/read", async (Guid conversationId, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new MarkDirectChatReadCommand(conversationId), ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        }).WithName("MarkDirectChatRead").WithSummary("Mark direct messages read up to now")
            .ReturnsNoContent().Fails(401, 404);

        return app;
    }
}
