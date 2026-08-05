using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Reflection;

namespace LocalDataApi.Application.Common;

/// <summary>
/// EF Core 实体更新扩展:仅覆盖传入实体中非空且非默认值的标量属性,
/// 供"局部更新"用例使用(如 BLF 参数更新)。
/// </summary>
public static class EntityUpdateExtensions
{
    /// <summary>
    /// 设置实体属性值,仅处理标量属性,忽略 null 与默认值。
    /// </summary>
    public static void SetScalarValuesIgnoreNull<T>(this EntityEntry<T> entry, T source)
        where T : class
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (source == null) throw new ArgumentNullException(nameof(source));

        var sourceProperties = typeof(T).GetProperties()
            .Where(p => p.CanWrite && p.CanRead && !p.GetIndexParameters().Any());

        foreach (var property in sourceProperties)
        {
            var sourceValue = property.GetValue(source);

            // 忽略 null 值
            if (sourceValue == null)
            {
                continue;
            }

            // 跳过集合属性
            if (IsCollectionType(property.PropertyType))
            {
                continue;
            }

            // 忽略默认值
            if (IsDefaultValue(property, sourceValue))
            {
                continue;
            }

            entry.CurrentValues[property.Name] = sourceValue;
        }
    }

    private static bool IsCollectionType(Type propertyType)
    {
        // 排除字符串类型(字符串虽然实现了 IEnumerable,但不是集合属性)
        if (propertyType == typeof(string))
        {
            return false;
        }

        // 检查是否实现了 IEnumerable 接口
        return typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType);
    }

    private static bool IsDefaultValue(PropertyInfo property, object value)
    {
        var propertyType = property.PropertyType;

        // 处理可空类型
        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        if (underlyingType != null)
        {
            propertyType = underlyingType;
        }

        // 对于字符串类型,额外判断空字符串
        if (propertyType == typeof(string))
        {
            return string.IsNullOrEmpty((string)value);
        }

        // 对于值类型,获取默认值进行比较
        if (propertyType.IsValueType)
        {
            var defaultValue = Activator.CreateInstance(propertyType);
            return value.Equals(defaultValue);
        }

        return false;
    }
}
