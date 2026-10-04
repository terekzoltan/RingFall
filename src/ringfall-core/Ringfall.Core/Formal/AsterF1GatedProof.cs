using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ringfall.Core.Formal;

// Independent fixed-symbol interpretation of Core facts. The Python mapper remains the
// accepted historical source; matching its complete instance bytes is a gate condition.
internal static class AsterF1GatedProof
{
    private static readonly string[] Classes = ["F1Actor", "F1Tool", "F1Crew", "F1Sector", "F1Action", "F1Location", "F1Request"];
    private static readonly string[] Relations = ["toolRef", "crewRef", "actorHomeSector", "crewHomeSector", "supports",
        "assignedActor", "issuer", "targetTool", "targetCrew", "action", "targetLocation"];
    private static readonly string[] Arguments = ["branch_id", "from_branch", "to_branch", "asset_id", "load_fraction"];
    private static readonly Dictionary<string, (string Tool, string[] Required)> Rules = new(StringComparer.Ordinal)
    {
        ["query_heat_alarm"] = ("local_grid_panel", []),
        ["query_branch_load"] = ("local_grid_panel", ["branch_id"]),
        ["dry_run_reroute"] = ("local_grid_panel", ["from_branch", "to_branch", "load_fraction"]),
        ["query_asset_status"] = ("maintenance_console", ["asset_id"]),
        ["query_backlog"] = ("maintenance_console", []),
        ["dry_run_patch"] = ("maintenance_console", ["asset_id"])
    };

    internal sealed record ClosedInstance(byte[] Bytes, byte[] ModelPrefix, string Sha256, HashSet<string> Nodes,
        HashSet<string> WitnessStatements);

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new AsterF1P1Exception("instance_fact_invalid");

    private static void Require(bool condition)
    {
        if (!condition) throw new AsterF1P1Exception("instance_fact_unmodeled");
    }

    private static JsonElement Field(JsonElement row, string name, bool optional = false)
    {
        Require(Text(row, "name") == name && Text(row, "coverage") == "guarded_by_core_validator");
        var presence = Text(row, "presence");
        var value = row.GetProperty("value");
        if (optional && presence == "absent" && value.ValueKind == JsonValueKind.Null) return value;
        Require(presence == "value");
        return value;
    }

    internal static ClosedInstance Map(AsterF1GatedExecution.Preflight preflight, byte[] model)
    {
        try
        {
            using var parsed = JsonDocument.Parse(preflight.Json);
            var record = parsed.RootElement;
            Require(Text(record, "completeness") == "complete"
                && record.GetProperty("unsupported_predicates").GetArrayLength() == 0);
            var facts = record.GetProperty("facts");
            Require(Text(facts, "issuer_id") == "A1" && Text(facts, "issuer_layer") == "L1"
                && Text(facts, "issuer_home_sector_id") == "Aster");
            var kind = Text(facts, "packet_kind");
            Require(kind is "ToolActionRequest" or "WorkOrderRequest");
            var tool = kind == "ToolActionRequest";
            var nodes = new HashSet<string>(["a1", "r1", "s1"], StringComparer.Ordinal);
            nodes.UnionWith(tool ? ["t1", "ac1"] : ["c1", "loc1"]);
            var edges = new SortedSet<string>(StringComparer.Ordinal)
            {
                "actorHomeSector(a1, s1).", "issuer(r1, a1)."
            };
            var declarations = new List<string> { "", "% One closed P1-bound intervention; no automatic nodes or supporting edges." };
            declarations.AddRange(Classes.Select(name => $"!exists({name}::new)."));
            declarations.AddRange(Relations.Select(name => $"default !{name}(*, *)."));
            declarations.Add("scope node = 5, " + string.Join(", ", Classes.Select(name => $"{name} = {(name switch
            {
                "F1Actor" or "F1Request" or "F1Sector" => 1,
                "F1Tool" or "F1Action" => tool ? 1 : 0,
                _ => tool ? 0 : 1
            })}")) + ".");
            declarations.AddRange(["F1Actor(a1).", "F1Request(r1).", "F1Sector(s1)."]);
            var attrs = new List<string> { "modeExecute(r1): false.", "requiresDryRunFalse(r1): false.",
                "missingRequiredArgument(r1): false.", "fractionPresent(r1): false.", "fractionOrZero(r1): 0.0." };

            if (tool)
            {
                Require(facts.GetProperty("work_order").ValueKind == JsonValueKind.Null);
                var target = facts.GetProperty("tool");
                var action = Text(target, "action");
                Require(Rules.TryGetValue(action, out var rule) && Text(target, "tool_id") == rule.Tool
                    && target.GetProperty("actor_tool_referenced").GetBoolean()
                    && Text(target, "status") is "available" or "unavailable"
                    && target.GetProperty("supported_actions").EnumerateArray().Any(item => item.GetString() == action)
                    && target.GetProperty("system_refs").ValueKind != JsonValueKind.Null
                    && Text(target, "mode") is "dry_run" or "execute"
                    && Text(target, "arguments_presence") is "absent" or "value");
                var fields = facts.GetProperty("tool_arguments").EnumerateArray().ToArray();
                Require(fields.Length == Arguments.Length);
                var missing = 0;
                JsonElement fraction = default;
                for (var i = 0; i < fields.Length; i++)
                {
                    Require(Text(fields[i], "name") == Arguments[i]);
                    if (rule.Required.Contains(Arguments[i], StringComparer.Ordinal))
                    {
                        var value = Field(fields[i], Arguments[i], optional: true);
                        if (value.ValueKind == JsonValueKind.Null) missing++;
                        else if (Arguments[i] == "load_fraction")
                        {
                            Require(value.ValueKind == JsonValueKind.Number);
                            fraction = value;
                        }
                        else
                        {
                            var text = value.GetString();
                            Require(Arguments[i] switch
                            {
                                "branch_id" => text is "Aster-G3" or "Aster-G4",
                                "from_branch" => text == "Aster-G3",
                                "to_branch" => text == "Aster-G4",
                                "asset_id" => text == "Aster/Grid-Spine-03/Coupler-7",
                                _ => false
                            });
                        }
                    }
                    else Require(Text(fields[i], "presence") == "absent"
                        && Text(fields[i], "coverage") == "guarded_by_core_validator");
                }
                Require(missing <= 1);
                var dryRun = Field(target.GetProperty("requires_dry_run"), "requires_dry_run", optional: true);
                Require(dryRun.ValueKind is JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False);
                if (fraction.ValueKind != JsonValueKind.Undefined)
                {
                    Require(action == "dry_run_reroute");
                    var decimalValue = decimal.Parse(fraction.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);
                    var formatted = decimalValue.ToString("0.############################", CultureInfo.InvariantCulture);
                    attrs[3] = "fractionPresent(r1): true.";
                    attrs[4] = $"fractionOrZero(r1): {(formatted.Contains('.') ? formatted : formatted + ".0")}.";
                }
                attrs[0] = $"modeExecute(r1): {Bool(Text(target, "mode") == "execute")}.";
                attrs[1] = $"requiresDryRunFalse(r1): {Bool(dryRun.ValueKind == JsonValueKind.False)}.";
                attrs[2] = $"missingRequiredArgument(r1): {Bool(missing != 0)}.";
                declarations.AddRange(["F1Tool(t1).", "F1Action(ac1)."]);
                attrs.Add($"toolAvailable(t1): {Bool(Text(target, "status") == "available")}.");
                attrs.Add($"requiresDryRun(ac1): {Bool(action is "dry_run_reroute" or "dry_run_patch")}.");
                edges.UnionWith(["toolRef(a1, t1).", "targetTool(r1, t1).", "supports(t1, ac1).", "action(r1, ac1)."]);
            }
            else
            {
                Require(facts.GetProperty("tool").ValueKind == JsonValueKind.Null);
                var target = facts.GetProperty("work_order");
                Require(target.GetProperty("actor_crew_referenced").GetBoolean()
                    && Text(target, "crew_id") == "crew_aster_repair_02"
                    && Text(target, "crew_status") is "available" or "unavailable"
                    && Text(target, "assigned_actor_id") == "A1" && Text(target, "crew_home_sector_id") == "Aster"
                    && target.GetProperty("crew_system_refs").ValueKind != JsonValueKind.Null
                    && Text(target, "target_location") == "Aster/Grid-Spine-03"
                    && Text(target, "task_type") == "inspect_and_patch"
                    && Field(target.GetProperty("target_crew_id"), "target_crew_id").GetString() == "crew_aster_repair_02"
                    && Text(target.GetProperty("target_crew_pool_id"), "presence") == "absent"
                    && !target.GetProperty("options").EnumerateArray().Any(row => Text(row, "coverage") == "unsupported"));
                declarations.AddRange(["F1Crew(c1).", "F1Location(loc1)."]);
                attrs.Add($"crewAvailable(c1): {Bool(Text(target, "crew_status") == "available")}.");
                edges.UnionWith(["crewRef(a1, c1).", "targetCrew(r1, c1).", "assignedActor(c1, a1).",
                    "crewHomeSector(c1, s1).", "targetLocation(r1, loc1)."]);
            }
            declarations.AddRange(edges);
            declarations.AddRange(attrs);
            var modelText = new UTF8Encoding(false, true).GetString(model);
            var instanceText = modelText.TrimEnd() + "\n" + string.Join("\n", declarations) + "\n";
            var bytes = Encoding.UTF8.GetBytes(instanceText);
            Require(bytes.Length <= 64_000);
            var witnessStatements = new HashSet<string>(StringComparer.Ordinal);
            foreach (var line in declarations)
            {
                if (line.StartsWith("!exists(", StringComparison.Ordinal) || line.StartsWith("default !", StringComparison.Ordinal)
                    || edges.Contains(line) || Classes.Any(name => line.StartsWith(name + "(", StringComparison.Ordinal)))
                    witnessStatements.Add(line);
                else if (Regex.Match(line, @"^(\w+\(\w+\)): (true|false)\.$") is var boolean && boolean.Success)
                    witnessStatements.Add((boolean.Groups[2].Value == "false" ? "!" : "") + boolean.Groups[1].Value + ".");
                else if (line.StartsWith("fractionOrZero(r1): ", StringComparison.Ordinal)) witnessStatements.Add(line);
            }
            return new ClosedInstance(bytes, model.ToArray(), AsterF1GatedExecution.Hash(bytes), nodes, witnessStatements);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or OverflowException or DecoderFallbackException or ArgumentException)
        {
            throw new AsterF1P1Exception("instance_fact_invalid");
        }
    }

    private static string Bool(bool value) => value ? "true" : "false";

    internal static bool WitnessIsClosed(byte[] raw, ClosedInstance instance)
    {
        // Refinery 0.3.0 emits the model source before the concrete witness. The
        // source must match the independently pinned model byte-for-byte; only
        // the remaining lines may be interpreted as generated declarations.
        if (raw.Length is 0 or > 65_536 || raw.Length <= instance.ModelPrefix.Length
            || !raw.AsSpan().StartsWith(instance.ModelPrefix)) return false;
        var suffix = raw.AsSpan(instance.ModelPrefix.Length);
        if (suffix.Length < 2 || suffix[0] != (byte)'\n' || suffix[^1] != (byte)'\n') return false;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(suffix[1..]); }
        catch (DecoderFallbackException) { return false; }
        if (text.Contains("unknown", StringComparison.OrdinalIgnoreCase) || text.Contains("?exists", StringComparison.Ordinal)) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = text.Split('\n');
        if (lines.Length < 2 || lines[^1].Length != 0) return false;
        for (var index = 0; index < lines.Length - 1; index++)
        {
            var line = lines[index];
            if (line.Length == 0) return false;
            if (index == 0)
            {
                if (!line.StartsWith("declare ", StringComparison.Ordinal) || !line.EndsWith(".", StringComparison.Ordinal))
                    return false;
                var names = line[8..^1].Split(", ", StringSplitOptions.None);
                if (names.Length != 5 || names.Distinct(StringComparer.Ordinal).Count() != 5
                    || !instance.Nodes.SetEquals(names)) return false;
            }
            else if (!instance.WitnessStatements.Contains(line) || !seen.Add(line)) return false;
        }
        return seen.SetEquals(instance.WitnessStatements);
    }
}
