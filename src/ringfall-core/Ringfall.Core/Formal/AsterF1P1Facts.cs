namespace Ringfall.Core.Formal;

internal sealed record AsterF1P1Field(string Name, string Presence, object? Value = null,
    string Coverage = "guarded_by_core_validator");

internal sealed record AsterF1P1Facts(
    string PacketKind,
    string? IssuerId,
    string? IssuerLayer,
    string? IssuerHomeSectorId,
    object? Tool,
    object? WorkOrder,
    IReadOnlyList<AsterF1P1Field> ToolArguments,
    IReadOnlyList<string> UnsupportedPredicates);

internal sealed class AsterF1P1Exception(string code) : Exception(code)
{
    public string Code { get; } = code;
}
