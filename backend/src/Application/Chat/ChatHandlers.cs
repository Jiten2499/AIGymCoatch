using LifeFitAI.Domain.Chat;
using LifeFitAI.SharedKernel.Results;
using MediatR;

namespace LifeFitAI.Application.Chat;

public sealed record ListChatInboxQuery(DateTime? Before, int Take, string? Term, string? Filter) : IRequest<Result<IReadOnlyList<ChatInboxItem>>>;

public sealed record ListChatMessagesQuery(Guid ConversationId, DateTime? Before, Guid? BeforeId, int Take) : IRequest<Result<IReadOnlyList<ChatMessageItem>>>;

public sealed record SearchChatRecipientsQuery(string? Term) : IRequest<Result<IReadOnlyList<ChatPerson>>>;

public sealed record GetChatSummaryQuery : IRequest<Result<ChatSummaryResponse>>;

public sealed record GetChatPermissionsQuery : IRequest<Result<IReadOnlyList<ChatRuleResponse>>>;

public sealed record DownloadChatAttachmentQuery(Guid AttachmentId) : IRequest<Result<ChatAttachmentContent>>;

public sealed record GetChatPhotoQuery(Guid UserId) : IRequest<Result<ChatPhotoFile>>;

public sealed record OpenDirectChatCommand(Guid RecipientUserId) : IRequest<Result<ChatInboxItem>>;

public sealed record SendChatMessageCommand(Guid ConversationId, string Body, string? ClientMessageId) : IRequest<Result<ChatMessageItem>>;

public sealed record SendChatAttachmentCommand(Guid ConversationId, string FileName, string ContentType, byte[] Content, string? ClientMessageId) : IRequest<Result<ChatMessageItem>>;

public sealed record MarkChatReadCommand(Guid ConversationId, Guid MessageId) : IRequest<Result>;

public sealed record UpdateChatPermissionsCommand(IReadOnlyList<ChatRuleResponse> Rules) : IRequest<Result<IReadOnlyList<ChatRuleResponse>>>;

public sealed class ChatHandler :
    IRequestHandler<ListChatInboxQuery, Result<IReadOnlyList<ChatInboxItem>>>,
    IRequestHandler<ListChatMessagesQuery, Result<IReadOnlyList<ChatMessageItem>>>,
    IRequestHandler<SearchChatRecipientsQuery, Result<IReadOnlyList<ChatPerson>>>,
    IRequestHandler<GetChatSummaryQuery, Result<ChatSummaryResponse>>,
    IRequestHandler<GetChatPermissionsQuery, Result<IReadOnlyList<ChatRuleResponse>>>,
    IRequestHandler<DownloadChatAttachmentQuery, Result<ChatAttachmentContent>>,
    IRequestHandler<GetChatPhotoQuery, Result<ChatPhotoFile>>,
    IRequestHandler<OpenDirectChatCommand, Result<ChatInboxItem>>,
    IRequestHandler<SendChatMessageCommand, Result<ChatMessageItem>>,
    IRequestHandler<SendChatAttachmentCommand, Result<ChatMessageItem>>,
    IRequestHandler<MarkChatReadCommand, Result>,
    IRequestHandler<UpdateChatPermissionsCommand, Result<IReadOnlyList<ChatRuleResponse>>>
{
    private readonly ChatMessageService _chat;

    public ChatHandler(ChatMessageService chat) => _chat = chat;

    public Task<Result<IReadOnlyList<ChatInboxItem>>> Handle(ListChatInboxQuery request, CancellationToken cancellationToken) =>
        _chat.ListInboxAsync(request.Before, request.Take, request.Term, request.Filter, cancellationToken);

    public Task<Result<IReadOnlyList<ChatMessageItem>>> Handle(ListChatMessagesQuery request, CancellationToken cancellationToken) =>
        _chat.ListMessagesAsync(request.ConversationId, request.Before, request.BeforeId, request.Take, cancellationToken);

    public Task<Result<IReadOnlyList<ChatPerson>>> Handle(SearchChatRecipientsQuery request, CancellationToken cancellationToken) =>
        _chat.SearchAsync(request.Term, cancellationToken);

    public Task<Result<ChatSummaryResponse>> Handle(GetChatSummaryQuery request, CancellationToken cancellationToken) =>
        _chat.SummaryAsync(cancellationToken);

    public Task<Result<IReadOnlyList<ChatRuleResponse>>> Handle(GetChatPermissionsQuery request, CancellationToken cancellationToken) =>
        _chat.GetRulesAsync(cancellationToken);

    public Task<Result<ChatAttachmentContent>> Handle(DownloadChatAttachmentQuery request, CancellationToken cancellationToken) =>
        _chat.DownloadAsync(request.AttachmentId, cancellationToken);

    public Task<Result<ChatPhotoFile>> Handle(GetChatPhotoQuery request, CancellationToken cancellationToken) =>
        _chat.PhotoAsync(request.UserId, cancellationToken);

    public Task<Result<ChatInboxItem>> Handle(OpenDirectChatCommand request, CancellationToken cancellationToken) =>
        _chat.OpenDirectAsync(request.RecipientUserId, cancellationToken);

    public Task<Result<ChatMessageItem>> Handle(SendChatMessageCommand request, CancellationToken cancellationToken) =>
        _chat.SendTextAsync(request.ConversationId, request.Body, request.ClientMessageId, cancellationToken);

    public Task<Result<ChatMessageItem>> Handle(SendChatAttachmentCommand request, CancellationToken cancellationToken) =>
        _chat.SendAttachmentAsync(request.ConversationId, request.FileName, request.ContentType, request.Content, request.ClientMessageId, cancellationToken);

    public Task<Result> Handle(MarkChatReadCommand request, CancellationToken cancellationToken) =>
        _chat.MarkReadAsync(request.ConversationId, request.MessageId, cancellationToken);

    public Task<Result<IReadOnlyList<ChatRuleResponse>>> Handle(UpdateChatPermissionsCommand request, CancellationToken cancellationToken) =>
        _chat.SaveRulesAsync(request.Rules, cancellationToken);
}
