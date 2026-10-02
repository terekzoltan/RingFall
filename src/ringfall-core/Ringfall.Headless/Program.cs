using Ringfall.Core;
using Ringfall.Core.Formal;

if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine(CoreInfo.ProductName);
    Console.WriteLine($"Status: {CoreInfo.ShellStatus}");
    Console.WriteLine("Commands:");
    foreach (var command in CoreInfo.SupportedCommands)
    {
        Console.WriteLine($"  {command}");
    }

    return 0;
}

if (args is ["--version"])
{
    Console.WriteLine($"{CoreInfo.ProductName} {CoreInfo.Version}");
    return 0;
}

if (args.Length > 0 && args[0] == "aster-f1-gated-execute")
    return AsterF1GatedCommand.Run(args);

if (args.Length > 0 && args[0] == "aster-f1-evidence")
{
    static int Fail(string code, int exit)
    {
        Console.Error.WriteLine($"p1_error:{code}");
        return exit;
    }

    var paths = new Dictionary<string, string>(StringComparer.Ordinal);
    var flags = new[] { "--state", "--candidate", "--pulse", "--context" };
    if (args.Length != 9) return Fail("usage_invalid", 2);
    for (var index = 1; index < args.Length; index += 2)
    {
        if (!flags.Contains(args[index], StringComparer.Ordinal) || !paths.TryAdd(args[index], args[index + 1])
            || string.IsNullOrWhiteSpace(args[index + 1])) return Fail("usage_invalid", 2);
    }
    try
    {
        byte[] Read(string flag)
        {
            var path = paths[flag];
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > 1048576) throw new AsterF1P1Exception("input_invalid");
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length is <= 0 or > 1048576) throw new AsterF1P1Exception("input_invalid");
            return bytes;
        }
        var evidence = AsterF1P1EvidenceBridge.Produce(Read("--state"), Read("--candidate"),
            Read("--pulse"), Read("--context"));
        Console.WriteLine(evidence);
        return 0;
    }
    catch (AsterF1P1Exception exception)
    {
        return Fail(exception.Code, exception.Code switch
        {
            "input_invalid" or "state_invalid" or "unsupported_packet_kind" => 3,
            "context_unbound" or "context_mismatch" => 4,
            _ => 5
        });
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException)
    {
        return Fail("input_invalid", 3);
    }
    catch (Exception)
    {
        return Fail("internal_failure", 5);
    }
}

Console.Error.WriteLine($"Unknown argument: {args[0]}");
Console.Error.WriteLine("Run with --help for available commands.");
return 2;
