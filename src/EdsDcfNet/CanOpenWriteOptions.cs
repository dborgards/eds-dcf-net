namespace EdsDcfNet;

using System.Text;

/// <summary>
/// Optional behavior for <see cref="CanOpenFile"/> and format-specific operations
/// (<see cref="CanOpenFile.Eds"/>, <see cref="CanOpenFile.Dcf"/>, <see cref="CanOpenFile.Cpj"/>,
/// <see cref="CanOpenFile.Xdd"/>, <see cref="CanOpenFile.Xdc"/>) write methods.
/// </summary>
/// <remarks>
/// This type intentionally holds only cross-format write concerns. Format-specific
/// options are introduced as derived per-format option types (unsealing this type
/// on demand) rather than as additional properties here — see the
/// "Options extension pattern" section in the README.
/// </remarks>
public sealed class CanOpenWriteOptions
{
    /// <summary>
    /// Gets a default options instance with validation disabled.
    /// </summary>
    public static CanOpenWriteOptions Default { get; } = new();

    /// <summary>
    /// Gets an options instance that validates the model before writing.
    /// </summary>
    public static CanOpenWriteOptions Validated { get; } = new() { ValidateBeforeWrite = true };

    /// <summary>
    /// When <see langword="true"/>, write methods validate the model and throw
    /// <see cref="Exceptions.ModelValidationException"/> when validation issues are found.
    /// Default is <see langword="false"/> for backward compatibility.
    /// </summary>
    public bool ValidateBeforeWrite { get; init; }

    /// <summary>
    /// Gets the encoding used for byte output of every format (EDS, DCF, CPJ, XDD, and XDC).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> (the default) writes UTF-8 without a byte-order mark, as previous
    /// releases did. File and stream output, synchronous and asynchronous, uses this encoding.
    /// For XDD and XDC the XML declaration names the same encoding.
    /// </para>
    /// <para>
    /// <c>WriteToString</c> returns a .NET string. This property affects only byte output;
    /// the string itself is unchanged, and the XDD/XDC declaration in that string stays UTF-8.
    /// </para>
    /// <para>
    /// Characters the encoding cannot represent are not replaced with a substitute character.
    /// EDS, DCF, and CPJ throw the format's write exception; the inner
    /// <see cref="EncoderFallbackException"/> names the character and its index in the output.
    /// The destination file is left unchanged. XDD and XDC emit a numeric character reference
    /// for those characters in text and attribute values.
    /// </para>
    /// </remarks>
    public Encoding? Encoding { get; init; }

    internal static Encoding? ResolveEncoding(CanOpenWriteOptions? options)
        => options?.Encoding;
}
