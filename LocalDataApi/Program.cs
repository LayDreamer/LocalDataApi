using LocalDataApi.Data;
using LocalDataApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 配置数据库连接
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.AddSingleton<UserService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BLFParameterService>();

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//json序列化
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // 保持属性名不变
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.IgnoreReadOnlyProperties = false;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

//Swagger 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Web API",
        Version = "v1",
        Description = "An API to manage users"
    });
    // 示例：如需配置 SchemaGeneratorOptions，可直接访问属性
    // 例如：c.SchemaGeneratorOptions.UseAllOfForInheritance = true;
    // 如果不需要配置，可以直接删除此行
});

var app = builder.Build();

// 在开发环境中启用Swagger中间件
if (app.Environment.IsDevelopment() || true)
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



app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
