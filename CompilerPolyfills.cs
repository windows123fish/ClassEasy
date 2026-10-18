// net6 移植：编译器特性 polyfill。
// 这些类型在 .NET 7+ 才内置于 BCL，在 net6.0 / net472 上需要自行声明，
// 以支持 C# 11 的 required 成员、C# 12 的主构造函数等由编译器生成的特性标注。
// 本文件通过 Global.props 仅在 net6.0* / net472 目标下编译。

namespace System.Runtime.CompilerServices
{
#if !NET5_0_OR_GREATER
    /// <summary>
    /// 支持 init-only 访问器（C# 9）。.NET 5+ 内置，net472 需要自行声明。
    /// </summary>
    internal static class IsExternalInit
    {
    }
#endif

    /// <summary>
    /// 指示编译器某个成员是必需的（C# 11 required 成员）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    /// <summary>
    /// 指示使用该成员需要特定的编译器功能。
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }

        public bool IsOptional { get; init; }

        public const string RefStructs = nameof(RefStructs);

        public const string RequiredMembers = nameof(RequiredMembers);
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// 指示构造函数设置所有必需的成员。
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute
    {
    }

    /// <summary>
    /// 指定字符串参数应使用的语法类型（仅供分析器与 IDE 使用）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false, Inherited = false)]
    internal sealed class StringSyntaxAttribute : Attribute
    {
        public StringSyntaxAttribute(string syntax)
        {
            Syntax = syntax;
            Arguments = Array.Empty<object?>();
        }

        public StringSyntaxAttribute(string syntax, params object?[] arguments)
        {
            Syntax = syntax;
            Arguments = arguments;
        }

        public string Syntax { get; }

        public object?[] Arguments { get; }

        public const string CompositeFormat = nameof(CompositeFormat);
        public const string DateOnlyFormat = nameof(DateOnlyFormat);
        public const string DateTimeFormat = nameof(DateTimeFormat);
        public const string EnumFormat = nameof(EnumFormat);
        public const string GuidFormat = nameof(GuidFormat);
        public const string Json = nameof(Json);
        public const string NumericFormat = nameof(NumericFormat);
        public const string Regex = nameof(Regex);
        public const string TimeOnlyFormat = nameof(TimeOnlyFormat);
        public const string TimeSpanFormat = nameof(TimeSpanFormat);
        public const string Uri = nameof(Uri);
        public const string Xml = nameof(Xml);
    }
}
