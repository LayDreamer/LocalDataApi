using LocalDataApi.Domain.WeChatWork;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Infrastructure.WeChatWork;
using LocalDataApi.WeChatWork;
using Microsoft.EntityFrameworkCore;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信群聊用例:创建群聊(持久化到本地库)、向群聊发送消息。
/// </summary>
public class WeChatWorkGroupChatService : WechatWorkServiceBase
{
    private readonly AppDbContext _context;

    public WeChatWorkGroupChatService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        AppDbContext context,
        ILogger<WeChatWorkGroupChatService> logger)
        : base(client, tokenProvider, logger)
    {
        _context = context;
    }

    /// <summary>
    /// 创建企业微信群聊并发送消息(一步完成)
    /// </summary>
    public async Task<(CgibinAppChatCreateResponse CreateResult, CgibinAppChatSendResponse? SendResult)>
        CreateChatAndSendMessageAsync(
            List<string> userIds,
            string chatName,
            string ownerUserId,
            string content,
            WechatWorkMessageType msgType = WechatWorkMessageType.Text,
            string? chatId = null,
            string? title = null,
            string? description = null,
            string? url = null,
            string? buttonText = null,
            CancellationToken ct = default)
    {
        // 1. 创建群聊
        var createResponse = await CreateGroupChatAsync(userIds, chatName, ownerUserId, chatId, ct);
        if (!createResponse.IsSuccessful())
        {
            _logger.LogError("创建群聊失败: [{ErrorCode}] {ErrorMessage}",
                createResponse.ErrorCode, createResponse.ErrorMessage);
            throw new InvalidOperationException(
                $"创建群聊失败: [{createResponse.ErrorCode}] {createResponse.ErrorMessage}");
        }

        _logger.LogInformation("群聊创建成功,ChatId: {ChatId}", createResponse.ChatId);

        // 2. 向新建的群聊发送消息
        var sendResponse = await SendMessageToGroupChatAsync(
            createResponse.ChatId, content, msgType, title, description, url, buttonText, ct: ct);

        if (!sendResponse.IsSuccessful())
        {
            _logger.LogError("向群聊 [{ChatId}] 发送消息失败: [{ErrorCode}] {ErrorMessage}",
                createResponse.ChatId, sendResponse.ErrorCode, sendResponse.ErrorMessage);
        }
        else
        {
            _logger.LogInformation("向群聊 [{ChatId}] 发送消息成功", createResponse.ChatId);
        }

        return (createResponse, sendResponse);
    }

    /// <summary>创建企业微信应用群聊</summary>
    public async Task<CgibinAppChatCreateResponse> CreateGroupChatAsync(
        List<string> userIds,
        string chatName,
        string ownerUserId,
        string? chatId = null,
        CancellationToken ct = default)
    {
        if (userIds == null || userIds.Count < 2)
            throw new ArgumentException("群聊至少需要2个成员", nameof(userIds));
        if (string.IsNullOrWhiteSpace(chatName))
            throw new ArgumentException("群聊名称不能为空", nameof(chatName));
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("群主不能为空", nameof(ownerUserId));

        var distinctUsers = userIds.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();

        if (!distinctUsers.Contains(ownerUserId))
        {
            _logger.LogWarning("群主 {Owner} 不在成员列表中,已自动添加", ownerUserId);
            distinctUsers.Add(ownerUserId);
            distinctUsers = distinctUsers.Distinct().ToList();
        }

        var response = await ExecuteWithTokenRefreshAsync(
            async token =>
            {
                var request = new CgibinAppChatCreateRequest
                {
                    AccessToken = token,
                    Name = chatName,
                    OwnerUserId = ownerUserId,
                    MemberUserIdList = distinctUsers,
                    ChatId = chatId
                };
                return await _client.ExecuteCgibinAppChatCreateAsync(request, ct);
            }, ct);

        if (!response.IsSuccessful())
        {
            _logger.LogError("创建群聊失败: [{ErrorCode}] {ErrorMessage}",
                response.ErrorCode, response.ErrorMessage);
            return response;
        }

        // 创建成功后,将群聊信息持久化到数据库
        await SaveGroupChatToDbAsync(response.ChatId, chatName, ownerUserId, distinctUsers);

        return response;
    }

    /// <summary>从数据库获取所有群聊记录</summary>
    public async Task<List<WechatWorkGroupChat>> GetAllGroupChatsAsync(CancellationToken ct = default)
    {
        return await _context.企业微信群聊
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>将群聊记录保存到数据库</summary>
    private async Task SaveGroupChatToDbAsync(
        string chatId,
        string chatName,
        string ownerUserId,
        List<string> memberUserIds)
    {
        try
        {
            // 检查是否已存在(防重复)
            var exists = await _context.企业微信群聊.AnyAsync(c => c.ChatId == chatId);
            if (exists)
            {
                _logger.LogInformation("群聊 [{ChatId}] 已在数据库中存在,跳过保存", chatId);
                return;
            }

            _context.企业微信群聊.Add(new WechatWorkGroupChat
            {
                编号 = Guid.NewGuid().ToString("N"),
                ChatId = chatId,
                Name = chatName,
                OwnerUserId = ownerUserId,
                MemberUserIds = string.Join(",", memberUserIds),
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("群聊 [{ChatId}] 已保存到数据库", chatId);
        }
        catch (Exception ex)
        {
            // 数据库保存失败不影响群聊创建结果
            _logger.LogWarning(ex, "保存群聊 [{ChatId}] 到数据库失败,但群聊已创建成功", chatId);
        }
    }

    /// <summary>向已有企业微信群聊发送消息</summary>
    public async Task<CgibinAppChatSendResponse> SendMessageToGroupChatAsync(
        string chatId,
        string content,
        WechatWorkMessageType msgType,
        string? title = null,
        string? description = null,
        string? url = null,
        string? buttonText = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            throw new ArgumentException("群聊ID不能为空", nameof(chatId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("消息内容不能为空", nameof(content));

        var response = await ExecuteWithTokenRefreshAsync(
            async token =>
            {
                var request = new CgibinAppChatSendRequest
                {
                    AccessToken = token,
                    ChatId = chatId
                };

                switch (msgType)
                {
                    case WechatWorkMessageType.Text:
                        request.MessageType = "text";
                        request.MessageContentAsText = new CgibinAppChatSendRequest.Types.TextMessage
                        {
                            Content = content
                        };
                        break;

                    case WechatWorkMessageType.Markdown:
                        request.MessageType = "markdown";
                        request.MessageContentAsMarkdown = new CgibinAppChatSendRequest.Types.MarkdownMessage
                        {
                            Content = content
                        };
                        break;

                    case WechatWorkMessageType.Card:
                        if (string.IsNullOrWhiteSpace(title))
                            throw new ArgumentException("发送卡片消息时 title 不能为空", nameof(title));
                        if (string.IsNullOrWhiteSpace(description))
                            throw new ArgumentException("发送卡片消息时 description 不能为空", nameof(description));
                        if (string.IsNullOrWhiteSpace(url))
                            throw new ArgumentException("发送卡片消息时 url 不能为空", nameof(url));
                        request.MessageType = "textcard";
                        request.MessageContentAsTextCard = new CgibinAppChatSendRequest.Types.TextCardMessage
                        {
                            Title = title,
                            Description = description,
                            Url = url,
                            ButtonText = buttonText ?? "查看详情"
                        };
                        break;

                    default:
                        throw new NotSupportedException($"群聊消息不支持的类型: {msgType}");
                }

                return await _client.ExecuteCgibinAppChatSendAsync(request, ct);
            }, ct);

        if (!response.IsSuccessful())
        {
            _logger.LogError("向群聊 [{ChatId}] 发送消息失败: [{ErrorCode}] {ErrorMessage}",
                chatId, response.ErrorCode, response.ErrorMessage);
        }

        return response;
    }

    /// <summary>向已有企业微信群聊发送文本卡片消息</summary>
    public async Task<CgibinAppChatSendResponse> SendMessageToGroupChatAsCardAsync(
        string chatId,
        string title,
        string description,
        string url,
        string buttonText = "查看详情",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            throw new ArgumentException("群聊ID不能为空", nameof(chatId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("卡片标题不能为空", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("卡片描述不能为空", nameof(description));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("跳转链接不能为空", nameof(url));

        var response = await ExecuteWithTokenRefreshAsync(
            async token =>
            {
                var request = new CgibinAppChatSendRequest
                {
                    AccessToken = token,
                    ChatId = chatId,
                    MessageType = "textcard",
                    MessageContentAsTextCard = new CgibinAppChatSendRequest.Types.TextCardMessage
                    {
                        Title = title,
                        Description = description,
                        Url = url,
                        ButtonText = buttonText
                    }
                };

                return await _client.ExecuteCgibinAppChatSendAsync(request, ct);
            }, ct);

        if (!response.IsSuccessful())
        {
            _logger.LogError("向群聊 [{ChatId}] 发送卡片消息失败: [{ErrorCode}] {ErrorMessage}",
                chatId, response.ErrorCode, response.ErrorMessage);
        }

        return response;
    }
}
