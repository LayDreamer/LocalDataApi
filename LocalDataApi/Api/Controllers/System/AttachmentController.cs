using LocalDataApi.Api.Attributes;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Platform;
using LocalDataApi.Dto;
using Microsoft.AspNetCore.Mvc;

namespace LocalDataApi.Api.Controllers.System;

/// <summary>
/// 统一附件中心(管理侧,挂 Platform.Attachment.* 权限)。
/// 边界(WP03 修订冻结版): 公共 Upload API 仅创建 SourceType=0 本地附件,不接受 SourceType/ExternalUrl 参数(防 SSRF/开放重定向);
/// SourceType=1 仅由受信任后端 Adapter/内部 Service 通过 IAttachmentService.CreateExternalAsync 创建。
/// 业务单据内嵌附件场景由业务 Service 在业务权限校验后调用 AttachmentService,不暴露本 Controller。
/// </summary>
[ApiController]
[Route("api/system/attachment")]
public sealed class AttachmentController(IAttachmentService attachmentService) : ControllerBase
{
    /// <summary>上传附件(multipart/form-data: businessType + businessId + file;单文件)。</summary>
    [HttpPost("upload")]
    [HasPermission(PermissionCodes.PlatformAttachmentUpload)]
    public async Task<ActionResult<ApiResponse<AttachmentDto>>> Upload(
        [FromForm] string businessType,
        [FromForm] string businessId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("请选择要上传的文件");

        await using var stream = file.OpenReadStream();
        var dto = await attachmentService.UploadAsync(businessType, businessId, file.FileName, file.ContentType, stream, ct);
        return Ok(new ApiResponse<AttachmentDto>
        {
            Success = true,
            Message = "上传成功",
            Data = dto
        });
    }

    /// <summary>查询业务对象附件列表。</summary>
    [HttpGet("list")]
    [HasPermission(PermissionCodes.PlatformAttachmentView)]
    public async Task<ActionResult<ApiResponse<List<AttachmentDto>>>> GetList(
        [FromQuery] string businessType,
        [FromQuery] string businessId,
        CancellationToken ct)
    {
        var list = await attachmentService.GetByBusinessAsync(businessType, businessId, ct);
        return Ok(new ApiResponse<List<AttachmentDto>>
        {
            Success = true,
            Data = list
        });
    }

    /// <summary>下载/预览: 本地附件返回文件流(Content-Disposition 按类型 inline/attachment),外部引用 302 至受控 ExternalUrl。</summary>
    [HttpGet("{id:long}/download")]
    [HasPermission(PermissionCodes.PlatformAttachmentView)]
    public async Task<IActionResult> Download(long id, CancellationToken ct)
    {
        var result = await attachmentService.OpenForDownloadAsync(id, ct);
        if (result.IsExternal)
            return Redirect(result.ExternalUrl!);

        var attachment = result.Attachment;
        var extension = attachment.Extension?.ToLowerInvariant();
        var inline = extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".pdf";
        return File(result.Stream!, attachment.ContentType, attachment.FileName, inline);
    }

    /// <summary>删除附件(记录 + 尽力删除物理文件)。</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(PermissionCodes.PlatformAttachmentDelete)]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(long id, CancellationToken ct)
    {
        await attachmentService.DeleteAsync(id, ct);
        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = "删除成功",
            Data = true
        });
    }
}
