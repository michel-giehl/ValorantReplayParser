using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Replay.Encoding.Archives;
using Replay.Valorant;

namespace CliReader.JsonExport;

internal sealed class ReplayExportRunner
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ReplayExportManifestWriter _manifestWriter;

    public ReplayExportRunner(
        ILoggerFactory loggerFactory,
        ReplayExportManifestWriter manifestWriter)
    {
        _loggerFactory = loggerFactory;
        _manifestWriter = manifestWriter;
    }

    public void Run(ExportOptions options)
    {
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        File.Delete(Path.Combine(outputDirectory, "manifest.json"));

        using var file = File.OpenRead(options.ReplayPath);
        var sourceSize = file.Length;
        var sourceSha256 = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
        file.Position = 0;

        var sink = ReplayExportSink.Create(outputDirectory);
        ValorantReplayReadResult result;
        try
        {
            using var archive = new FBinaryArchive(file);
            result = ValorantReplayReader.CreateDefault(
                _loggerFactory,
                sink,
                options.ParseProfile).Read(archive);
        }
        finally
        {
            sink.Dispose();
        }

        _manifestWriter.Write(
            outputDirectory,
            options.ReplayPath,
            sourceSha256,
            sourceSize,
            options.ProfileName,
            result,
            sink.Statistics);
    }
}
