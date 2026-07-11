using LocalDataApi.Data;
using LocalDataApi.Models;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Services
{
    public class ERPBaseService
    {
        private readonly AppDbContext _context;

        public ERPBaseService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 根据 userid 和 tablename 获取控制ID记录
        /// </summary>
        /// <param name="userid">用户编号</param>
        /// <param name="tablename">表名</param>
        /// <returns>匹配的控制ID记录</returns>
        public async Task<ERPId?> GetControlIdAsync(string userid, string tablename)
        {
            return await _context.tb_control_id
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.userid == userid && e.tablename == tablename);
        }

        /// <summary>
        /// 根据 userid 和 tablename 生成下一条编号
        /// 1. 查询 tb_control_id 中匹配的记录，确定 tablecode
        /// 2. 无匹配记录则返回空
        /// 3. 自增计数并补零到5位，拼接编号 userid + tablecode + currentcount
        /// </summary>
        /// <param name="userid">用户编号</param>
        /// <param name="tablename">表名</param>
        /// <param name="saveChanges">是否立即保存计数到数据库；批量场景由调用方统一提交时可传 false</param>
        /// <returns>生成的编号；无匹配记录时返回 null</returns>
        public async Task<string?> GenerateCodeAsync(string userid, string tablename, bool saveChanges = true)
        {
            var record = await _context.tb_control_id
                .FirstOrDefaultAsync(e => e.userid == userid && e.tablename == tablename);

            // 无匹配记录则返回空
            if (record == null)
            {
                return null;
            }

            // 自增计数（仅在内存中更新，待调用方统一提交）
            int newCount = (record.currentcount ?? 0) + 1;
            record.currentcount = newCount;

            // 不超过5位时补零到5位
            string paddedCount = newCount.ToString();
            if (paddedCount.Length <= 5)
            {
                paddedCount = paddedCount.PadLeft(5, '0');
            }

            // 按需保存自增后的计数；saveChanges=false 时由调用方统一提交（可与外产_BOM 同事务落库）
            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }

            // 拼接编号并去掉所有空格
            return (userid + record.tablecode + paddedCount).Replace(" ", "");
        }

        /// <summary>
        /// 预先加载控制ID记录（跟踪状态，不查询时使用 AsNoTracking）。
        /// 供批量场景在循环前调用一次，循环内复用同一条被跟踪实体，避免重复查询/保存。
        /// </summary>
        /// <param name="userid">用户编号</param>
        /// <param name="tablename">表名</param>
        /// <returns>匹配的控制ID记录（被跟踪）；无匹配返回 null</returns>
        public async Task<ERPId?> GetControlIdTrackedAsync(string userid, string tablename)
        {
            return await _context.tb_control_id
                .FirstOrDefaultAsync(e => e.userid == userid && e.tablename == tablename);
        }

        /// <summary>
        /// 基于已加载（被跟踪）的控制ID记录生成下一条编号（仅在内存自增，不查询、不保存）。
        /// 需在循环前通过 GetControlIdTrackedAsync 预加载记录，并在末尾由调用方统一 SaveChangesAsync 提交。
        /// </summary>
        /// <param name="record">已加载的控制ID记录（被跟踪）</param>
        /// <param name="userid">用户编号</param>
        /// <returns>生成的编号；record 为 null 时返回 null</returns>
        public string? GenerateCodeFromRecord(ERPId? record, string userid)
        {
            if (record == null)
            {
                return null;
            }

            // 自增计数（仅内存，依赖 record 处于被跟踪状态，末尾统一提交）
            int newCount = (record.currentcount ?? 0) + 1;
            record.currentcount = newCount;

            // 不超过5位时补零到5位
            string paddedCount = newCount.ToString();
            if (paddedCount.Length <= 5)
            {
                paddedCount = paddedCount.PadLeft(5, '0');
            }
            // 拼接编号并去掉所有空格
            string result=string.Concat(userid, record.tablecode, paddedCount).Replace(" ", "");
            return result;
        }

        /// <summary>
        /// 根据工单编号计算工单工号
        /// 规则：USR 替换成 10，然后只保留数字
        /// </summary>
        /// <param name="code">工单编号</param>
        /// <returns>处理后的工单工号；编号为空时返回空字符串</returns>
        public string CalculateWorkOrder(string? code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return string.Empty;
            }
            // 按照规则处理：USR替换成10，然后去掉中间的字母
            string workOrder = code.ToUpper().Replace("USR", "10");
            // 只保留数字
            return new string(workOrder.Where(char.IsDigit).ToArray());
        }
    }
}
