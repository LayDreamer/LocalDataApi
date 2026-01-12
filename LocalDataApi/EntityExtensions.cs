using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections;
using System.Reflection;

namespace LocalDataApi
    {
    public static class EntityExtensions
    {
        /// <summary>
        /// 设置实体属性值，忽略 null 值和默认值，包括处理集合属性
        /// </summary>
        public static void SetValuesIgnoreNullWithCollections<T>(this EntityEntry<T> entry, T source)
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

                // 判断是否为集合属性
                if (IsCollectionType(property.PropertyType))
                {
                    // 对于集合属性，使用特殊处理逻辑
                    HandleCollectionProperty(entry, property, sourceValue);
                    continue;
                }

                // 忽略默认值
                if (IsDefaultValue(property, sourceValue))
                {
                    continue;
                }

                // 设置标量属性值
                entry.CurrentValues[property.Name] = sourceValue;
            }
        }

        /// <summary>
        /// 判断属性类型是否为集合类型
        /// </summary>
        private static bool IsCollectionType(Type propertyType)
        {
            // 排除字符串类型（字符串虽然实现了IEnumerable，但不是集合属性）
            if (propertyType == typeof(string))
            {
                return false;
            }

            // 检查是否实现了 IEnumerable 接口
            if (!typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                return false;
            }

            // 排除非集合类型（如单值导航属性）
            // 集合类型通常是 IList、ICollection、IEnumerable 等的具体实现
            // 或者是用 [NotMapped] 标记的属性（这些属性不应该被处理）
            return true;
        }

        /// <summary>
        /// 处理集合属性
        /// </summary>
        private static void HandleCollectionProperty<T>(EntityEntry<T> entry, PropertyInfo property, object sourceValue)
            where T : class
        {
            // 获取集合实例
            var collection = sourceValue as IEnumerable;
            if (collection == null)
            {
                return;
            }

            // 计算集合中的元素数量
            int count = 0;
            foreach (var item in collection)
            {
                count++;
            }

            // 如果集合为空，可以选择忽略或设置为空集合
            // 这里我们选择忽略空集合，因为通常没有实际意义
            if (count == 0)
            {
                return;
            }

            // 对于集合属性，我们不直接设置到 CurrentValues
            // 因为 EF Core 的集合属性需要通过导航属性处理
            // 这里提供一个日志记录或后续处理的扩展点
            LogCollectionProperty(entry, property, count);
        }

        /// <summary>
        /// 记录集合属性信息（用于调试和扩展处理）
        /// </summary>
        private static void LogCollectionProperty<T>(EntityEntry<T> entry, PropertyInfo property, int itemCount)
            where T : class
        {
            // 可以在这里添加日志记录逻辑
            // 或者触发事件通知调用方有集合属性需要特殊处理
            // 例如：
            // _logger.LogDebug("实体 {EntityType} 的集合属性 {PropertyName} 包含 {Count} 个元素",
            //     typeof(T).Name, property.Name, itemCount);
        }

        /// <summary>
        /// 设置实体属性值，仅处理标量属性，忽略集合属性
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

        /// <summary>
        /// 分别处理标量属性和集合属性
        /// </summary>
        public static void SetValuesWithCollections<T>(
            this EntityEntry<T> entry,
            T source,
            Action<EntityEntry<T>, PropertyInfo, IEnumerable> collectionHandler = null)
            where T : class
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (source == null) throw new ArgumentNullException(nameof(source));

            var sourceProperties = typeof(T).GetProperties()
                .Where(p => p.CanWrite && p.CanRead && !p.GetIndexParameters().Any());

            foreach (var property in sourceProperties)
            {
                var sourceValue = property.GetValue(source);

                if (sourceValue == null)
                {
                    continue;
                }

                // 处理集合属性
                if (IsCollectionType(property.PropertyType))
                {
                    var collection = sourceValue as IEnumerable;
                    if (collection != null && collectionHandler != null)
                    {
                        collectionHandler(entry, property, collection);
                    }
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

        /// <summary>
        /// 判断属性值是否为默认值
        /// </summary>
        private static bool IsDefaultValue(PropertyInfo property, object value)
        {
            var propertyType = property.PropertyType;

            // 处理可空类型
            var underlyingType = Nullable.GetUnderlyingType(propertyType);
            if (underlyingType != null)
            {
                propertyType = underlyingType;
            }

            // 对于字符串类型，额外判断空字符串
            if (propertyType == typeof(string))
            {
                return string.IsNullOrEmpty((string)value);
            }

            // 对于值类型，获取默认值进行比较
            if (propertyType.IsValueType)
            {
                var defaultValue = Activator.CreateInstance(propertyType);
                return value.Equals(defaultValue);
            }

            // 引用类型已经在外部处理了 null，这里不需要额外处理
            return false;
        }

        /// <summary>
        /// 判断值是否为类型的默认值（泛型版本）
        /// </summary>
        public static bool IsDefaultValue<T>(T value)
        {
            // 引用类型
            if (!typeof(T).IsValueType)
            {
                return value == null;
            }

            // 值类型使用比较器
            return EqualityComparer<T>.Default.Equals(value, default(T));
        }

        /// <summary>
        /// 安全地获取集合的元素数量
        /// </summary>
        public static int GetCollectionCount<T>(this IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return 0;
            }

            // 对于 ICollection<T>，使用 Count 属性更高效
            if (collection is ICollection<T> genericCollection)
            {
                return genericCollection.Count;
            }

            // 对于普通 IEnumerable，需要枚举
            int count = 0;
            using (var enumerator = collection.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 检查集合是否为空（没有元素）
        /// </summary>
        public static bool IsEmptyCollection<T>(this IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return true;
            }

            // 对于实现了 ICollection<T> 的集合，使用 Count 属性
            if (collection is ICollection<T> genericCollection)
            {
                return genericCollection.Count == 0;
            }

            // 对于普通 IEnumerable，需要检查是否有元素
            return !collection.GetEnumerator().MoveNext();
        }
    }
    //public static class EntityExtensions
    //{
    //    public static void SetValuesIgnoreNull<T>(this EntityEntry<T> entry, T source)
    //        where T : class
    //    {
    //        var sourceProperties = typeof(T).GetProperties()
    //            .Where(p => p.CanWrite && p.CanRead && !p.GetIndexParameters().Any());

    //        foreach (var property in sourceProperties)
    //        {
    //            var sourceValue = property.GetValue(source);

    //            // 忽略 null 值
    //            if (sourceValue == null)
    //            {
    //                continue;
    //            }

    //            // 忽略默认值
    //            if (IsDefaultValue(property, sourceValue))
    //            {
    //                continue;
    //            }

    //            entry.CurrentValues[property.Name] = sourceValue;

    //        }
    //    }
    //    /// <summary>
    //    /// 判断属性值是否为默认值
    //    /// </summary>
    //    private static bool IsDefaultValue(PropertyInfo property, object value)
    //    {
    //        var propertyType = property.PropertyType;

    //        // 处理可空类型
    //        var underlyingType = Nullable.GetUnderlyingType(propertyType);
    //        if (underlyingType != null)
    //        {
    //            propertyType = underlyingType;
    //        }

    //        // 对于字符串类型，额外判断空字符串
    //        if (propertyType == typeof(string))
    //        {
    //            return string.IsNullOrEmpty((string)value);
    //        }

    //        // 对于值类型，获取默认值进行比较
    //        if (propertyType.IsValueType)
    //        {
    //            var defaultValue = Activator.CreateInstance(propertyType);
    //            return value.Equals(defaultValue);
    //        }

    //        // 引用类型已经在外部处理了 null，这里不需要额外处理
    //        return false;
    //    }

    //    /// <summary>
    //    /// 判断值是否为类型的默认值
    //    /// </summary>
    //    public static bool IsDefaultValue<T>(T value)
    //    {
    //        // 引用类型
    //        if (!typeof(T).IsValueType)
    //        {
    //            return value == null;
    //        }

    //        // 值类型使用比较器
    //        return EqualityComparer<T>.Default.Equals(value, default(T));
    //    }
    //}
}
