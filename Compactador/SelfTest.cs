using System.Text.Json;

namespace Compactador;

static class SelfTest
{
    public static async Task Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var report = new List<object>();
        try
        {
            var source = Path.Combine(folder, "teste-original.mkv");
            if (!File.Exists(source)) await Engine.Run("ffmpeg", ["-v", "error", "-f", "lavfi", "-i", "testsrc2=size=1280x720:rate=24", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000", "-t", "8", "-c:v", "libx264", "-crf", "12", "-c:a", "aac", source]);
            var j = new Job { Source = source }; await Engine.Analyze(j);
            var available = await Engine.DetectEncoders();
            report.Add(new { detection = available });
            foreach (var backend in EncodingBackend.All.Where(b => available.Any(a => a.Id == b.Id && a.Available)))
            {
                var result = Path.Combine(folder, "teste-" + backend.Id + ".mkv");
                await Engine.Encode(j, result, 1, backend, false, _ => { });
                await Engine.Verify(result, j.Duration);
                var output = new Job { Source = result }; await Engine.Analyze(output);
                if (output.Codec != "hevc") throw new Exception("Codec inesperado.");
                report.Add(new { backend = backend.Name, originalBytes = j.Size, resultBytes = output.Size, originalDuration = j.Duration, resultDuration = output.Duration, integrity = "passed" });
            }
            if (!available.Any(a => a.Id == "cpu" && a.Available)) throw new Exception("Codificação por processador indisponível.");
            var sample = Path.Combine(folder, "teste-amostra.mkv");
            await Engine.EncodeCompatible(j, sample, 1, "auto", available, true, _ => { }, _ => { });
            await Engine.Verify(sample, Math.Min(30, j.Duration * .6));
            report.Add(new { sample = "passed" });
            var corrupt = Path.Combine(folder, "corrompido.mkv"); await File.WriteAllTextAsync(corrupt, "invalid video");
            bool rejected = false; try { await Engine.Verify(corrupt, 8); } catch { rejected = true; }
            if (!rejected) throw new Exception("Arquivo inválido não foi rejeitado.");
            report.Add(new { invalidFileRejected = true });
            // Simulate a driver that passed startup detection but fails with a real film.
            var retry = Path.Combine(folder, "teste-recuperacao.mkv");
            var fakeAvailability = new[] { new EncoderAvailability("nvidia", "NVIDIA", true, "simulado") };
            var attempts = new List<string>();
            var used = await Engine.EncodeCompatible(j, retry, 1, "auto", fakeAvailability, false, _ => { }, _ => { }, async (b, path) =>
            {
                attempts.Add(b.Id);
                if (b.Id == "nvidia") { await File.WriteAllTextAsync(path, "partial hardware output"); throw new Engine.ProcessFailure("Falha de driver simulada"); }
                await Engine.Encode(j, path, 1, b, false, _ => { });
            });
            await Engine.Verify(retry, j.Duration);
            if (used.Id != "cpu" || !attempts.SequenceEqual(new[] { "nvidia", "cpu" }) || Directory.EnumerateFiles(folder, "*.attempt").Any()) throw new Exception("Recuperação de falha não passou.");
            if (EncodingSelection.Candidates("auto", [])[0].Id != "cpu" || EncodingSelection.Candidates("amd", available.Where(a => a.Id != "amd").ToArray())[0].Id != "cpu") throw new Exception("Seleção sem hardware falhou.");
            var noGpu = Path.Combine(folder, "teste-sem-gpu.mkv");
            var noGpuUsed = await Engine.EncodeCompatible(j, noGpu, 1, "amd", [], true, _ => { }, _ => { });
            await Engine.Verify(noGpu, Math.Min(30, j.Duration * .6));
            if (noGpuUsed.Id != "cpu") throw new Exception("Alternativa sem GPU falhou.");
            bool existingRejected = false;
            try { await Engine.EncodeCompatible(j, source, 1, "auto", available, false, _ => { }, _ => { }); } catch (IOException) { existingRejected = true; }
            if (!existingRejected || new FileInfo(source).Length != j.Size) throw new Exception("Proteção contra sobrescrita falhou.");
            report.Add(new { hardwareFailureFallback = "passed", unavailableHardwareFallback = "passed", originalProtected = true, temporaryFilesCleaned = true });
            var restored = JsonSerializer.Deserialize<MainWindow.Saved>(JsonSerializer.Serialize(new MainWindow.Saved([j], folder, 1, true, "auto")));
            var legacy = JsonSerializer.Deserialize<MainWindow.Saved>("{\"Jobs\":[],\"Destination\":\"videos\",\"Profile\":1,\"Gpu\":false}");
            if (restored == null || restored.Jobs.Count != 1 || restored.EncodingMode != "auto" || legacy == null || legacy.EncodingMode != null || legacy.Gpu) throw new Exception("Persistência/migração da fila falhou.");
            report.Add(new { queueSerializationAndLegacyMigration = "passed" });
            await File.WriteAllTextAsync(Path.Combine(folder, "relatorio.json"), JsonSerializer.Serialize(new { passed = true, checks = report }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) { await File.WriteAllTextAsync(Path.Combine(folder, "relatorio.json"), JsonSerializer.Serialize(new { passed = false, error = e.ToString(), checks = report })); Environment.ExitCode = 1; }
    }
}
