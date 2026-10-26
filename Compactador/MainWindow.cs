using System.Diagnostics;
using System.Text.Json;

namespace Compactador;

public sealed class MainWindow : Form
{
    readonly List<Job> jobs = [];
    readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    readonly ComboBox profile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 185 };
    readonly ComboBox encoder = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    List<EncoderAvailability> availability = [];
    readonly Label hardware = new() { Text = "Verificando compatibilidade…", AutoSize = true, Padding = new Padding(10, 6, 10, 6) };
    readonly TextBox destination = new() { Width = 400 };
    readonly ProgressBar bar = new() { Dock = DockStyle.Top, Height = 22 };
    readonly Label status = new() { Text = "Adicione filmes ou uma pasta para começar.", Dock = DockStyle.Fill, AutoSize = false };
    readonly FlowLayoutPanel controls = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
    bool busy, stop;
    readonly string state = Path.Combine(Engine.Data, "fila.json");
    public MainWindow()
    {
        Text = "Compactador de Filmes"; Width = 1100; Height = 700; MinimumSize = new Size(900, 550); Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterScreen;
        list.Columns.Add("Filme", 340); list.Columns.Add("Análise / andamento", 355); list.Columns.Add("Tamanho", 105); list.Columns.Add("Duração", 100);
        profile.Items.AddRange(["Máxima qualidade", "Equilibrado", "Menor tamanho"]); profile.SelectedIndex = 1;
        FillEncoders("auto");
        Directory.CreateDirectory(Engine.Data);
        destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Filmes compactados");
        AddButton("Adicionar filmes", async () => { using var d = new OpenFileDialog { Multiselect = true, Filter = "Vídeos|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.m4v;*.ts;*.webm|Todos|*.*" }; if (d.ShowDialog() == DialogResult.OK) await AddFiles(d.FileNames); });
        AddButton("Adicionar pasta", async () => { using var d = new FolderBrowserDialog(); if (d.ShowDialog() == DialogResult.OK) await AddFiles(Directory.EnumerateFiles(d.SelectedPath, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Where(p => new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm" }.Contains(Path.GetExtension(p).ToLowerInvariant()))); });
        AddButton("Remover selecionados", () => { foreach (ListViewItem item in list.SelectedItems) jobs.Remove((Job)item.Tag!); Save(); RefreshList(); return Task.CompletedTask; });
        controls.Controls.Add(profile); controls.Controls.Add(encoder);
        AddButton("Compatibilidade", () => { MessageBox.Show(string.Join("\n\n", availability.Select(a => $"{a.Name}: {(a.Available ? "disponível" : "indisponível")}\n{a.Detail}")), "Diagnóstico de codificação"); return Task.CompletedTask; });
        AddButton("Localizar FFmpeg", async () => { using var d = new FolderBrowserDialog { Description = "Selecione a pasta que contém ffmpeg.exe e ffprobe.exe" }; if (d.ShowDialog() != DialogResult.OK) return; if (!File.Exists(Path.Combine(d.SelectedPath, "ffmpeg.exe")) || !File.Exists(Path.Combine(d.SelectedPath, "ffprobe.exe"))) { MessageBox.Show("A pasta precisa conter ffmpeg.exe e ffprobe.exe."); return; } Engine.ToolsDirectory = d.SelectedPath; File.WriteAllText(Path.Combine(Engine.Data, "ferramentas.txt"), d.SelectedPath); await CheckCompatibility(); });
        var dest = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) }; dest.Controls.Add(new Label { Text = "Destino:", AutoSize = true }); dest.Controls.Add(destination);
        var choose = new Button { Text = "Escolher…", AutoSize = true }; choose.Click += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog() == DialogResult.OK) destination.Text = d.SelectedPath; }; dest.Controls.Add(choose);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10) };
        foreach (var (text, sample) in new[] { ("Testar trecho selecionado", true), ("Comprimir fila", false) }) { var b = new Button { Text = text, AutoSize = true }; b.Click += async (_, _) => await ProcessQueue(sample); actions.Controls.Add(b); }
        var pause = new Button { Text = "Parar após o atual", AutoSize = true }; pause.Click += (_, _) => { stop = true; status.Text = "A fila vai parar após concluir e verificar o filme atual."; }; actions.Controls.Add(pause);
        var open = new Button { Text = "Abrir destino", AutoSize = true }; open.Click += (_, _) => { Directory.CreateDirectory(destination.Text); Process.Start(new ProcessStartInfo(destination.Text) { UseShellExecute = true }); }; actions.Controls.Add(open);
        var compare = new Button { Text = "Abrir original / resultado", AutoSize = true }; compare.Click += (_, _) => { if (list.SelectedItems.Count == 0) return; var j = (Job)list.SelectedItems[0].Tag!; foreach (var path in new[] { j.Source, j.Output }.Where(File.Exists)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }; actions.Controls.Add(compare);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 85, Padding = new Padding(12) }; footer.Controls.Add(status); footer.Controls.Add(bar);
        var hardwarePanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true }; hardwarePanel.Controls.Add(hardware);
        Controls.Add(list); Controls.Add(footer); Controls.Add(actions); Controls.Add(dest); Controls.Add(hardwarePanel); Controls.Add(controls);
        try { if (File.Exists(state)) { var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(state)); if (saved != null) { jobs.AddRange(saved.Jobs); destination.Text = saved.Destination; profile.SelectedIndex = Math.Clamp(saved.Profile, 0, 2); FillEncoders(saved.EncodingMode ?? (saved.Gpu ? "auto" : "cpu")); } foreach (var j in jobs.Where(j => j.Status is "Comprimindo" or "Verificando")) j.Status = "Interrompido — pronto para retomar"; } } catch (Exception e) { status.Text = "Não foi possível recuperar a fila: " + e.Message; }
        try { var config = Path.Combine(Engine.Data, "ferramentas.txt"); if (File.Exists(config)) Engine.ToolsDirectory = File.ReadAllText(config).Trim(); } catch (IOException) { }
        RefreshList();
        Shown += async (_, _) => await CheckCompatibility();
        FormClosing += (_, e) => { if (busy) { e.Cancel = true; stop = true; MessageBox.Show("O trabalho atual continuará até concluir. A fila será parada em seguida. Aguarde para fechar."); } else Save(); };
    }
    void AddButton(string text, Func<Task> action) { var b = new Button { Text = text, AutoSize = true }; b.Click += async (_, _) => { if (busy) return; try { await action(); } catch (Exception e) { MessageBox.Show(e.Message); } }; controls.Controls.Add(b); }
    public sealed record Saved(List<Job> Jobs, string Destination, int Profile, bool Gpu, string? EncodingMode = null);
    string EncodingMode => (encoder.SelectedItem as EncodingChoice)?.Id ?? "auto";
    void FillEncoders(string selected)
    {
        encoder.Items.Clear(); encoder.Items.Add(new EncodingChoice("auto", "Automático (recomendado)"));
        foreach (var b in EncodingBackend.All) { var tested = availability.FirstOrDefault(a => a.Id == b.Id); encoder.Items.Add(new EncodingChoice(b.Id, b.Name + (tested == null ? "" : tested.Available ? " — disponível" : " — indisponível"))); }
        encoder.SelectedItem = encoder.Items.Cast<EncodingChoice>().FirstOrDefault(c => c.Id == selected) ?? encoder.Items[0];
    }
    async Task CheckCompatibility()
    {
        busy = true; controls.Enabled = false; hardware.Text = "Testando codificação NVIDIA, Intel, AMD e processador…";
        try
        {
            await Engine.Run("ffprobe", ["-version"], timeout: TimeSpan.FromSeconds(10));
            availability = await Engine.DetectEncoders(); var selected = EncodingMode; FillEncoders(selected);
            var usable = availability.Where(a => a.Available).Select(a => a.Name).ToArray();
            hardware.Text = usable.Length == 0 ? "Nenhum codificador disponível. Consulte Compatibilidade ou localize outra versão do FFmpeg." : "Disponível neste computador: " + string.Join(" • ", usable);
            status.Text = "No modo automático, uma falha de aceleração faz o vídeo reiniciar com outra opção, até o processador.";
        }
        catch (Exception e) { availability = []; hardware.Text = "FFmpeg/FFprobe indisponível. Use Localizar FFmpeg para selecionar os dois executáveis."; status.Text = e.Message; }
        finally { busy = false; controls.Enabled = true; }
    }
    void Save() { var temp = state + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(new Saved(jobs, destination.Text, profile.SelectedIndex, EncodingMode != "cpu", EncodingMode))); File.Move(temp, state, true); }
    void RefreshList() { list.BeginUpdate(); list.Items.Clear(); foreach (var j in jobs) { var row = new ListViewItem(Path.GetFileName(j.Source)) { Tag = j }; row.SubItems.Add(j.Status); row.SubItems.Add($"{j.Size / 1048576d:N1} MB"); row.SubItems.Add(TimeSpan.FromSeconds(j.Duration).ToString(@"hh\:mm\:ss")); list.Items.Add(row); } list.EndUpdate(); }
    async Task AddFiles(IEnumerable<string> paths)
    {
        busy = true; controls.Enabled = false;
        try { foreach (var path in paths.ToArray()) { if (jobs.Any(j => j.Source.Equals(path, StringComparison.OrdinalIgnoreCase))) continue; var j = new Job { Source = path }; jobs.Add(j); try { await Engine.Analyze(j); j.Status = $"{j.Codec.ToUpperInvariant()} • {j.Bitrate:N1} Mb/s • " + (j.Codec is "hevc" or "av1" || j.Bitrate < 2 ? "já eficiente; teste recomendado" : "candidato à redução"); } catch (Exception e) { j.Status = "Erro de análise: " + e.Message; } RefreshList(); Save(); } }
        finally { busy = false; controls.Enabled = true; }
    }
    async Task ProcessQueue(bool sample)
    {
        if (busy) return;
        var selected = list.SelectedItems.Count > 0 ? (Job)list.SelectedItems[0].Tag! : null;
        if (sample && selected == null) { MessageBox.Show("Selecione um filme para testar um trecho de até 30 segundos."); return; }
        var queue = sample ? new[] { selected! } : jobs.Where(j => !j.Status.StartsWith("Concluído") && !j.Status.StartsWith("Sem redução") && !j.Status.StartsWith("Erro de análise")).ToArray();
        busy = true; stop = false; controls.Enabled = false; destination.ReadOnly = true;
        var folder = destination.Text; var chosenProfile = profile.SelectedIndex; var mode = EncodingMode;
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var j in queue)
            {
                string? partial = null;
                try
                {
                    await Engine.Analyze(j);
                    var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!).AvailableFreeSpace;
                    if (free < j.Size * 1.15) throw new Exception("Espaço livre insuficiente para processar com segurança.");
                    var output = Path.Combine(folder, Path.GetFileNameWithoutExtension(j.Source) + (sample ? " - amostra - " : " - compacto - ") + Guid.NewGuid().ToString("N")[..8] + ".mkv"); partial = output + ".partial";
                    var previousStatus = j.Status;
                    j.Status = "Comprimindo"; Save(); RefreshList(); var timer = Stopwatch.StartNew();
                    var activeBackend = "";
                    var used = await Engine.EncodeCompatible(j, partial, chosenProfile, mode, availability, sample,
                        ratio => { bar.Value = (int)(ratio * 100); status.Text = $"{Path.GetFileName(j.Source)} • {activeBackend} • {ratio:P0}" + (ratio > .01 ? $" • restante estimado: {TimeSpan.FromSeconds(timer.Elapsed.TotalSeconds * (1 - ratio) / ratio):hh\\:mm\\:ss}" : ""); },
                        notice => { activeBackend = notice; timer.Restart(); status.Text = notice; });
                    j.Backend = used.Name;
                    if (!sample) { j.Status = "Verificando"; RefreshList(); status.Text = "Conferindo duração e decodificando o arquivo completo…"; await Engine.Verify(partial, j.Duration); }
                    else await Engine.Verify(partial, Math.Min(30, j.Duration * .6));
                    var bytes = new FileInfo(partial).Length;
                    if (!sample && bytes >= j.Size) { File.Delete(partial); j.Status = "Sem redução — resultado descartado; original preservado"; }
                    else { File.Move(partial, output); j.Output = output; if (!sample) j.Status = $"Concluído • {used.Name} • economia de {1 - (double)bytes / j.Size:P1}"; else { j.Status = previousStatus; var estimate = bytes / Math.Min(30, j.Duration * .6) * j.Duration; status.Text = $"Amostra ({used.Name}): {bytes / 1048576d:N1} MB • economia estimada: {1 - estimate / j.Size:P1}. Compare no ponto {TimeSpan.FromSeconds(j.Duration * .35):hh\\:mm\\:ss} do original. A estimativa pode variar."; } }
                }
                catch (Exception e) { j.Status = "Erro — " + e.Message.Split('\n')[0]; File.AppendAllText(Path.Combine(Engine.Data, "erros.log"), $"{DateTime.Now:u} {j.Source}\n{e}\n"); status.Text = "Falha registrada. Consulte erros.log na pasta de dados do aplicativo."; }
                finally { if (partial != null && File.Exists(partial)) File.Delete(partial); Save(); RefreshList(); }
                if (stop) break;
            }
            if (!sample) status.Text = stop ? "Fila parada. Clique em Comprimir fila para continuar." : "Fila finalizada. Confira a situação de cada filme.";
        }
        catch (Exception e) { MessageBox.Show(e.Message); }
        finally { busy = false; controls.Enabled = true; destination.ReadOnly = false; bar.Value = 0; }
    }
}
