using LocalDataApi.Api.Attributes;
using LocalDataApi.Api.Filters;
using LocalDataApi.Api.Middlewares;
using LocalDataApi.Application.Blf;
using LocalDataApi.Application.Audit;
using LocalDataApi.Application.Common;
using LocalDataApi.Application.Erp;
using LocalDataApi.Application.Identity;
using LocalDataApi.Application.Pmc.Contracts;
using LocalDataApi.Application.Pmc.Services;
using LocalDataApi.Application.WeChatWork;
using LocalDataApi.Infrastructure.Data;
using LocalDataApi.Infrastructure.WeChatWork;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SKIT.FlurlHttpClient.Wechat.Work;
using SKIT.FlurlHttpClient.Wechat.Work.Settings;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ========== 1. 配置日志 ==========
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ========== 2. 配置数据库(动态探测 SQL Server 版本,设置批量插入大小) ==========
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "缺少数据库连接字符串,请配置环境变量 ConnectionStrings__DefaultConnection。");
}
int maxBatchSize = 1; // 默认保守:低版本单条插入

try
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS INT)";
    var versionObj = command.ExecuteScalar();
    if (versionObj != null && Convert.ToInt32(versionObj) >= 10) // SQL Server 2008+ 支持多行 VALUES (...), (...)
    {
        maxBatchSize = 1000;
    }
}
catch
{
    maxBatchSize = 1;
}

builder.Services.AddSingleton<DataChangeLogInterceptor>();
builder.Services.AddDbContextPool<AppDbContext>
    ((serviceProvider, options) =>
    {
        options.AddInterceptors(serviceProvider.GetRequiredService<DataChangeLogInterceptor>());
        options.UseSqlServer(connectionString,
            sqlOptions =>
            {
                sqlOptions.MaxBatchSize(maxBatchSize);
                sqlOptions.UseCompatibilityLevel(100);
                // 将内存集合翻译为 IN 常量列表而非 OPENJSON,兼容低版本 SQL Server(同时避免手写分批)
                sqlOptions.TranslateParameterizedCollectionsToConstants();
                // 启用连接弹性(连接失败自动重试)
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(30);
            });
        // 在开发环境启用敏感数据日志,便于调试
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
        }
    }, poolSize: 256);
builder.Services.AddMemoryCache();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

// ========== 3. 数据库密集型接口限流 ==========
var databaseConcurrency = builder.Configuration.GetValue("Performance:DatabaseConcurrency", 64);
var databaseQueue = builder.Configuration.GetValue("Performance:DatabaseQueue", 256);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "1";
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiResponse<object>
        {
            Success = false,
            Message = "系统繁忙,请稍后重试。",
            Data = new { TraceId = context.HttpContext.TraceIdentifier }
        }, cancellationToken);
    };
    options.AddConcurrencyLimiter("DatabaseHeavy", limiter =>
    {
        limiter.PermitLimit = databaseConcurrency;
        limiter.QueueLimit = databaseQueue;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// ========== 4. 应用层服务注册(按业务模块) ==========
builder.Services.AddScoped<IBLFParameterService, BLFParameterService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ERPBaseService>();

// Identity / RBAC 权限中心(2026-08-08)
builder.Services.AddSingleton<PermissionCache>();
builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ILoginLogService, LoginLogService>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddScoped<IEmployeeAccountService, EmployeeAccountService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<RbacSeeder>();
builder.Services.AddHttpContextAccessor();

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var authSecret = string.IsNullOrWhiteSpace(authOptions.Secret)
    ? "LocalDataApi-Default-Dev-Secret-Change-Me"
    : authOptions.Secret;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authSecret)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionIdText = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sid);
                if (!Guid.TryParse(sessionIdText, out var sessionId) || string.IsNullOrWhiteSpace(userId))
                {
                    context.Fail("AUTH_SESSION_REVOKED");
                    return;
                }

                var sessions = context.HttpContext.RequestServices.GetRequiredService<IAuthSessionService>();
                var validation = await sessions.ValidateAccessAsync(userId, sessionId, context.HttpContext.RequestAborted);
                if (!validation.IsValid)
                    context.Fail(validation.ErrorCode ?? "AUTH_SESSION_REVOKED");
            },
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                    context.HttpContext.Items["AuthErrorCode"] = "AUTH_ACCESS_EXPIRED";
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var code = context.HttpContext.Items["AuthErrorCode"]?.ToString()
                    ?? context.AuthenticateFailure?.Message
                    ?? "AUTH_SESSION_REVOKED";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ApiResponse<object>
                {
                    Success = false,
                    Message = code == "AUTH_ACCESS_EXPIRED" ? "访问令牌已过期" : "未登录或登录已失效",
                    Data = new { code }
                });
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// PMC 域
builder.Services.AddScoped<IPmcProductService, PmcProductService>();
builder.Services.AddScoped<IPmcDeliveryReviewService, PmcDeliveryReviewService>();
builder.Services.AddScoped<IPmcBomService, PmcBomService>();
builder.Services.AddScoped<IPmcSchedulingService, PmcSchedulingService>();
builder.Services.AddScoped<IPmcWorkOrderService, PmcWorkOrderService>();
builder.Services.AddScoped<IPmcExternalProductionService, PmcExternalProductionService>();

// ========== 5. 企业微信客户端与基础设施注册 ==========
// 5.1 读取配置并验证
var wechatWorkSection = builder.Configuration.GetSection("WechatWork");
if (!wechatWorkSection.Exists())
{
    throw new InvalidOperationException("配置文件中缺少 WechatWork 节。");
}
var wechatWorkOptions = wechatWorkSection.Get<WechatWorkClientOptions>();
if (wechatWorkOptions == null || string.IsNullOrEmpty(wechatWorkOptions.CorpId) || wechatWorkOptions.AgentId == null || string.IsNullOrEmpty(wechatWorkOptions.AgentSecret))
{
    throw new InvalidOperationException("企业微信配置缺失或不完整,请检查 appsettings.json 中的 WechatWork 节。");
}
// 注册 IHttpClientFactory(必须!)
builder.Services.AddHttpClient();
// 命名客户端:供 TokenProvider 拉取 jsapi_ticket 复用连接池
builder.Services.AddHttpClient("WechatWork", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
// 5.2 注册企业微信客户端(单例,并传入日志工厂以便 SDK 输出内部日志)
builder.Services.AddSingleton(new WechatWorkClient(new WechatWorkClientOptions
{
    CorpId = wechatWorkOptions.CorpId,
    AgentId = wechatWorkOptions.AgentId,
    AgentSecret = wechatWorkOptions.AgentSecret
}));
// 5.3 注册 Token 提供者(单例,线程安全;内部使用 IHttpClientFactory)
builder.Services.AddSingleton<WechatWorkTokenProvider>();
// 5.4 注册企业微信应用服务(按子域拆分)
builder.Services.AddScoped<WeChatWorkOrganizationService>();
builder.Services.AddScoped<WeChatWorkMessageService>();
builder.Services.AddScoped<WeChatWorkSmartSheetService>();
builder.Services.AddScoped<WeChatWorkGroupChatService>();
builder.Services.AddScoped<WeChatWorkJsSdkService>();
builder.Services.AddScoped<IWechatWorkUserService, WechatWorkUserService>();

// ========== 6. 控制器与 JSON 序列化 ==========
builder.Services.AddControllers()
    .AddMvcOptions(options => options.Filters.Add<OperationLogFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // 保持属性名不变(前端契约依赖)
        options.JsonSerializerOptions.WriteIndented = true; // 美化JSON输出
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull; // 忽略空值属性
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; // 处理循环引用
    });
builder.Services.AddOpenApi();

// ========== 7. Swagger(仅开发环境暴露) ==========
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Web API",
        Version = "v1",
        Description = "API接口文档"
    });

    // 加载 XML 注释文件,使 Swagger UI 显示控制器/接口的中文说明
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// ========== 8. CORS ==========
// 允许的来源从配置读取(默认含开发期 5173 与内网地址);生产环境请在 Cors:AllowedOrigins 中加入前端真实域名
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:5173", "http://localhost:1001", "http://192.168.1.18:1001" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(corsOrigins)
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials();
    });
});

var app = builder.Build();

// ========== 8.5 RBAC 初始化数据(启动时执行,幂等;失败仅警告不阻断启动) ==========
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<LocalDataApi.Application.Identity.RbacSeeder>();
    await seeder.SeedAsync();
}

// ========== 9. 中间件管道 ==========
// 仅开发环境暴露 Swagger 文档
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web API V1");
        c.DocumentTitle = "API在线文档";
        c.DocExpansion(DocExpansion.None);
        c.DisplayRequestDuration();
        c.RoutePrefix = string.Empty; // Swagger UI 在根路径可用
    });
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseRouting();

// 请求入口日志：记录所有到达后端的 HTTP 请求，便于排查前端是否真正发请求及请求内容
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var origin = context.Request.Headers.Origin.ToString();
    logger.LogInformation(
        "[REQ] {Method} {Path}{Query} | RemoteIp={RemoteIp} | Origin={Origin}",
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString,
        context.Connection.RemoteIpAddress,
        string.IsNullOrEmpty(origin) ? "(none)" : origin);

    await next();

    logger.LogInformation(
        "[RES] {Method} {Path}{Query} | Status={StatusCode}",
        context.Request.Method,
        context.Request.Path,
        context.Request.QueryString,
        context.Response.StatusCode);
});

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ========== 10. Controller 权限覆盖检查(启动扫描,仅输出警告,不阻断启动) ==========
// 目的:发现新增接口未声明权限控制(HasPermission),避免接口意外暴露。
// 注意:PMC/ERP/WeChatWork 业务接口处于"权限未接入阶段"(Task-012/013 未执行),会集中告警,属预期。
using (var scanScope = app.Services.CreateScope())
{
    var scanLogger = scanScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    ScanControllersForMissingPermissionAttributes(scanLogger);
}

static void ScanControllersForMissingPermissionAttributes(ILogger logger)
{
    var assembly = typeof(Program).Assembly;
    var controllerTypes = assembly.GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic)
        .ToList();

    var missing = 0;
    var missingList = new List<string>();
    foreach (var controller in controllerTypes)
    {
        var controllerProtected = controller.GetCustomAttributes<HasPermissionAttribute>(true).Any();
        var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>(true).Any());

        foreach (var action in actions)
        {
            var isProtected = controllerProtected || action.GetCustomAttributes<HasPermissionAttribute>(true).Any();
            if (!isProtected)
            {
                missing++;
                missingList.Add($"{controller.Name}.{action.Name}");
            }
        }
    }

    if (missing > 0)
    {
        logger.LogWarning(
            "[RBAC] 权限覆盖检查: {Missing}/{Total} 个接口缺少权限声明(含业务接口未接入阶段,属预期;新增接口请注意补加 [HasPermission])\n{List}",
            missing, controllerTypes.Sum(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance).Count(m => !m.IsSpecialName && m.GetCustomAttributes<HttpMethodAttribute>(true).Any())),
            string.Join("\n", missingList.Take(30)));
    }
    else
    {
        logger.LogInformation("[RBAC] 权限覆盖检查: 全部接口均已声明权限。");
    }
}

app.Run();
