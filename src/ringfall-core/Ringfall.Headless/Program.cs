using Ringfall.Core;

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

Console.Error.WriteLine($"Unknown argument: {args[0]}");
Console.Error.WriteLine("Run with --help for available commands.");
return 2;
