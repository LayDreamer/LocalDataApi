using LocalDataApi.Dto;
using LocalDataApi.Domain.Ppc;

namespace LocalDataApi.Application.Ppc.Contracts;

/// <summary>
/// 排产分析用例:根据成品货号构建嵌套的排产分析树。
/// </summary>
public interface IPmcSchedulingService
{
    /// <summary>获取排产分析列表(嵌套树形结构)</summary>
    Task<List<SchedulingAnalysisDto>> GetSchedulingAnalysisList(PMCRequestDto request);
}
