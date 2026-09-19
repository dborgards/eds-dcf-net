namespace EdsDcfNet;

/// <summary>
/// CiA 301 (§7.4.7) data-type index constants and metadata: bit lengths,
/// signedness, and display names.
/// </summary>
/// <remarks>
/// A <see langword="static"/> class with <see cref="ushort"/> constants instead of an enum so
/// manufacturer-specific data types (0x0040 and above) remain representable in the existing
/// <see cref="ushort"/> model properties (<see cref="Models.CanOpenObject.DataType"/>,
/// <see cref="Models.CanOpenSubObject.DataType"/>).
/// <see cref="TryGetBitLength"/> is the single source of truth for fixed bit widths;
/// <see cref="Utilities.CanOpenValueConverter"/> derives its conversion widths from it.
/// </remarks>
public static class CanOpenDataType
{
    /// <summary>BOOLEAN (0x0001, 1 bit).</summary>
    public const ushort Boolean = 0x0001;

    /// <summary>INTEGER8 (0x0002, 8 bit, signed).</summary>
    public const ushort Integer8 = 0x0002;

    /// <summary>INTEGER16 (0x0003, 16 bit, signed).</summary>
    public const ushort Integer16 = 0x0003;

    /// <summary>INTEGER32 (0x0004, 32 bit, signed).</summary>
    public const ushort Integer32 = 0x0004;

    /// <summary>UNSIGNED8 (0x0005, 8 bit, unsigned).</summary>
    public const ushort Unsigned8 = 0x0005;

    /// <summary>UNSIGNED16 (0x0006, 16 bit, unsigned).</summary>
    public const ushort Unsigned16 = 0x0006;

    /// <summary>UNSIGNED32 (0x0007, 32 bit, unsigned).</summary>
    public const ushort Unsigned32 = 0x0007;

    /// <summary>REAL32 (0x0008, 32 bit).</summary>
    public const ushort Real32 = 0x0008;

    /// <summary>VISIBLE_STRING (0x0009, variable length).</summary>
    public const ushort VisibleString = 0x0009;

    /// <summary>OCTET_STRING (0x000A, variable length).</summary>
    public const ushort OctetString = 0x000A;

    /// <summary>UNICODE_STRING (0x000B, variable length).</summary>
    public const ushort UnicodeString = 0x000B;

    /// <summary>TIME_OF_DAY (0x000C, 48 bit).</summary>
    public const ushort TimeOfDay = 0x000C;

    /// <summary>TIME_DIFFERENCE (0x000D, 48 bit).</summary>
    public const ushort TimeDifference = 0x000D;

    /// <summary>DOMAIN (0x000F, variable length).</summary>
    public const ushort Domain = 0x000F;

    /// <summary>INTEGER24 (0x0010, 24 bit, signed).</summary>
    public const ushort Integer24 = 0x0010;

    /// <summary>REAL64 (0x0011, 64 bit).</summary>
    public const ushort Real64 = 0x0011;

    /// <summary>INTEGER40 (0x0012, 40 bit, signed).</summary>
    public const ushort Integer40 = 0x0012;

    /// <summary>INTEGER48 (0x0013, 48 bit, signed).</summary>
    public const ushort Integer48 = 0x0013;

    /// <summary>INTEGER56 (0x0014, 56 bit, signed).</summary>
    public const ushort Integer56 = 0x0014;

    /// <summary>INTEGER64 (0x0015, 64 bit, signed).</summary>
    public const ushort Integer64 = 0x0015;

    /// <summary>UNSIGNED24 (0x0016, 24 bit, unsigned).</summary>
    public const ushort Unsigned24 = 0x0016;

    /// <summary>UNSIGNED40 (0x0018, 40 bit, unsigned).</summary>
    public const ushort Unsigned40 = 0x0018;

    /// <summary>UNSIGNED48 (0x0019, 48 bit, unsigned).</summary>
    public const ushort Unsigned48 = 0x0019;

    /// <summary>UNSIGNED56 (0x001A, 56 bit, unsigned).</summary>
    public const ushort Unsigned56 = 0x001A;

    /// <summary>UNSIGNED64 (0x001B, 64 bit, unsigned).</summary>
    public const ushort Unsigned64 = 0x001B;

    private readonly record struct Entry(ushort Code, string Name, int? Bits, bool Signed, bool Unsigned);

    private static readonly Entry[] Table =
    {
        new(Boolean, "BOOLEAN", 1, false, false),
        new(Integer8, "INTEGER8", 8, true, false),
        new(Integer16, "INTEGER16", 16, true, false),
        new(Integer32, "INTEGER32", 32, true, false),
        new(Unsigned8, "UNSIGNED8", 8, false, true),
        new(Unsigned16, "UNSIGNED16", 16, false, true),
        new(Unsigned32, "UNSIGNED32", 32, false, true),
        new(Real32, "REAL32", 32, false, false),
        new(VisibleString, "VISIBLE_STRING", null, false, false),
        new(OctetString, "OCTET_STRING", null, false, false),
        new(UnicodeString, "UNICODE_STRING", null, false, false),
        new(TimeOfDay, "TIME_OF_DAY", 48, false, false),
        new(TimeDifference, "TIME_DIFFERENCE", 48, false, false),
        new(Domain, "DOMAIN", null, false, false),
        new(Integer24, "INTEGER24", 24, true, false),
        new(Real64, "REAL64", 64, false, false),
        new(Integer40, "INTEGER40", 40, true, false),
        new(Integer48, "INTEGER48", 48, true, false),
        new(Integer56, "INTEGER56", 56, true, false),
        new(Integer64, "INTEGER64", 64, true, false),
        new(Unsigned24, "UNSIGNED24", 24, false, true),
        new(Unsigned40, "UNSIGNED40", 40, false, true),
        new(Unsigned48, "UNSIGNED48", 48, false, true),
        new(Unsigned56, "UNSIGNED56", 56, false, true),
        new(Unsigned64, "UNSIGNED64", 64, false, true),
    };

    /// <summary>
    /// Returns the fixed bit length of <paramref name="dataType"/>, or <see langword="null"/>
    /// for variable-length types (VISIBLE_STRING, OCTET_STRING, UNICODE_STRING, DOMAIN),
    /// reserved codes, and manufacturer-specific or unknown types. Consumers must not assume
    /// a fixed width when the result is <see langword="null"/>.
    /// </summary>
    /// <param name="dataType">The CANopen data-type index.</param>
    /// <returns>The bit length, or <see langword="null"/> when no fixed width exists.</returns>
    public static int? TryGetBitLength(ushort dataType) => Find(dataType)?.Bits;

    /// <summary>
    /// Returns whether <paramref name="dataType"/> is an assigned CiA 301 standard data type
    /// (0x0001–0x001B, excluding reserved codes).
    /// </summary>
    /// <param name="dataType">The CANopen data-type index.</param>
    public static bool IsStandardType(ushort dataType) => Find(dataType) != null;

    /// <summary>
    /// Returns whether <paramref name="dataType"/> is a signed integer type
    /// (INTEGER8–INTEGER64).
    /// </summary>
    /// <param name="dataType">The CANopen data-type index.</param>
    public static bool IsSigned(ushort dataType) => Find(dataType)?.Signed ?? false;

    /// <summary>
    /// Returns whether <paramref name="dataType"/> is an unsigned integer type
    /// (UNSIGNED8–UNSIGNED64).
    /// </summary>
    /// <param name="dataType">The CANopen data-type index.</param>
    public static bool IsUnsigned(ushort dataType) => Find(dataType)?.Unsigned ?? false;

    /// <summary>
    /// Returns the CiA 301 name of <paramref name="dataType"/> (for example
    /// <c>"UNSIGNED32"</c>) for display purposes, or <see langword="null"/> for reserved,
    /// manufacturer-specific, or unknown codes.
    /// </summary>
    /// <param name="dataType">The CANopen data-type index.</param>
    public static string? GetName(ushort dataType) => Find(dataType)?.Name;

    private static Entry? Find(ushort code)
    {
        foreach (var entry in Table)
        {
            if (entry.Code == code)
                return entry;
        }

        return null;
    }
}
