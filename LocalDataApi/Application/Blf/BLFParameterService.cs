using LocalDataApi.Application.Common;
using LocalDataApi.Domain.Blf;
using LocalDataApi.Dto;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Blf;

/// <summary>
/// 比例阀(BLF)参数用例:查询 / 创建 / 局部更新 / 批量删除。
/// </summary>
public class BLFParameterService : IBLFParameterService
{
    private readonly AppDbContext _context;

    public BLFParameterService(AppDbContext context)
    {
        _context = context;
    }

    // 查询所有
    public async Task<List<BLFParameter>> GetAllParameters()
    {
        return await _context.BLFParameters
            .Include(p => p.CurrentFlowRateCurve)
            .Include(p => p.PressureFlowRateCurve)
            .AsNoTracking() // 如果只是读取数据,添加这个提高性能
            .AsSplitQuery() // 拆分查询
            .ToListAsync();
    }

    // 按编号查询
    public async Task<BLFParameter?> GetBLFParameter(GetBLFParameterRequest getBLFParameter)
    {
        return await _context.BLFParameters
            .Include(p => p.CurrentFlowRateCurve)
            .Include(p => p.PressureFlowRateCurve)
            .AsNoTracking()
            .AsSplitQuery() // 拆分查询
            .FirstOrDefaultAsync(e => e.BLFNumber == getBLFParameter.Number);
    }

    // 创建
    public async Task CreateBLFParameter(BLFParameter blfParameter)
    {
        if (blfParameter == null)
        {
            throw new ValidationException("比例阀参数不能为空。");
        }

        var isExists = await _context.BLFParameters.AnyAsync(e => e.BLFNumber == blfParameter.BLFNumber);
        if (isExists)
        {
            throw new ValidationException($"比例阀编码:{blfParameter.BLFNumber} 已经存在!");
        }
        blfParameter.CreateDate = DateTime.Now;
        _context.BLFParameters.Add(blfParameter);
        await _context.SaveChangesAsync();
    }

    // 更新(局部更新非空字段;嵌套集合完全替换)
    public async Task UpdateBLFParameter(BLFParameter blfParameter)
    {
        var currentParam = await _context.BLFParameters
            .Include(p => p.CurrentFlowRateCurve)
            .Include(p => p.PressureFlowRateCurve)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.BLFNumber == blfParameter.BLFNumber);

        if (currentParam == null)
        {
            throw new NotFoundException($"比例阀编码:{blfParameter.BLFNumber} 相关数据不存在!");
        }

        // 局部更新非空字段
        _context.Entry(currentParam).SetScalarValuesIgnoreNull(blfParameter);

        // 嵌套集合会被完全替换
        if (blfParameter.CurrentFlowRateCurve != null && blfParameter.CurrentFlowRateCurve.Count > 0)
        {
            currentParam.CurrentFlowRateCurve = blfParameter.CurrentFlowRateCurve;
        }
        if (blfParameter.PressureFlowRateCurve != null && blfParameter.PressureFlowRateCurve.Count > 0)
        {
            currentParam.PressureFlowRateCurve = blfParameter.PressureFlowRateCurve;
        }
        currentParam.ModifyDate = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    // 删除:批量收集后一次提交,避免循环内逐条 SaveChanges
    public async Task DeleteBLFParameter(List<string> numbers)
    {
        if (numbers == null || numbers.Count == 0)
        {
            throw new ValidationException("删除列表不能为空。");
        }

        var missing = new List<string>();
        var toRemove = new List<BLFParameter>();

        var existing = await _context.BLFParameters
            .Include(p => p.CurrentFlowRateCurve)
            .Include(p => p.PressureFlowRateCurve)
            .AsSplitQuery()
            .Where(e => numbers.Contains(e.BLFNumber!))
            .ToListAsync();

        var existingSet = existing
            .Where(e => e.BLFNumber != null)
            .Select(e => e.BLFNumber!)
            .ToHashSet();

        foreach (var number in numbers)
        {
            if (!existingSet.Contains(number))
            {
                missing.Add(number);
                continue;
            }
        }

        foreach (var param in existing)
        {
            if (param.CurrentFlowRateCurve != null && param.CurrentFlowRateCurve.Count > 0)
            {
                _context.CurrentFlowRates.RemoveRange(param.CurrentFlowRateCurve);
            }
            if (param.PressureFlowRateCurve != null && param.PressureFlowRateCurve.Count > 0)
            {
                _context.PressureFlowRates.RemoveRange(param.PressureFlowRateCurve);
            }
            _context.BLFParameters.Remove(param);
        }

        await _context.SaveChangesAsync();

        if (missing.Count > 0)
        {
            throw new NotFoundException($"比例阀编码:{string.Join(",", missing)} 相关数据不存在!");
        }
    }
}
