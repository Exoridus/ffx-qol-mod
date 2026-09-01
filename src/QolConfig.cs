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

    /// <summary>
    ///     Take the boot straight to the title screen. Turning it off is not only a preference: the
    ///     opening demo is the one scene that shows a campfire and an animated weapon texture at the
    ///     same time, which makes it the shortest test there is for the particle timeline and the
    ///     texture animation step.
    /// </summary>
    [JsonPropertyName("skip_splash")]
    public bool SkipSplash { get; init; } = true;

    public static QolConfig Load(string path)
    {
        if (!File.Exists(path)) return new QolConfig();
        return JsonSerializer.Deserialize<QolConfig>(File.ReadAllText(path)) ?? new QolConfig();
    }

    /// <summary>
    ///     Where the config actually sits, which is beside this assembly in the mod's own directory.
    ///     AppContext.BaseDirectory is the host's bin directory, not the mod's, so a config written
    ///     next to the DLL was silently never read and every run took the defaults.
    /// </summary>
    public static string ResolvePath()
    {
        string assembly = typeof(QolConfig).Assembly.Location;

        return !string.IsNullOrEmpty(assembly) && Path.GetDirectoryName(assembly) is { Length: > 0 } dir
            ? Path.Combine(dir, FileName)
            : Path.Combine(AppContext.BaseDirectory, FileName);
    }

    public const string FileName = "fhqol.config.json";
}
