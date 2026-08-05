using LocalDataApi.Domain.Erp;
using LocalDataApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LocalDataApi.Application.Erp;

/// <summary>
/// ERP 基础用例:控制ID取号、工单工号计算、ERP 用户校验。
/// </summary>
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
    public async Task<ERPId?> GetControlIdAsync(string userid, string tablename)
    {
        return await _context.tb_control_id
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.userid == userid && e.tablename == tablename);
    }

    /// <summary>
    /// 根据 userid 和 tablename 生成下一条编号
    /// </summary>
    public async Task<string?> GenerateCodeAsync(string userid, string tablename, bool saveChanges = true)
    {
        var record = await _context.tb_control_id
            .FirstOrDefaultAsync(e => e.userid == userid && e.tablename == tablename);

        if (record == null)
        {
            return null;
        }

        int newCount = (record.currentcount ?? 0) + 1;
        record.currentcount = newCount;

        string paddedCount = newCount.ToString();
        if (paddedCount.Length <= 5)
        {
            paddedCount = paddedCount.PadLeft(5, '0');
        }

        if (saveChanges)
        {
            await _context.SaveChangesAsync();
        }

        return (userid + record.tablecode + paddedCount).Replace(" ", "");
    }

    /// <summary>
    /// 预先加载控制ID记录(跟踪状态),供批量场景在循环前调用一次。
    /// </summary>
    public async Task<ERPId?> GetControlIdTrackedAsync(string userid, string tablename)
    {
        return await _context.tb_control_id
            .FirstOrDefaultAsync(e => e.userid == userid && e.tablename == tablename);
    }

    /// <summary>
    /// 基于已加载(被跟踪)的控制ID记录生成下一条编号(仅内存自增)。
    /// </summary>
    public string? GenerateCodeFromRecord(ERPId? record, string userid)
    {
        if (record == null)
        {
            return null;
        }

        int newCount = (record.currentcount ?? 0) + 1;
        record.currentcount = newCount;

        string paddedCount = newCount.ToString();
        if (paddedCount.Length <= 5)
        {
            paddedCount = paddedCount.PadLeft(5, '0');
        }
        string result = string.Concat(userid, record.tablecode, paddedCount).Replace(" ", "");
        return result;
    }

    /// <summary>
    /// 根据工单编号计算工单工号(USR 替换成 10,然后只保留数字)
    /// </summary>
    public string CalculateWorkOrder(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }
        string workOrder = code.ToUpper().Replace("USR", "10");
        return new string(workOrder.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// 获取 tb_control_user 表中所有用户的 username 列表。
    /// </summary>
    public async Task<List<string>> GetAllUsersAsync()
    {
        return await _context.tb_control_user
            .AsNoTracking()
            .Select(u => u.username!)
            .ToListAsync();
    }

    /// <summary>
    /// 校验 ERP 用户(tb_control_user):用户名不存在返回"用户名错误",密码不匹配返回"密码错误"。
    /// </summary>
    public async Task<(bool Success, string Message, ERPUser? User)> ValidateUserAsync(string username, string upwd)
    {
        var userNameTrim = (username ?? string.Empty).Trim();
        var upwdTrim = (upwd ?? string.Empty).Trim();

        var user = await _context.tb_control_user
            .AsNoTracking()
            .FirstOrDefaultAsync(u => (u.username ?? string.Empty).Trim() == userNameTrim);

        if (user == null)
            return (false, "用户名错误", null);

        if ((user.upwd ?? string.Empty).Trim() != upwdTrim)
            return (false, "密码错误", null);

        return (true, "校验成功", user);
    }
}
