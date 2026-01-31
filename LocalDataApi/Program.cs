using LocalDataApi.Data;
using LocalDataApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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
