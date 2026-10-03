namespace EdsDcfNet.Utilities;

/// <summary>
/// CiA 306-1 v1.4.0 Table 7: validity of the object-type dependent keys in
/// <c>[&lt;Index&gt;]</c> and <c>[&lt;Index&gt;sub&lt;sub-index&gt;]</c> sections. Single source for
/// the INI writers (which omit "n" keys) and the EDS/DCF readers (which report them).
/// </summary>
/// <remarks>
/// Columns: VAR/DEFTYPE; DEFSTRUCT/ARRAY/RECORD without or with zero <c>CompactSubObj</c>
/// (footnote a); DEFSTRUCT/ARRAY/RECORD with non-zero <c>CompactSubObj</c> (footnote b);
/// DOMAIN. Table 7 has no column for NULL or an unassigned object code; such objects keep all
/// keys. Only <see cref="KeyRule.NotSupported"/> drives behavior. "nc" (footnote c: not
/// supported, the value may be 0) is not reported on read; the writer emits <c>SubNumber</c>
/// under <c>CompactSubObj</c> only for expanded sub-objects above the compact range (review
/// finding S18) and never writes a zero <c>CompactSubObj</c>. One "n" key is still written:
/// <c>SubNumber</c> of a VAR, DEFTYPE, or DOMAIN object that has sub-objects, so an
/// unvalidated write does not lose them on re-read. A validated EDS/DCF write rejects such an
/// object instead (decision E10, <c>IniWriteRules</c>).
/// </remarks>
internal static class ObjectTypeKeyMatrix
{
    internal enum KeyRule : byte
    {
        Mandatory,
        Optional,
        NotSupported,
        NotSupportedZeroAllowed
    }

    private const KeyRule M = KeyRule.Mandatory;
    private const KeyRule O = KeyRule.Optional;
    private const KeyRule N = KeyRule.NotSupported;
    private const KeyRule Nc = KeyRule.NotSupportedZeroAllowed;

    private const int VarColumn = 0;
    private const int StructuredColumn = 1;
    private const int CompactStructuredColumn = 2;
    private const int DomainColumn = 3;

    // Columns: VAR/DEFTYPE, DEFSTRUCT/ARRAY/RECORD (a), DEFSTRUCT/ARRAY/RECORD (b), DOMAIN.
    private static readonly Dictionary<string, KeyRule[]> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ParameterName"] = new[] { M, M, M, M },
        ["ObjectType"] = new[] { O, M, M, M },
        ["DataType"] = new[] { M, N, M, O },
        ["AccessType"] = new[] { M, N, M, O },
        ["DefaultValue"] = new[] { O, N, O, O },
        ["PDOMapping"] = new[] { O, N, O, N },
        ["SubNumber"] = new[] { N, M, Nc, N },
        ["LowLimit"] = new[] { O, N, O, N },
        ["HighLimit"] = new[] { O, N, O, N },
        ["ObjFlags"] = new[] { O, O, O, O },
        ["CompactSubObj"] = new[] { N, Nc, M, N }
    };

    /// <summary>
    /// <see langword="true"/> when Table 7 marks <paramref name="key"/> as not supported ("n")
    /// for <paramref name="objectType"/>. <paramref name="hasCompactSubObj"/> selects the column
    /// with non-zero <c>CompactSubObj</c> for DEFSTRUCT/ARRAY/RECORD. Keys outside Table 7 and
    /// object codes without a column are never "n".
    /// </summary>
    internal static bool IsNotSupported(byte objectType, bool hasCompactSubObj, string key)
        => TryGetColumn(objectType, hasCompactSubObj, out var column)
           && Rules.TryGetValue(key, out var rules)
           && rules[column] == KeyRule.NotSupported;

    /// <summary>
    /// <see langword="true"/> when an object of <paramref name="objectType"/> may carry
    /// sub-indexes, that is, when Table 7 does not mark <c>SubNumber</c> as "n" for it.
    /// </summary>
    internal static bool AllowsSubObjects(byte objectType)
        => !IsNotSupported(objectType, hasCompactSubObj: false, "SubNumber");

    private static bool TryGetColumn(byte objectType, bool hasCompactSubObj, out int column)
    {
        switch (objectType)
        {
            case CanOpenObjectType.Var:
            case CanOpenObjectType.DefType:
                column = VarColumn;
                return true;
            case CanOpenObjectType.DefStruct:
            case CanOpenObjectType.Array:
            case CanOpenObjectType.Record:
                column = hasCompactSubObj ? CompactStructuredColumn : StructuredColumn;
                return true;
            case CanOpenObjectType.Domain:
                column = DomainColumn;
                return true;
            default:
                column = 0;
                return false;
        }
    }
}
