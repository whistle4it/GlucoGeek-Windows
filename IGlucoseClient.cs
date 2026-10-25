namespace GlucoGeek;

internal interface IGlucoseClient : IDisposable
{
    Task<Snapshot> Fetch(CancellationToken ct);
    static IGlucoseClient Create(Settings settings) => settings.Provider switch
    {
        "LibreLinkUp" => new LibreLinkClient(settings),
        "Dexcom G7" => new DexcomClient(settings),
        _ => throw new InvalidDataException("Unknown glucose provider. Check Settings.")
    };
}
