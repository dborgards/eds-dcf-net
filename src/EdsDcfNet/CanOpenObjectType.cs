namespace EdsDcfNet;

/// <summary>
/// CiA 306 object-type codes and related helpers.
/// Single source of truth for the reader (composite-type detection, defaults)
/// and the validator (valid-code check).
/// </summary>
/// <remarks>
/// Code assignment per CiA DS 306: 0x0 = NULL, 0x2 = DOMAIN, 0x5 = DEFTYPE,
/// 0x6 = DEFSTRUCT, 0x7 = VAR, 0x8 = ARRAY, 0x9 = RECORD.
/// The constants are public so consumers can compare
/// <see cref="Models.CanOpenObject.ObjectType"/> values without re-implementing the table.
/// </remarks>
public static class CanOpenObjectType
{
    /// <summary>NULL object-type code (0x0).</summary>
    public const byte Null = 0x0;

    /// <summary>DOMAIN object-type code (0x2).</summary>
    public const byte Domain = 0x2;

    /// <summary>DEFTYPE object-type code (0x5).</summary>
    public const byte DefType = 0x5;

    /// <summary>DEFSTRUCT object-type code (0x6).</summary>
    public const byte DefStruct = 0x6;

    /// <summary>VAR object-type code (0x7).</summary>
    public const byte Var = 0x7;

    /// <summary>ARRAY object-type code (0x8).</summary>
    public const byte Array = 0x8;

    /// <summary>RECORD object-type code (0x9).</summary>
    public const byte Record = 0x9;

    /// <summary>
    /// The default object type literal used when an INI section omits <c>ObjectType</c>
    /// (VAR, per CiA 306).
    /// </summary>
    internal const string VarLiteral = "0x7";

    /// <summary>
    /// Returns whether <paramref name="code"/> is a valid CiA 306 object-type code.
    /// </summary>
    internal static bool IsValid(byte code) =>
        code is Null or Domain or DefType or DefStruct or Var or Array or Record;

    /// <summary>
    /// Returns whether objects of this type are composite and carry sub-objects
    /// (DEFSTRUCT, ARRAY, RECORD).
    /// </summary>
    internal static bool HasSubObjects(byte code) =>
        code is DefStruct or Array or Record;
}
