namespace EdsDcfNet.TestHost;

using System.Globalization;
using EdsDcfNet;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: EdsDcfNet.TestHost <sync|async> <file-path>");
            return 1;
        }

        var mode = args[0];
        var filePath = args[1];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Fixture file not found: {filePath}");
            return 1;
        }

        try
        {
            var eds = mode switch
            {
                "sync" => CanOpenFile.ReadEds(filePath),
                "async" => await CanOpenFile.ReadEdsAsync(filePath).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown mode '{mode}'. Expected 'sync' or 'async'.", nameof(args))
            };

            if (!eds.ObjectDictionary.Objects.TryGetValue(0x2000, out var obj))
            {
                Console.Error.WriteLine("Object 0x2000 was not parsed.");
                return 1;
            }

            Console.WriteLine($"SubNumber={obj.SubNumber?.ToString(CultureInfo.InvariantCulture) ?? "<null>"}");
            Console.WriteLine($"HasSub0={obj.SubObjects.ContainsKey(0x00)}");
            Console.WriteLine($"HasSubFF={obj.SubObjects.ContainsKey(0xFF)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
