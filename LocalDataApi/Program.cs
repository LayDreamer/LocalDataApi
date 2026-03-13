using LocalDataApi.Data;
using LocalDataApi.Models;
using LocalDataApi.Services;
using LocalDataApi.WeChatWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Settings;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ========== 1. 配置日志（用于调试 SDK 内部请求） ==========
builder.Logging.ClearProviders();
builder.Logging.AddConsole(); // 将日志输出到控制台

// 配置数据库连接
builder.Services.AddDbContext<AppDbContext>
    (options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
        // 在开发环境启用敏感数据日志，便于调试
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    });
builder.Services.AddScoped<BLFParameterService>();

// ========== 3. 企业微信客户端配置 ==========
// 3.1 读取配置并验证（如果值为空，可提前抛出异常或日志）
var wechatWorkSection = builder.Configuration.GetSection("WechatWork");
if (!wechatWorkSection.Exists())
{
    throw new InvalidOperationException("配置文件中缺少 WechatWork 节。");
}
var wechatWorkOptions = wechatWorkSection.Get<WechatWorkClientOptions>();
if (wechatWorkOptions == null || string.IsNullOrEmpty(wechatWorkOptions.CorpId) || wechatWorkOptions.AgentId == null || string.IsNullOrEmpty(wechatWorkOptions.AgentSecret))
{
    throw new InvalidOperationException("企业微信配置缺失或不完整，请检查 appsettings.json 中的 WechatWork 节。");
}
// 注册 IHttpClientFactory（必须！）
builder.Services.AddHttpClient();
// 3.2 注册企业微信客户端（单例，并传入日志工厂以便 SDK 输出内部日志）
builder.Services.AddSingleton(new WechatWorkClient(new WechatWorkClientOptions
{
    CorpId = wechatWorkOptions.CorpId,
    AgentId = wechatWorkOptions.AgentId,
    AgentSecret = wechatWorkOptions.AgentSecret
}));
//// 3.3 注册自定义服务
builder.Services.AddSingleton<WeChatWorkService>();

/// 配置控制器和JSON选项
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // 保持属性名不变
        options.JsonSerializerOptions.WriteIndented = true;// 美化JSON输出
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;  // 忽略空值属性
        //options.JsonSerializerOptions.IgnoreReadOnlyProperties = false;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;   // 处理循环引用
    });
builder.Services.AddOpenApi();

//Swagger 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Web API",
        Version = "v1",
        Description = "API接口文档"
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        //builder.AllowAnyOrigin()
        //       .AllowAnyMethod()
        //       .AllowAnyHeader();
        builder.WithOrigins("http://localhost:5173", "http://192.168.1.110:1001")
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials();
    });
});

var app = builder.Build();

// 在开发环境中启用Swagger中间件
if (app.Environment.IsDevelopment()||true)
{
    app.MapOpenApi();
    // Enable middleware to serve generated Swagger as a JSON endpoint
    app.UseSwagger();
    // Enable middleware to serve Swagger UI (HTML, JS, CSS, etc.)
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web API V1");
        // 设置Swagger UI页面标题
        c.DocumentTitle = "API在线文档";
        // 展开深度：None(不展开)、List(展开操作列表)、Full(展开所有)
        c.DocExpansion(DocExpansion.None);
        // 显示请求持续时间（毫秒）
        c.DisplayRequestDuration();
        c.RoutePrefix = string.Empty; // Make Swagger UI available at the root
    });
}

//app.UseCors("AllowVueApp");
app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthorization();

//// 启用静态文件服务（可选）
//app.UseDefaultFiles();
//app.UseStaticFiles();
//// 启用路由和控制器映射
//app.UseRouting();

app.MapControllers();

app.Run();
