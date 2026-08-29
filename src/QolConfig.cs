namespace Fahrenheit.Mods.Qol;

/// <summary>Settings read from <c>fhqol.config.json</c> beside the module.</summary>
public sealed record QolConfig
{
    /// <summary>
    ///     The word the axes hold at rest. Null leaves the focus fix in survey mode, where it only
    ///     reports the values it sees rather than writing any.
    /// </summary>
    [JsonPropertyName("axis_neutral")]
    public uint? AxisNeutral { get; init; }

    public static QolConfig Load(string path)
    {
        if (!File.Exists(path)) return new QolConfig();
        return JsonSerializer.Deserialize<QolConfig>(File.ReadAllText(path)) ?? new QolConfig();
    }
}
