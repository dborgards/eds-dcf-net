namespace EdsDcfNet;

/// <summary>
/// CiA 306 object-type codes and related helpers.
/// Single source of truth for the reader (composite-type detection, defaults)
/// and the validator (valid-code check).
/// </summary>
/// <remarks>
/// Code assignment per CiA DS 306: 0x0 = NULL, 0x2 = DOMAIN, 0x5 = DEFTYPE,
/// 0x6 = DEFSTRUCT, 0x7 = VAR, 0x8 = ARRAY, 0x9 = RECORD.
/// </remarks>
internal static class CanOpenObjectType
{
    internal const byte Null = 0x0;
    internal const byte Domain = 0x2;
    internal const byte DefType = 0x5;
    internal const byte DefStruct = 0x6;
    internal const byte Var = 0x7;
    internal const byte Array = 0x8;
    internal const byte Record = 0x9;

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
