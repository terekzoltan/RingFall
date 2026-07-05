namespace Ringfall.Core;

/// <summary>
/// Stable metadata for the initial core/headless shell. This is not domain state.
/// </summary>
public static class CoreInfo
{
    public static string ProductName => "Ringfall Headless";
    public static string Version => "0.1.0-shell";
    public static string ShellStatus => "core/headless shell only";

    public static readonly string[] SupportedCommands = ["--help", "--version"];
}
