namespace LocalDataApi.Api.Attributes;

/// <summary>标记不应生成操作日志的控制器或操作。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class NoOperationLogAttribute : Attribute;
