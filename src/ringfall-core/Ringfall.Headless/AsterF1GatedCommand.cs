using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ringfall.Core.Formal;

internal static class AsterF1GatedCommand
{
    private static readonly string[] Flags = ["--state", "--candidate", "--pulse", "--context", "--out"];
    private static readonly Dictionary<string, string> NamedIssues = new(StringComparer.Ordinal)
    {
        ["toolUnavailable"] = "tool_unavailable", ["crewUnavailable"] = "crew_unavailable",
        ["toolExecuteDenied"] = "tool_execute_denied", ["requiresDryRunConflict"] = "tool_requires_dry_run_conflict",
        ["toolArgumentsMissing"] = "tool_arguments_missing", ["loadFractionNonPositive"] = "tool_argument_value_invalid",
        ["loadFractionTooHigh"] = "tool_macro_surface_denied"
    };

    internal static int Run(string[] args)
    {
        static int Fail(string reason)
        {
            Console.Error.WriteLine("gate_error:" + reason);
            return 1;
        }
        if (args.Length != 11) return Fail("usage_invalid");
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
            if (!Flags.Contains(args[index], StringComparer.Ordinal) || !paths.TryAdd(args[index], args[index + 1])
                || string.IsNullOrWhiteSpace(args[index + 1])) return Fail("usage_invalid");
        if (paths.Count != Flags.Length) return Fail("usage_invalid");

        try
        {
            var output = AsterF1GatedExecution.PrivateOutput(paths["--out"]);
            var root = FindRoot();
            var adapter = Path.Combine(root, "src", "ringfall-core", "formal", "aster-f1-v0.1", "run_preflight_v1.py");
            var manifest = Path.Combine(root, "src", "ringfall-core", "formal", "aster-f1-v0.1", "successor-preflight-v1.json");
            var anchor = AsterF1OperatorAnchor.Load(root, manifest);
            var inputPaths = Flags[..4].ToDictionary(flag => flag, flag => Path.GetFullPath(paths[flag]), StringComparer.Ordinal);
            if (inputPaths.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
                return Fail("duplicate_input_path");
            var original = inputPaths.ToDictionary(pair => pair.Key, pair => ReadBounded(pair.Value), StringComparer.Ordinal);
            var preflight = AsterF1GatedExecution.Prepare(original["--state"], original["--candidate"],
                original["--pulse"], original["--context"]);
            if (output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return Fail("output_not_private");

            // The parent owns input, instance and receipt paths. Python only maps; Docker is
            // invoked and captured by this trusted parent using the operator-pinned executable.
            var invocation = Guid.NewGuid().ToString("N");
            var runDir = Path.Combine(Path.GetTempPath(), "opencode", "ringfall-gate-v1-" + invocation);
            Directory.CreateDirectory(runDir);
            var solverDir = Path.Combine(runDir, "solver");
            Directory.CreateDirectory(solverDir);
            var instancePath = Path.Combine(solverDir, "instance.problem");
            var preflightPath = Path.Combine(runDir, "preflight.json");
            File.WriteAllText(preflightPath, preflight.Json, new UTF8Encoding(false));
            foreach (var (flag, bytes) in original)
                File.WriteAllBytes(Path.Combine(runDir, flag[2..] + ".json"), bytes);
            // -I ignores PYTHONPATH and site customizations. The explicitly verified module
            // directory is added by fixed bootstrap text, not by ambient PATH/import search.
            var python = new AsterF1TrustedDocker(anchor.Python);
            var mapping = python.Invoke(["-I", "-B", "-c",
                "import sys,runpy;sys.path.insert(0,sys.argv[1]);sys.argv=sys.argv[2:];runpy.run_path(sys.argv[0],run_name='__main__')",
                Path.GetDirectoryName(adapter)!, adapter, "--preflight", preflightPath,
                "--state", Path.Combine(runDir, "state.json"), "--candidate", Path.Combine(runDir, "candidate.json"),
                "--pulse", Path.Combine(runDir, "pulse.json"), "--context", Path.Combine(runDir, "context.json"),
                "--instance", instancePath], 30);
            if (mapping.Stdout.Length == 0 || mapping.Stderr.Length != 0)
                return Fail("mapping_output_invalid");
            using var mappingDoc = JsonDocument.Parse(mapping.Stdout);
            var mapped = mappingDoc.RootElement;
            if (mapping.ExitCode != 0)
                return Fail(string.Join(",", mapped.GetProperty("diagnostics").EnumerateArray()
                    .Select(item => item.GetString() ?? "mapping_failed")));
            var modelBytes = File.ReadAllBytes(Path.Combine(root, "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem"));
            var expected = AsterF1GatedProof.Map(preflight, modelBytes);
            if (mapped.GetProperty("record_type").GetString() != "AsterF1PreflightMapping"
                || mapped.GetProperty("version").GetString() != "1.0"
                || mapped.GetProperty("source_aggregate").GetString() != anchor.SourceAggregate
                || mapped.GetProperty("model_sha256").GetString() != anchor.ModelHash
                || mapped.GetProperty("instance_sha256").GetString() != expected.Sha256
                || mapped.GetProperty("preflight_sha256").GetString() != preflight.Sha256
                || mapped.GetProperty("packet_id_sha256").GetString() != AsterF1GatedExecution.Hash(Encoding.UTF8.GetBytes(preflight.PacketId))
                || mapped.GetProperty("source_context_id_sha256").GetString() != AsterF1GatedExecution.Hash(Encoding.UTF8.GetBytes(preflight.ContextId))
                || mapped.GetProperty("schema_validation").GetString() != "not_run"
                || mapped.GetProperty("mutation_authorized").ValueKind != JsonValueKind.False
                || mapped.GetProperty("diagnostics").GetArrayLength() != 0
                || !ReadOwned(instancePath, runDir, 64_000).AsSpan().SequenceEqual(expected.Bytes))
                return Fail("gate_instance_mismatch");
            anchor.Recheck(root, manifest);
            foreach (var (flag, bytes) in original)
            {
                if (!ReadBounded(inputPaths[flag]).AsSpan().SequenceEqual(bytes)
                    || !ReadOwned(Path.Combine(runDir, flag[2..] + ".json"), runDir, 1_048_576).AsSpan().SequenceEqual(bytes)
                    || mapped.GetProperty("input_sha256").GetProperty(flag[2..]).GetString() != AsterF1GatedExecution.Hash(bytes))
                    return Fail("gate_input_drift");
            }
            var checkName = "ringfall-p2-" + Guid.NewGuid().ToString("N");
            var generateName = "ringfall-p2-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(runDir, "invocation.json"), JsonSerializer.Serialize(new
            {
                invocation_id = invocation, preflight_sha256 = preflight.Sha256, source_aggregate = anchor.SourceAggregate,
                instance_sha256 = expected.Sha256, check_container = checkName, generate_container = generateName
            }));
            var docker = new AsterF1TrustedDocker(anchor.Docker);
            var engine = docker.InspectPinnedImage();
            var check = docker.Run(solverDir, checkName, ["check", "-k", "instance.problem"]);
            var receipts = Path.Combine(runDir, "receipts");
            Directory.CreateDirectory(receipts);
            var checkReceipt = StoreCapture(receipts, "check", check, invocation, expected.Sha256, anchor.Docker);
            if (!ReadOwned(instancePath, runDir, 64_000).AsSpan().SequenceEqual(expected.Bytes))
                return Fail("gate_instance_drift");
            if (check.ExitCode != 0)
            {
                var matched = ParseNamedErrors(check);
                var issues = preflight.Decision.Issues.Select(issue => issue.Code).Order(StringComparer.Ordinal).ToArray();
                return Fail(matched is not null && matched.Order(StringComparer.Ordinal).SequenceEqual(issues)
                    ? "core_rejected:" + string.Join(",", matched) : "solver_result_ambiguous");
            }
            if (!check.Stdout.AsSpan().SequenceEqual("Model is consistent\n"u8) || check.Stderr.Length != 0)
                return Fail("solver_result_ambiguous");
            var generated = docker.Run(solverDir, generateName, ["generate", "-o", "-", "instance.problem"]);
            var generateReceipt = StoreCapture(receipts, "generate", generated, invocation, expected.Sha256, anchor.Docker);
            if (generated.ExitCode != 0 || generated.Stderr.Length != 0
                || !AsterF1GatedProof.WitnessIsClosed(generated.Stdout, expected))
                return Fail("witness_not_closed");
            if (preflight.Decision.Status != Ringfall.Core.Actions.AsterL1DecisionStatus.Allowed
                || preflight.Decision.Issues.Count != 0) return Fail("formal_core_disagreement");
            anchor.Recheck(root, manifest);
            if (!ReadOwned(instancePath, runDir, 64_000).AsSpan().SequenceEqual(expected.Bytes)
                || !ReadOwned(preflightPath, runDir, 1_048_576).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(preflight.Json))
                || !ReceiptsUnchanged(receipts, check, generated, checkReceipt, generateReceipt))
                return Fail("gate_receipt_mismatch");
            foreach (var (flag, bytes) in original)
                if (!ReadBounded(inputPaths[flag]).AsSpan().SequenceEqual(bytes)) return Fail("gate_input_drift");
            var report = JsonSerializer.SerializeToUtf8Bytes(new
            {
                record_type = "AsterF1PreflightSolverReport", version = "1.0", invocation_id = invocation,
                operator_anchor_sha256 = anchor.RecordHash, preflight_sha256 = preflight.Sha256,
                packet_id_sha256 = AsterF1GatedExecution.Hash(Encoding.UTF8.GetBytes(preflight.PacketId)),
                source_context_id_sha256 = AsterF1GatedExecution.Hash(Encoding.UTF8.GetBytes(preflight.ContextId)),
                input_sha256 = original.ToDictionary(item => item.Key[2..], item => AsterF1GatedExecution.Hash(item.Value)),
                source_aggregate = anchor.SourceAggregate, model_sha256 = anchor.ModelHash,
                instance_sha256 = expected.Sha256, executed_image = AsterF1OperatorAnchor.Image,
                inspected_platform = AsterF1OperatorAnchor.Platform, engine,
                check = check.Receipt(invocation, expected.Sha256), generated = generated.Receipt(invocation, expected.Sha256),
                witness_closed = true, verdict = "valid", agreement = "agreed",
                diagnostics = Array.Empty<string>(), schema_validation = "not_run", mutation_authorized = false
            });
            var bundle = AsterF1GatedExecution.Publish(preflight, report, output, expected, check.Stdout, generated.Stdout);
            AsterF1GatedExecution.EmitPublished(bundle, Path.Combine(runDir, "publication.json"), invocation,
                anchor.SourceAggregate, Console.WriteLine);
            return 0;
        }
        catch (AsterF1P1Exception exception) { return Fail(exception.Code); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or JsonException or KeyNotFoundException or System.ComponentModel.Win32Exception)
        { return Fail("gate_local_failure"); }
    }

    private static byte[] ReadOwned(string path, string runDir, long limit)
    {
        if (!Path.GetFullPath(path).StartsWith(runDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new AsterF1P1Exception("gate_owned_path_invalid");
        for (var parent = new FileInfo(path).Directory; parent is not null && parent.FullName.StartsWith(runDir, StringComparison.OrdinalIgnoreCase);
            parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new AsterF1P1Exception("gate_owned_path_invalid");
        var file = new FileInfo(path);
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length > limit)
            throw new AsterF1P1Exception("gate_owned_path_invalid");
        return File.ReadAllBytes(path);
    }

    private static string[]? ParseNamedErrors(AsterF1TrustedDocker.Captured run)
    {
        if (run.ExitCode != 1 || run.Stderr.Length != 0) return null;
        string text;
        try { text = new UTF8Encoding(false, true).GetString(run.Stdout); }
        catch (DecoderFallbackException) { return null; }
        if (!Regex.IsMatch(text, @"\AInconsistencies found in model:\n\n(?:\t[A-Za-z]\w*\(r1\): error\.\n)+\n?\z")) return null;
        var names = Regex.Matches(text, @"\t([A-Za-z]\w*)\(r1\): error\.")
            .Select(match => match.Groups[1].Value).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Any(name => !NamedIssues.ContainsKey(name))) return null;
        return names.Select(name => NamedIssues[name]).ToArray();
    }

    private static string StoreCapture(string directory, string name, AsterF1TrustedDocker.Captured capture,
        string invocation, string instanceHash, string executable)
    {
        File.WriteAllBytes(Path.Combine(directory, name + ".stdout.bin"), capture.Stdout);
        File.WriteAllBytes(Path.Combine(directory, name + ".stderr.bin"), capture.Stderr);
        var rawReceipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            invocation_id = invocation, instance_sha256 = instanceHash, executable,
            arguments = capture.Arguments, exit_code = capture.ExitCode,
            stdout_sha256 = AsterF1GatedExecution.Hash(capture.Stdout), stderr_sha256 = AsterF1GatedExecution.Hash(capture.Stderr)
        });
        File.WriteAllBytes(Path.Combine(directory, name + ".receipt.json"), rawReceipt);
        return AsterF1GatedExecution.Hash(rawReceipt);
    }

    private static bool ReceiptsUnchanged(string directory, AsterF1TrustedDocker.Captured check,
        AsterF1TrustedDocker.Captured generated, string checkReceiptHash, string generateReceiptHash)
    {
        foreach (var (name, capture, receiptHash) in new[] { ("check", check, checkReceiptHash),
            ("generate", generated, generateReceiptHash) })
        {
            foreach (var suffix in new[] { ".stdout.bin", ".stderr.bin", ".receipt.json" })
                if ((new FileInfo(Path.Combine(directory, name + suffix)).Attributes & FileAttributes.ReparsePoint) != 0)
                    return false;
            if (!ReadOwned(Path.Combine(directory, name + ".stdout.bin"), Path.GetDirectoryName(directory)!, 65_536).AsSpan().SequenceEqual(capture.Stdout)
                || !ReadOwned(Path.Combine(directory, name + ".stderr.bin"), Path.GetDirectoryName(directory)!, 65_536).AsSpan().SequenceEqual(capture.Stderr))
                return false;
            var raw = ReadOwned(Path.Combine(directory, name + ".receipt.json"), Path.GetDirectoryName(directory)!, 65_536);
            if (AsterF1GatedExecution.Hash(raw) != receiptHash) return false;
            using var receipt = JsonDocument.Parse(raw);
            if (receipt.RootElement.GetProperty("exit_code").GetInt32() != capture.ExitCode
                || receipt.RootElement.GetProperty("arguments").GetArrayLength() != capture.Arguments.Length
                || receipt.RootElement.GetProperty("stdout_sha256").GetString() != AsterF1GatedExecution.Hash(capture.Stdout)
                || receipt.RootElement.GetProperty("stderr_sha256").GetString() != AsterF1GatedExecution.Hash(capture.Stderr))
                return false;
        }
        return true;
    }

    private static byte[] ReadBounded(string path)
    {
        var length = new FileInfo(path).Length;
        if (length is <= 0 or > 1_048_576) throw new AsterF1P1Exception("input_invalid");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != length) throw new AsterF1P1Exception("gate_input_drift");
        return bytes;
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "ringfall-contracts"))
                && File.Exists(Path.Combine(directory.FullName, "src", "ringfall-core", "formal", "aster-f1-v0.1", "run_refinery.py")))
                return directory.FullName;
        throw new AsterF1P1Exception("source_root_unavailable");
    }

}
