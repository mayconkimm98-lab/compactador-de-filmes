namespace Compactador;

public sealed record EncodingBackend(string Id, string Name, string Encoder)
{
    public static readonly EncodingBackend Nvidia = new("nvidia", "NVIDIA NVENC", "hevc_nvenc");
    public static readonly EncodingBackend Intel = new("intel", "Intel Quick Sync", "hevc_qsv");
    public static readonly EncodingBackend Amd = new("amd", "AMD AMF", "hevc_amf");
    public static readonly EncodingBackend Cpu = new("cpu", "Processador", "libx265");
    public static readonly EncodingBackend[] All = [Nvidia, Intel, Amd, Cpu];

    public string[] Arguments(int profile)
    {
        if (profile is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(profile));
        var quality = new[] { "20", "24", "28" }[profile];
        return Id switch
        {
            "nvidia" => ["-preset", "p5", "-rc", "vbr", "-cq", quality, "-b:v", "0"],
            "intel" => ["-preset", "medium", "-global_quality", quality],
            "amd" => ["-quality", "quality", "-rc", "cqp", "-qp_i", quality, "-qp_p", quality],
            "cpu" => ["-preset", "medium", "-crf", quality],
            _ => throw new ArgumentException("Codificador desconhecido.")
        };
    }
}

public sealed record EncoderAvailability(string Id, string Name, bool Available, string Detail);

public sealed record EncodingChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public static class EncodingSelection
{
    // Deliberately deterministic. A short compatibility test is not a speed benchmark.
    public static EncodingBackend[] Candidates(string mode, IReadOnlyList<EncoderAvailability> available)
    {
        if (mode == "cpu") return [EncodingBackend.Cpu];
        if (mode != "auto" && !EncodingBackend.All.Any(b => b.Id == mode)) throw new ArgumentException("Modo de compressão inválido.");
        var hardware = EncodingBackend.All.Where(b => b.Id != "cpu" && (mode == "auto" || mode == b.Id)
            && available.Any(a => a.Id == b.Id && a.Available));
        return [.. hardware, EncodingBackend.Cpu];
    }
}
