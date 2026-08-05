using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;

namespace LocalDataApi.Application.Ppc.Contracts;

/// <summary>
/// 外产 BOM 用例:按成品货号生成并保存 BOM 结构、BOM 查询删除、BOM 结构工序。
/// </summary>
public interface IPmcBomService
{
    /// <summary>根据成品货号生成并保存外产BOM结构(事务 + 原子取号锁)</summary>
    Task<List<ExternalProductionBOM>> SaveExternalProductionBOM(List<ExternalProductionBOM> bomList, string username, string schedulingNo);

    /// <summary>获取外产BOM列表(分页)</summary>
    Task<PagedResult<ExternalProductionBOM>> GetExternalProductionBOMList(PMCRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>批量删除外产BOM数据</summary>
    Task DeleteExternalProductionBOMList(List<string> ids);

    /// <summary>获取所有BOM结构工序数据(带缓存)</summary>
    Task<List<BOMStructureProcess>> GetBOMStructureProcessList();

    /// <summary>根据成品货号获取外产 BOM 扁平记录(用于排产分析树构建)</summary>
    Task<List<ExternalProductionBOM>> GetBomByItemNo(string? itemNo);
}
