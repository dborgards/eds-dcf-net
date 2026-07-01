namespace EdsDcfNet.Tests.Utilities;

using System.Collections;
using System.Reflection;
using EdsDcfNet;
using EdsDcfNet.Models;
using FluentAssertions;

/// <summary>
/// Builds model instances with every public settable property and collection entry
/// populated to a distinct non-default value for clone completeness testing.
/// </summary>
internal static class ModelCloneSampleBuilder
{
    private const int MaxParameterGroupDepth = 2;

    public static object CreateSourceArgument(ParameterInfo parameter, ref int seed)
    {
        var parameterType = parameter.ParameterType;

        if (parameterType == typeof(List<ModuleInfo>))
        {
            var list = new List<ModuleInfo> { (ModuleInfo)CreateSample(typeof(ModuleInfo), ref seed) };
            return list;
        }

        if (parameterType == typeof(List<ToolInfo>))
        {
            var list = new List<ToolInfo> { (ToolInfo)CreateSample(typeof(ToolInfo), ref seed) };
            return list;
        }

        if (parameterType == typeof(Dictionary<string, Dictionary<string, string>>))
        {
            var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [$"key_{seed++}"] = $"value_{seed++}"
            };
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [$"Section_{seed++}"] = section
            };
        }

        var underlying = Nullable.GetUnderlyingType(parameterType);
        if (underlying != null)
            return CreateSample(underlying, ref seed);

        return CreateSample(parameterType, ref seed);
    }

    public static object CreateSample(Type type, ref int seed) =>
        CreateSample(type, ref seed, depth: 0);

    private static object CreateSample(Type type, ref int seed, int depth)
    {
        if (type == typeof(string))
            return $"sample_{seed++}";

        if (type.IsEnum)
            return GetNonDefaultEnumValue(type, seed++);

        if (type.IsPrimitive)
            return GetNonDefaultPrimitive(type, seed++);

        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Could not create an instance of {type.FullName}.");

        Populate(instance, ref seed, depth);
        return instance;
    }

    public static void Populate(object instance, ref int seed, int depth = 0)
    {
        foreach (var property in instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.PropertyType == typeof(ApLabelGroup) && property.CanRead && !property.CanWrite)
            {
                PopulateLabelGroup((ApLabelGroup)property.GetValue(instance)!, ref seed);
                continue;
            }

            if (IsReadOnlyCollection(property))
            {
                PopulateCollection(property.GetValue(instance), property.PropertyType, ref seed, depth);
                continue;
            }

            if (!property.CanWrite)
                continue;

            var value = CreatePropertyValue(property.PropertyType, ref seed, depth, property.Name);
            if (value != ModelCloneSampleBuilderSkippedMarker.Value)
                property.SetValue(instance, value);
        }
    }

    private static void PopulateLabelGroup(ApLabelGroup labelGroup, ref int seed)
    {
        labelGroup.Labels.Add((ApLabel)CreateSample(typeof(ApLabel), ref seed));
        labelGroup.Descriptions.Add((ApDescription)CreateSample(typeof(ApDescription), ref seed));
        labelGroup.TextRefs.Add((ApTextRef)CreateSample(typeof(ApTextRef), ref seed));
    }

    private static void PopulateCollection(object? collection, Type collectionType, ref int seed, int depth)
    {
        if (collection is not IList list)
            return;

        if (collection is IDictionary dictionary)
        {
            PopulateDictionary(dictionary, collectionType, ref seed, depth);
            return;
        }

        var elementType = collectionType.IsGenericType
            ? collectionType.GetGenericArguments()[0]
            : typeof(object);

        if (elementType == typeof(ApParameterGroup) && depth >= MaxParameterGroupDepth)
            return;

        list.Add(CreateCollectionElement(elementType, ref seed, depth));
    }

    private static void PopulateDictionary(IDictionary dictionary, Type dictionaryType, ref int seed, int depth)
    {
        var keyType = dictionaryType.GenericTypeArguments[0];
        var valueType = dictionaryType.GenericTypeArguments[1];

        var key = CreateDictionaryKey(keyType, ref seed);
        var value = IsModelType(valueType)
            ? CreateSample(valueType, ref seed, depth + 1)
            : CreatePropertyValue(valueType, ref seed, depth + 1, dictionaryType.Name);

        if (value == ModelCloneSampleBuilderSkippedMarker.Value)
            return;

        dictionary[key] = value;
    }

    private static object CreateDictionaryKey(Type keyType, ref int seed) =>
        keyType switch
        {
            _ when keyType == typeof(string) => $"dict_key_{seed++}",
            _ when keyType == typeof(ushort) => (ushort)(0x1000 + seed++),
            _ when keyType == typeof(byte) => (byte)((seed++ % 254) + 1),
            _ when keyType == typeof(int) => seed++,
            _ => CreateSample(keyType, ref seed)
        };

    private static object CreateCollectionElement(Type elementType, ref int seed, int depth) =>
        elementType == typeof(string)
            ? $"item_{seed++}"
            : CreateSample(elementType, ref seed, depth + 1);

    private static object CreatePropertyValue(Type propertyType, ref int seed, int depth, string propertyName)
    {
        if (propertyType == typeof(string))
            return $"prop_{propertyName}_{seed++}";

        if (propertyType.IsEnum)
            return GetNonDefaultEnumValue(propertyType, seed++);

        if (propertyType.IsPrimitive)
            return GetNonDefaultPrimitive(propertyType, seed++);

        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying != null)
            return CreateSample(underlying, ref seed, depth + 1);

        if (propertyType == typeof(ApParameterGroup) && depth >= MaxParameterGroupDepth)
            return ModelCloneSampleBuilderSkippedMarker.Value;

        if (IsModelType(propertyType))
            return CreateSample(propertyType, ref seed, depth + 1);

        return ModelCloneSampleBuilderSkippedMarker.Value;
    }

    private static bool IsReadOnlyCollection(PropertyInfo property) =>
        property.CanRead
        && !property.CanWrite
        && typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
        && property.PropertyType != typeof(string);

    private static object GetNonDefaultEnumValue(Type enumType, int seed)
    {
        var values = Enum.GetValues(enumType);
        if (values.Length == 1)
            return values.GetValue(0)!;

        var index = Math.Abs(seed % values.Length) % values.Length;
        return values.GetValue(index)!;
    }

    private static object GetNonDefaultPrimitive(Type type, int seed) =>
        Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => true,
            TypeCode.Byte => (byte)((seed % 254) + 1),
            TypeCode.SByte => (sbyte)1,
            TypeCode.Int16 => (short)(seed + 1),
            TypeCode.UInt16 => (ushort)(seed + 1),
            TypeCode.Int32 => seed + 1,
            TypeCode.UInt32 => (uint)(seed + 1),
            TypeCode.Int64 => (long)seed + 1,
            TypeCode.UInt64 => (ulong)seed + 1,
            TypeCode.Single => seed + 0.5f,
            TypeCode.Double => seed + 0.5d,
            TypeCode.Char => (char)('A' + (seed % 26)),
            _ => Activator.CreateInstance(type)!
        };

    private static bool IsModelType(Type type) =>
        type.Namespace?.StartsWith("EdsDcfNet.Models", StringComparison.Ordinal) == true;
}

internal static class ModelCloneSampleBuilderSkippedMarker
{
    internal static readonly object Value = new();
}

/// <summary>
/// Asserts deep structural equality and deep-copy semantics between a source model graph
/// and its clone.
/// </summary>
internal static class ModelCloneDeepAssert
{
    public static void AssertDeepClone(object? source, object? clone, string path = "root")
    {
        if (source is null || clone is null)
        {
            source.Should().BeSameAs(clone, $"both sides should be null at {path}");
            return;
        }

        clone.Should().NotBeSameAs(source, $"top-level clone must be a new instance at {path}");
        AssertEquivalent(source, clone, path, assertDistinctInstances: true);
    }

    private static void AssertEquivalent(
        object source,
        object clone,
        string path,
        bool assertDistinctInstances)
    {
        if (source is string sourceString)
        {
            clone.Should().BeOfType<string>($"{path} should remain a string");
            clone.Should().Be(sourceString, $"{path} string values should match");
            return;
        }

        if (source.GetType().IsValueType)
        {
            clone.Should().Be(source, $"{path} value-type members should match");
            return;
        }

        if (source is IDictionary sourceDictionary)
        {
            AssertDictionaryEquivalent(sourceDictionary, (IDictionary)clone, path, assertDistinctInstances);
            return;
        }

        if (source is IList sourceList)
        {
            AssertListEquivalent(sourceList, (IList)clone, path, assertDistinctInstances);
            return;
        }

        if (assertDistinctInstances)
            clone.Should().NotBeSameAs(source, $"{path} should be a deep-copied instance");

        foreach (var property in source.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead)
                continue;

            var sourceValue = property.GetValue(source);
            var cloneValue = property.GetValue(clone);
            var propertyPath = $"{path}.{property.Name}";

            if (sourceValue is null)
            {
                cloneValue.Should().BeNull($"{propertyPath} should remain null");
                continue;
            }

            cloneValue.Should().NotBeNull($"{propertyPath} should be cloned");

            if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
            {
                cloneValue.Should().Be(sourceValue, $"{propertyPath} scalar values should match");
                continue;
            }

            if (sourceValue is IEnumerable and not string)
            {
                AssertEquivalent(sourceValue, cloneValue!, propertyPath, assertDistinctInstances: true);
                continue;
            }

            AssertEquivalent(sourceValue, cloneValue!, propertyPath, assertDistinctInstances: true);
        }
    }

    private static void AssertListEquivalent(
        IList source,
        IList clone,
        string path,
        bool assertDistinctInstances)
    {
        if (assertDistinctInstances)
            clone.Should().NotBeSameAs(source, $"{path} collection should be a new instance");

        clone.Count.Should().Be(source.Count, $"{path} collection counts should match");

        for (var i = 0; i < source.Count; i++)
        {
            var itemPath = $"{path}[{i}]";
            var sourceItem = source[i];
            var cloneItem = clone[i];

            if (sourceItem is null)
            {
                cloneItem.Should().BeNull(itemPath);
                continue;
            }

            if (sourceItem is string or ValueType)
            {
                cloneItem.Should().Be(sourceItem, $"{itemPath} element values should match");
                continue;
            }

            AssertEquivalent(sourceItem, cloneItem!, itemPath, assertDistinctInstances: true);
        }
    }

    private static void AssertDictionaryEquivalent(
        IDictionary source,
        IDictionary clone,
        string path,
        bool assertDistinctInstances)
    {
        if (assertDistinctInstances)
            clone.Should().NotBeSameAs(source, $"{path} dictionary should be a new instance");

        clone.Count.Should().Be(source.Count, $"{path} dictionary counts should match");

        foreach (DictionaryEntry entry in source)
        {
            var keyPath = $"{path}[{entry.Key}]";
            clone.Contains(entry.Key).Should().BeTrue($"{keyPath} key should exist in clone");

            var sourceValue = entry.Value;
            var cloneValue = clone[entry.Key];

            if (sourceValue is null)
            {
                cloneValue.Should().BeNull(keyPath);
                continue;
            }

            if (sourceValue is string or ValueType)
            {
                cloneValue.Should().Be(sourceValue, $"{keyPath} values should match");
                continue;
            }

            AssertEquivalent(sourceValue, cloneValue!, keyPath, assertDistinctInstances: true);
        }
    }
}

/// <summary>
/// Discovers <see cref="EdsDcfNet.Utilities.ModelCloner"/> clone entry points via reflection.
/// </summary>
internal static class ModelClonerTestCatalog
{
  private static readonly Type ModelClonerType =
      typeof(CanOpenFile).Assembly.GetType("EdsDcfNet.Utilities.ModelCloner", throwOnError: true)!;

  public static IEnumerable<object[]> CloneMethods =>
      ModelClonerType
          .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
          .Where(method => method.Name.StartsWith("Clone", StringComparison.Ordinal))
          .OrderBy(method => method.Name, StringComparer.Ordinal)
          .Select(method => new object[] { method.Name });
}
