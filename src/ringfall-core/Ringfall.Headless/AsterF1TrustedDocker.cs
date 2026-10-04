using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ringfall.Core.Formal;

// The operator freezes this external record after the repaired source candidate is
// frozen. Neither the Python child nor the invocation is permitted to mint it.
internal sealed class AsterF1OperatorAnchor
{
    internal const string Image = "ghcr.io/graphs4value/refinery-cli@sha256:5d7eacdef0ddfb98e264cad405badf96753c68203e498a12d91331a0aa96ad57";
    internal const string Platform = "linux/amd64";
    internal string Python { get; }
    internal string Docker { get; }
    internal string SourceAggregate { get; }
    internal string ModelHash { get; }
    internal string ManifestHash { get; }
    internal string RecordHash { get; }

    private AsterF1OperatorAnchor(string python, string docker, string aggregate, string model,
        string manifest, string record)
    {
        Python = python; Docker = docker; SourceAggregate = aggregate; ModelHash = model;
        ManifestHash = manifest; RecordHash = record;
    }

    internal static string ExpectedPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RingFall", "anchors", "aster-f1-preflight-v1.json");

    internal static AsterF1OperatorAnchor Load(string root, string manifestPath)
    {
        var path = ExpectedPath;
        var file = new FileInfo(path);
        if (!file.Exists || !file.IsReadOnly || (file.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new AsterF1P1Exception("operator_anchor_missing_or_untrusted");
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new AsterF1P1Exception("operator_anchor_untrusted_path");
        var raw = File.ReadAllBytes(path);
        if (raw.Length is 0 or > 65_536) throw new AsterF1P1Exception("operator_anchor_invalid");
        using var record = JsonDocument.Parse(raw);
        var source = record.RootElement;
        if (source.GetProperty("record_type").GetString() != "AsterF1OperatorFreeze"
            || source.GetProperty("version").GetString() != "1.0"
            || source.GetProperty("image").GetString() != Image
            || source.GetProperty("platform").GetString() != Platform)
            throw new AsterF1P1Exception("operator_anchor_invalid");
        var python = VerifiedExecutable(source.GetProperty("python"));
        var docker = VerifiedExecutable(source.GetProperty("docker"));
        var expectedManifest = source.GetProperty("source_manifest_sha256").GetString();
        var manifestBytes = File.ReadAllBytes(manifestPath);
        if (expectedManifest != Hash(manifestBytes)) throw new AsterF1P1Exception("operator_source_drift");
        using var manifestDoc = JsonDocument.Parse(manifestBytes);
        var files = manifestDoc.RootElement.GetProperty("source_files");
        var expectedFiles = source.GetProperty("source_files");
        var pinned = expectedFiles.EnumerateObject().ToArray();
        var actualEntries = files.EnumerateObject().ToArray();
        if (pinned.Length != actualEntries.Length || pinned.Where((item, index) =>
            item.Name != actualEntries[index].Name || item.Value.GetString() != actualEntries[index].Value.GetString()).Any())
            throw new AsterF1P1Exception("operator_source_drift");
        var lines = new List<string>();
        foreach (var entry in files.EnumerateObject())
        {
            var relative = entry.Name.Replace('/', Path.DirectorySeparatorChar);
            var absolute = Path.GetFullPath(Path.Combine(root, relative));
            if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || (new FileInfo(absolute).Attributes & FileAttributes.ReparsePoint) != 0)
                throw new AsterF1P1Exception("operator_source_untrusted_path");
            var actual = Hash(File.ReadAllBytes(absolute));
            if (actual != entry.Value.GetString()) throw new AsterF1P1Exception("operator_source_drift");
            lines.Add(entry.Name + "=" + actual);
        }
        var aggregate = Hash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        if (aggregate != source.GetProperty("source_aggregate").GetString()
            || aggregate != manifestDoc.RootElement.GetProperty("source_aggregate").GetString())
            throw new AsterF1P1Exception("operator_source_drift");
        var model = Path.Combine(root, "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem");
        var modelHash = Hash(File.ReadAllBytes(model));
        if (modelHash != source.GetProperty("model_sha256").GetString()
            || modelHash != manifestDoc.RootElement.GetProperty("model_sha256").GetString())
            throw new AsterF1P1Exception("operator_model_drift");
        return new AsterF1OperatorAnchor(python, docker, aggregate, modelHash, expectedManifest!, Hash(raw));
    }

    internal void Recheck(string root, string manifestPath)
    {
        var next = Load(root, manifestPath);
        if (next.RecordHash != RecordHash || next.SourceAggregate != SourceAggregate
            || next.Python != Python || next.Docker != Docker)
            throw new AsterF1P1Exception("operator_anchor_drift");
    }

    private static string VerifiedExecutable(JsonElement entry)
    {
        var path = entry.GetProperty("path").GetString() ?? "";
        if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != path)
            throw new AsterF1P1Exception("operator_executable_untrusted");
        var file = new FileInfo(path);
        if (!file.Exists || (file.Attributes & FileAttributes.ReparsePoint) != 0
            || Hash(File.ReadAllBytes(path)) != entry.GetProperty("sha256").GetString())
            throw new AsterF1P1Exception("operator_executable_drift");
        return path;
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal sealed class AsterF1TrustedDocker
{
    private readonly string executable;
    private readonly Func<Process, int, bool> postKillWait;

    internal AsterF1TrustedDocker(string executable)
        : this(executable, static (process, milliseconds) => process.WaitForExit(milliseconds)) { }

    // Internal control for the observed post-kill settlement result. The command
    // path uses the single-argument constructor and always waits on the real process.
    internal AsterF1TrustedDocker(string executable, Func<Process, int, bool> postKillWait)
    {
        this.executable = executable;
        this.postKillWait = postKillWait;
    }

    internal sealed record Captured(int ExitCode, byte[] Stdout, byte[] Stderr, string[] Arguments)
    {
        internal object Receipt(string invocationId, string instanceHash) => new
        {
            invocation_id = invocationId, instance_sha256 = instanceHash, exit_code = ExitCode,
            stdout_sha256 = AsterF1OperatorAnchor.Hash(Stdout), stderr_sha256 = AsterF1OperatorAnchor.Hash(Stderr),
            stdout_bytes = Stdout.Length, stderr_bytes = Stderr.Length
        };
    }

    internal Captured Invoke(string[] args, int seconds = 15, string? ownedContainer = null)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = true, CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new AsterF1P1Exception("docker_unavailable");
        process.StandardInput.Close();
        var output = Drain(process.StandardOutput.BaseStream);
        var error = Drain(process.StandardError.BaseStream);
        try
        {
            var finished = Task.WhenAll(process.WaitForExitAsync(), output, error);
            if (!finished.Wait(TimeSpan.FromSeconds(seconds))) throw new AsterF1P1Exception("docker_timeout");
            var result = new Captured(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult(), args);
            if (result.Stdout.Length + result.Stderr.Length > 65_536)
                throw new AsterF1P1Exception("docker_output_limit");
            return result;
        }
        catch (Exception exception) when (exception is AggregateException or AsterF1P1Exception or IOException)
        {
            var settled = false;
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                settled = postKillWait(process, 5_000);
            }
            catch (Exception failure) when (failure is InvalidOperationException or System.ComponentModel.Win32Exception)
            { /* Still attempt this invocation's named container cleanup. */ }
            if (ownedContainer is not null) VerifyRemoved(ownedContainer);
            if (!settled) throw new AsterF1P1Exception(ownedContainer is null
                ? "process_cleanup_incomplete" : "adapter_cleanup_uncertain");
            throw new AsterF1P1Exception("docker_capture_failed");
        }
    }

    private static async Task<byte[]> Drain(Stream stream)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(chunk)) != 0)
        {
            if (buffer.Length + count > 65_536) throw new IOException("docker_output_limit");
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }

    internal void VerifyRemoved(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"\Aringfall-p2-[0-9a-f]{32}\z"))
            throw new AsterF1P1Exception("container_cleanup_unattributable");
        try
        {
            Invoke(["rm", "--force", name], 8);
            var absent = Invoke(["inspect", "--type", "container", name], 8);
            var healthy = Invoke(["version", "--format", "{{.Server.Version}}"], 8);
            // Pinned CLI absence response, captured with an owned nonexistent name.
            // Inspect exit 1 alone can also mean a daemon or inspection error.
            var namedAbsence = Encoding.UTF8.GetBytes(
                "Error response from daemon: {\"message\":\"No such container: " + name + "\"}\n");
            if (absent.ExitCode != 1 || !absent.Stdout.AsSpan().SequenceEqual("[]\n"u8)
                || !absent.Stderr.AsSpan().SequenceEqual(namedAbsence)
                || healthy.ExitCode != 0 || !healthy.Stdout.AsSpan().SequenceEqual("28.0.1\n"u8)
                || healthy.Stderr.Length != 0)
                throw new AsterF1P1Exception("adapter_cleanup_uncertain");
        }
        catch (Exception exception) when (exception is AsterF1P1Exception or System.ComponentModel.Win32Exception)
        {
            throw new AsterF1P1Exception("adapter_cleanup_uncertain");
        }
    }

    internal string InspectPinnedImage()
    {
        var version = Invoke(["version", "--format", "{{.Server.Version}} {{.Server.Os}}/{{.Server.Arch}}"]);
        if (version.ExitCode != 0 || Encoding.UTF8.GetString(version.Stdout).Trim() != "28.0.1 linux/amd64")
            throw new AsterF1P1Exception("docker_engine_mismatch");
        var inspect = Invoke(["image", "inspect", AsterF1OperatorAnchor.Image,
            "--format", "{{.Os}}/{{.Architecture}} {{json .RepoDigests}}"]);
        if (inspect.ExitCode != 0) throw new AsterF1P1Exception("digest_image_unavailable");
        var text = Encoding.UTF8.GetString(inspect.Stdout).Trim();
        var separator = text.IndexOf(' ');
        if (separator < 0 || text[..separator] != AsterF1OperatorAnchor.Platform)
            throw new AsterF1P1Exception("digest_or_platform_mismatch");
        using var digests = JsonDocument.Parse(text[(separator + 1)..]);
        if (!digests.RootElement.EnumerateArray().Any(item => item.GetString() == AsterF1OperatorAnchor.Image))
            throw new AsterF1P1Exception("digest_or_platform_mismatch");
        return Encoding.UTF8.GetString(version.Stdout).Trim();
    }

    internal Captured Run(string directory, string name, string[] command)
    {
        InspectPinnedImage();
        return Invoke(["run", "--pull=never", "--platform", AsterF1OperatorAnchor.Platform, "--rm",
            "--network", "none", "--cpus", "2", "--memory", "2g", "--pids-limit", "128",
            "--name", name, "--mount", "type=bind,source=" + directory + ",target=/data,readonly",
            AsterF1OperatorAnchor.Image, .. command], 90, name);
    }
}
