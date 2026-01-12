using LocalDataApi.Data;
using LocalDataApi.Models;
using Microsoft.EntityFrameworkCore;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace LocalDataApi.Services
{
    public class BLFParameterService
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
                        .Include(p => p.PressureFlowRate)
                        .AsNoTracking() // 如果只是读取数据，添加这个提高性能
                        .ToListAsync();
        }

        //按编号查询
        public async Task<BLFParameter?> GetBLFParameter(string number)
        {
            var currentParam = await _context.BLFParameters
                     .Include(p => p.CurrentFlowRateCurve)
                     .Include(p => p.PressureFlowRate)
                     .AsNoTracking()
                     .FirstOrDefaultAsync(e => e.BLFNumber == number);
            return currentParam;
        }

        //创建
        public async Task CreateBLFParameter(BLFParameter blfParameter)
        {
            var isExists = await _context.BLFParameters.AnyAsync(e => e.BLFNumber == blfParameter.BLFNumber);
            if (isExists)
            {
                throw new InvalidOperationException($"比例阀编码：{blfParameter.BLFNumber} 已经存在！");
            }
            blfParameter.CreateDate = DateTime.Now;
            _context.BLFParameters.Add(blfParameter);
            await _context.SaveChangesAsync();
            return ;
        }

        //更新
        public async Task UpdateBLFParameter(BLFParameter blfParameter)
        {
            var currentParam = await _context.BLFParameters
                    .Include(p => p.CurrentFlowRateCurve)
                    .Include(p => p.PressureFlowRate).FirstOrDefaultAsync(e => e.BLFNumber == blfParameter.BLFNumber);

            if (currentParam == null)
            {
                throw new InvalidOperationException($"比例阀编码：{blfParameter.BLFNumber} 相关数据不存在！");
            }         

            //局部更新非空字段
            _context.Entry(currentParam).SetScalarValuesIgnoreNull(blfParameter);

            //嵌套集合会被完全替换
            if (blfParameter.CurrentFlowRateCurve != null && blfParameter.CurrentFlowRateCurve.Count > 0)
            {
                currentParam.CurrentFlowRateCurve = blfParameter.CurrentFlowRateCurve;
            }
            if (blfParameter.PressureFlowRate != null && blfParameter.PressureFlowRate.Count > 0)
            {

                currentParam.PressureFlowRate = blfParameter.PressureFlowRate;
            }
            currentParam.ModifyDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return;
        }

        //删除
        public async Task DeleteBLFParameter(List<string> numbers)
        {
            List<string> error = new();
            foreach (var number in numbers)
            {
                var currentParam = await _context.BLFParameters
                    .Include(p => p.CurrentFlowRateCurve)
                    .Include(p => p.PressureFlowRate).FirstOrDefaultAsync(e => e.BLFNumber == number);
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
                    if (currentParam.PressureFlowRate != null && currentParam.PressureFlowRate.Count > 0)
                    {
                        _context.PressureFlowRates.RemoveRange(currentParam.PressureFlowRate);
                    }
                    _context.BLFParameters.Remove(currentParam);
                    await _context.SaveChangesAsync();
                }
            }

            if(error.Count > 0)
            {
                throw new InvalidOperationException($"比例阀编码：{string.Join(",", error)} 相关数据不存在！");
            }   
            return;
        }
    }
}
