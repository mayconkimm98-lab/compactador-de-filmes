using System.Diagnostics;
using System.Text.Json;

namespace Compactador;

public sealed class Job
{
    public string Source { get; set; } = "";
    public string Status { get; set; } = "Pendente";
    public string Output { get; set; } = "";
    public double Duration { get; set; }
    public long Size { get; set; }
    public string Codec { get; set; } = "";
    public double Bitrate { get; set; }
    public string Backend { get; set; } = "";
    public int VideoStream { get; set; }
}
public static class Engine
{
    public static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CompactadorFilmes");
    public static string ToolsDirectory { get; set; } = "";
    public static string Tool(string name)
    {
        foreach (var folder in new[] { ToolsDirectory, AppContext.BaseDirectory }.Where(p => !string.IsNullOrWhiteSpace(p)))
        { var path = Path.Combine(folder, name + ".exe"); if (File.Exists(path)) return path; }
        return name;
    }
    public sealed class ProcessFailure(string message) : Exception(message);
    public static async Task<string> Run(string tool, IEnumerable<string> args, Action<string>? progress = null, TimeSpan? timeout = null)
    {
        var info = new ProcessStartInfo(Tool(tool)) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var p = Process.Start(info) ?? throw new Exception("Não foi possível iniciar " + tool);
        using var deadline = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan);
        using var registration = deadline.Token.Register(() => { try { p.Kill(true); } catch (InvalidOperationException) { } });
        var errors = ReadErrors(p.StandardError);
        var lines = new System.Text.StringBuilder();
        while (await p.StandardOutput.ReadLineAsync() is { } line) { lines.AppendLine(line); if (progress != null && lines.Length > 65536) lines.Remove(0, lines.Length - 65536); progress?.Invoke(line); }
        await p.WaitForExitAsync();
        var error = await errors;
        if (deadline.IsCancellationRequested) throw new TimeoutException(tool + ": tempo limite excedido.");
        if (p.ExitCode != 0) throw new ProcessFailure(tool + ": " + error[^Math.Min(error.Length, 2500)..]);
        return lines.ToString();
    }
    static async Task<string> ReadErrors(StreamReader reader)
    {
        var text = new System.Text.StringBuilder(); var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0) { text.Append(buffer, 0, count); if (text.Length > 16384) text.Remove(0, text.Length - 16384); }
        return text.ToString();
    }
    public static async Task<List<EncoderAvailability>> DetectEncoders()
    {
        // Encode actual frames. Listing -encoders alone does not prove a usable GPU or driver.
        var results = new List<EncoderAvailability>();
        foreach (var backend in EncodingBackend.All)
        {
            try
            {
                await Run("ffmpeg", ["-v", "error", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=24", "-frames:v", "3", "-an", "-c:v", backend.Encoder, .. backend.Arguments(1), "-f", "null", "-"], timeout: TimeSpan.FromSeconds(20));
                results.Add(new(backend.Id, backend.Name, true, "Teste de codificação HEVC 8 bits aprovado. Formatos específicos são conferidos durante o processamento."));
            }
            catch (Exception e) when (e is ProcessFailure or TimeoutException) { results.Add(new(backend.Id, backend.Name, false, e.Message)); }
        }
        return results;
    }
    public static async Task Analyze(Job job)
    {
        using var doc = JsonDocument.Parse(await Run("ffprobe", ["-v", "error", "-show_format", "-show_streams", "-of", "json", job.Source], timeout: TimeSpan.FromSeconds(30)));
        var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var video = streams.FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video" && (!s.TryGetProperty("disposition", out var d) || d.GetProperty("attached_pic").GetInt32() == 0));
        if (video.ValueKind == JsonValueKind.Undefined) throw new Exception("Arquivo sem faixa de vídeo.");
        job.Codec = video.GetProperty("codec_name").GetString() ?? "";
        job.VideoStream = video.GetProperty("index").GetInt32();
        job.Duration = double.Parse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        if (job.Duration <= 0) throw new Exception("Duração inválida.");
        job.Size = new FileInfo(job.Source).Length;
        job.Bitrate = job.Size * 8 / job.Duration / 1000000;
    }
    public static async Task Encode(Job job, string output, int profile, EncodingBackend backend, bool sample, Action<double> progress)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-n" };
        args.AddRange(["-i", job.Source]);
        if (sample) args.AddRange(["-ss", (job.Duration * .35).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
        if (sample) args.AddRange(["-t", Math.Min(30, job.Duration * .6).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
        args.AddRange(["-map", $"0:{job.VideoStream}", "-map", "0:a?", "-map", "0:s?", "-map_metadata", "0", "-map_chapters", sample ? "-1" : "0", "-c:a", "copy", "-c:s", "copy", "-c:v", backend.Encoder]);
        args.AddRange(backend.Arguments(profile));
        args.AddRange(["-progress", "pipe:1", "-nostats", "-f", "matroska", output]);
        await Run("ffmpeg", args, line => { if (line.StartsWith("out_time_us=") && double.TryParse(line[12..], out var us)) progress(Math.Clamp(us / 1000000 / (sample ? Math.Min(30, job.Duration * .6) : job.Duration), 0, 1)); });
    }
    public static async Task<EncodingBackend> EncodeCompatible(Job job, string output, int profile, string mode,
        IReadOnlyList<EncoderAvailability> available, bool sample, Action<double> progress, Action<string> notice,
        Func<EncodingBackend, string, Task>? encodeAttempt = null)
    {
        if (File.Exists(output)) throw new IOException("O destino já existe. Escolha outro nome.");
        var candidates = EncodingSelection.Candidates(mode, available);
        if (mode is not "auto" and not "cpu" && candidates[0].Id == "cpu") notice("A opção escolhida está indisponível neste computador. Usando processador.");
        foreach (var backend in candidates)
        {
            var attempt = output + "." + Guid.NewGuid().ToString("N") + ".attempt";
            try
            {
                notice("Comprimindo com " + backend.Name); progress(0);
                if (encodeAttempt != null) await encodeAttempt(backend, attempt);
                else await Encode(job, attempt, profile, backend, sample, progress);
                File.Move(attempt, output); return backend;
            }
            catch (ProcessFailure e) when (backend.Id != "cpu")
            {
                Directory.CreateDirectory(Data);
                await File.AppendAllTextAsync(Path.Combine(Data, "erros.log"), $"{DateTime.Now:u} {backend.Name} falhou; tentando alternativa.\n{e.Message}\n");
                notice(backend.Name + " falhou. Reiniciando este vídeo com a próxima opção compatível.");
            }
            finally { if (File.Exists(attempt)) File.Delete(attempt); }
        }
        throw new InvalidOperationException("Nenhum codificador conseguiu concluir.");
    }
    public static async Task Verify(string path, double expected)
    {
        var probe = new Job { Source = path };
        await Analyze(probe);
        if (Math.Abs(probe.Duration - expected) > Math.Max(2, expected * .001)) throw new Exception("A duração do resultado difere do original.");
        await Run("ffmpeg", ["-v", "error", "-xerror", "-nostdin", "-i", path, "-map", "0:v:0", "-map", "0:a?", "-f", "null", "-"]);
    }
}
