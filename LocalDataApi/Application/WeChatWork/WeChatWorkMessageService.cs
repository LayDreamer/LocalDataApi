using LocalDataApi.Infrastructure.WeChatWork;
using LocalDataApi.WeChatWork;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Models;

namespace LocalDataApi.Application.WeChatWork;

/// <summary>
/// 企业微信消息用例:向成员发送文本 / Markdown / 文本卡片消息。
/// </summary>
public class WeChatWorkMessageService : WechatWorkServiceBase
{
    public WeChatWorkMessageService(
        WechatWorkClient client,
        WechatWorkTokenProvider tokenProvider,
        ILogger<WeChatWorkMessageService> logger)
        : base(client, tokenProvider, logger)
    {
    }

    /// <summary>
    /// 发送消息接口(支持文本、markdown等多种消息类型,接收者可以是成员/部门/标签)
    /// </summary>
    public async Task<CgibinMessageSendResponse> SendMessageAsync(
        List<string> users,
        string content,
        WechatWorkMessageType msgType,
        bool isSafe = false,
        CancellationToken ct = default)
    {
        if (users == null || users.Count == 0 || users.All(string.IsNullOrWhiteSpace))
            throw new ArgumentException("接收者列表不能为空", nameof(users));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("消息内容不能为空", nameof(content));

        users = users.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        var request = new CgibinMessageSendRequest
        {
            AccessToken = accessToken,
            ToUserIdList = users,
            ToDepartmentIdList = null,
            ToTagIdList = null,
            IsSafe = isSafe,
        };

        switch (msgType)
        {
            case WechatWorkMessageType.Text:
                request.MessageType = "text";
                request.MessageContentAsText = new CgibinMessageSendRequest.Types.TextMessage
                {
                    Content = content
                };
                break;

            case WechatWorkMessageType.Markdown:
                request.MessageType = "markdown";
                request.MessageContentAsMarkdown = new CgibinMessageSendRequest.Types.MarkdownMessage
                {
                    Content = content
                };
                break;

            default:
                throw new NotSupportedException($"不支持的消息类型: {msgType}");
        }

        var response = await ExecuteWithTokenRefreshAsync(
            async token =>
            {
                request.AccessToken = token;
                return await _client.ExecuteCgibinMessageSendAsync(request, ct);
            }, ct);

        if (response.IsSuccessful())
        {
            _logger.LogInformation("消息发送成功,MsgId: {MsgId}", response.MessageId);
        }
        else
        {
            _logger.LogError("发送消息失败: {ErrorCode} - {ErrorMessage}", response.ErrorCode, response.ErrorMessage);
        }

        return response;
    }

    /// <summary>发送文本卡片消息(适合发送智能表格链接等场景)</summary>
    public async Task<CgibinMessageSendResponse> SendMessageAsCardAsync(
        List<string> users,
        string title,
        string description,
        string url,
        string buttonText = "查看详情",
        CancellationToken ct = default)
    {
        if (users == null || users.Count == 0 || users.All(string.IsNullOrWhiteSpace))
            throw new ArgumentException("接收者列表不能为空", nameof(users));

        users = users.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        var accessToken = await _tokenProvider.GetAccessTokenAsync(ct);

        var request = new CgibinMessageSendRequest
        {
            AccessToken = accessToken,
            ToUserIdList = users,
            MessageType = "textcard",
            ToDepartmentIdList = null,
            ToTagIdList = null,
            IsSafe = false,
            MessageContentAsTextCard = new CgibinMessageSendRequest.Types.TextCardMessage
            {
                Title = title,
                Description = description,
                Url = url,
                ButtonText = buttonText
            },
        };

        var response = await ExecuteWithTokenRefreshAsync(
            async token =>
            {
                request.AccessToken = token;
                return await _client.ExecuteCgibinMessageSendAsync(request, ct);
            }, ct);

        if (response.IsSuccessful())
        {
            _logger.LogInformation("消息发送成功,MsgId: {MsgId}", response.MessageId);
        }
        else
        {
            _logger.LogError("发送消息失败: {ErrorCode} - {ErrorMessage}", response.ErrorCode, response.ErrorMessage);
        }

        return response;
    }
}
