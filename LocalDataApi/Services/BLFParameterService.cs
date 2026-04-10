using LocalDataApi.Data;
using LocalDataApi.Dto;
using LocalDataApi.Exceptions;
using LocalDataApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
namespace LocalDataApi.Services
{
    public class BLFParameterService : IBLFParameterService
    {
        private readonly AppDbContext _context;
        public BLFParameterService(AppDbContext context)
        {
            _context = context;
        }

        //查询所有
        public async Task<List<BLFParameter>> GetAllParameters()
        {
            return await _context.BLFParameters
                        .Include(p => p.CurrentFlowRateCurve)
                        .Include(p => p.PressureFlowRateCurve)
                        .AsNoTracking() // 如果只是读取数据，添加这个提高性能
                        .AsSplitQuery() // 拆分查询
                        .ToListAsync();
        }

        //按编号查询
        public async Task<BLFParameter?> GetBLFParameter(GetBLFParameterRequest getBLFParameter)
        {
            var currentParam = await _context.BLFParameters
                     .Include(p => p.CurrentFlowRateCurve)
                     .Include(p => p.PressureFlowRateCurve)
                     .AsNoTracking()
                     .AsSplitQuery() // 拆分查询
                     .FirstOrDefaultAsync(e => e.BLFNumber == getBLFParameter.Number);
            return currentParam;
        }

        //创建
        public async Task CreateBLFParameter(BLFParameter blfParameter)
        {
            if (blfParameter == null)
            {
                throw new ValidationException("比例阀参数不能为空。");
            }

            var isExists = await _context.BLFParameters.AnyAsync(e => e.BLFNumber == blfParameter.BLFNumber);
            if (isExists)
            {
                throw new ValidationException($"比例阀编码：{blfParameter.BLFNumber} 已经存在！");
            }
            blfParameter.CreateDate = DateTime.Now;
            _context.BLFParameters.Add(blfParameter);
            await _context.SaveChangesAsync();
            return;
        }

        //更新
        public async Task UpdateBLFParameter(BLFParameter blfParameter)
        {
            var currentParam = await _context.BLFParameters
                    .Include(p => p.CurrentFlowRateCurve)
                    .Include(p => p.PressureFlowRateCurve)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(e => e.BLFNumber == blfParameter.BLFNumber);

            if (currentParam == null)
            {
                throw new NotFoundException($"比例阀编码：{blfParameter.BLFNumber} 相关数据不存在！");
            }
            
            //局部更新非空字段
            _context.Entry(currentParam).SetScalarValuesIgnoreNull(blfParameter);

            //嵌套集合会被完全替换
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
            return;
        }

        //删除
        public async Task DeleteBLFParameter(List<string> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ValidationException("删除列表不能为空。");
            }

            List<string> error = new();
            foreach (var number in numbers)
            {
                var currentParam = await _context.BLFParameters
                    .Include(p => p.CurrentFlowRateCurve)
                    .Include(p => p.PressureFlowRateCurve)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(e => e.BLFNumber == number);
                if (currentParam == null)
                {
                    error.Add(number);
                }
                else
                {
                    if (currentParam.CurrentFlowRateCurve != null && currentParam.CurrentFlowRateCurve.Count > 0)
                    {
                        _context.CurrentFlowRates.RemoveRange(currentParam.CurrentFlowRateCurve);
                    }
                    if (currentParam.PressureFlowRateCurve != null && currentParam.PressureFlowRateCurve.Count > 0)
                    {
                        _context.PressureFlowRates.RemoveRange(currentParam.PressureFlowRateCurve);
                    }
                    _context.BLFParameters.Remove(currentParam);
                    await _context.SaveChangesAsync();
                }
            }

            if (error.Count > 0)
            {
                throw new NotFoundException($"比例阀编码：{string.Join(",", error)} 相关数据不存在！");
            }
            return;
        }
    }
}
