#if PLUGIN_TRANSPORT_WASM
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EMMA.Plugin.Common;

namespace EMMA.VideoTest.Infrastructure;

internal sealed class WasmPluginOperationHost
    : PluginBasicPagedVideoWasmOperationHost<WasmChapterOperationItem>
{
    private static readonly PluginBasicPagedWasmHostOptions<WasmChapterOperationItem> HostOptions = new(
        HandshakeVersion: "1.0.0",
        HandshakeMessage: "EMMA wasm component ready",
        CapabilityProfile: PluginCapabilityProfile.PagedVideoAudio,
        HandshakeTypeInfo: WasmJsonContext.Default.HandshakeResponse,
        CapabilityTypeInfo: WasmJsonContext.Default.CapabilityItemArray,
        SearchTypeInfo: WasmJsonContext.Default.SearchItemArray,
        ChapterTypeInfo: WasmJsonContext.Default.ChapterItemArray,
        ChapterInvokeTypeInfo: WasmJsonContext.Default.WasmChapterOperationItemArray,
        PageTypeInfo: WasmJsonContext.Default.PageItem,
        PageArrayTypeInfo: WasmJsonContext.Default.PageItemArray,
        OperationResultTypeInfo: WasmJsonContext.Default.OperationResult,
        BenchmarkTypeInfo: WasmJsonContext.Default.BenchmarkResult,
        NetworkBenchmarkTypeInfo: WasmJsonContext.Default.NetworkBenchmarkResult);

    private readonly WasmClient _client = new();
    private readonly CoreClient _core = new();

    public WasmPluginOperationHost()
        : base(HostOptions)
    {
    }

    protected override JsonTypeInfo<VideoStreamOperationItem[]> VideoStreamArrayTypeInfo =>
        WasmJsonContext.Default.VideoStreamOperationItemArray;

    protected override JsonTypeInfo<VideoSegmentOperationItem> VideoSegmentTypeInfo =>
        WasmJsonContext.Default.VideoSegmentOperationItem;

    protected override SearchItem[] Search(PluginSearchQuery parsedQuery, string payloadJson)
    {
        var query = parsedQuery.Query;
        if (PluginEnvironment.IsDevelopmentMode())
        {
            Console.WriteLine($"[SEARCH] Called with query='{query}' (empty={string.IsNullOrWhiteSpace(query)})");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            if (PluginEnvironment.IsDevelopmentMode())
            {
                Console.WriteLine("[SEARCH] Returning fixture catalog for empty query");
            }

            var fixtures = _core.SearchFixtures(string.Empty);
            return [.. fixtures];
        }

        payloadJson ??= string.Empty;

        if (PluginEnvironment.IsDevelopmentMode())
        {
            Console.WriteLine($"[SEARCH] Parsing payload for query='{query}'");
        }

        var parseMapResult = _client.SearchFromPayload(query, payloadJson);
        if (PluginEnvironment.IsDevelopmentMode())
        {
            Console.WriteLine($"[SEARCH] Parse completed, got {parseMapResult.Results.Count} results");
        }

        return [.. parseMapResult.Results];
    }

    protected override string? FetchSearchPayload(PluginSearchQuery parsedQuery) =>
        _client.FetchSearchPayload(parsedQuery.Query);

    protected override (IReadOnlyList<SearchItem> Results, long ParseMs, long MapMs) SearchFromPayloadWithTimings(string payloadJson)
    {
        var result = _client.SearchFromPayload(string.Empty, payloadJson);
        return (result.Results, result.ParseMs, result.MapMs);
    }

    protected override string? FetchChaptersPayload(string mediaId) => _client.FetchChaptersPayload(mediaId);

    protected override IReadOnlyList<ChapterItem> GetChaptersFromPayload(string mediaId, string payloadJson) =>
        _client.GetChaptersFromPayload(mediaId, payloadJson ?? string.Empty);

    protected override IReadOnlyList<ChapterOperationItem> GetChapterOperationItemsFromPayload(string mediaId, string payloadJson) =>
        _client.GetChapterOperationItemsFromPayload(mediaId, payloadJson ?? string.Empty);

    protected override WasmChapterOperationItem MapChapterOperationItem(ChapterOperationItem item) =>
        new(item.id, item.number, item.title, [.. item.uploaderGroups ?? []]);

    protected override string? FetchAtHomePayload(string chapterId) => _client.FetchAtHomePayload(chapterId);

    protected override PageItem? GetPageFromPayload(string chapterId, int pageIndex, string payloadJson) =>
        _client.GetPageFromPayload(chapterId, pageIndex, payloadJson);

    protected override IReadOnlyList<PageItem> GetPagesFromPayload(string chapterId, int startIndex, int count, string payloadJson) =>
        _client.GetPagesFromPayload(chapterId, startIndex, count, payloadJson);

    protected override IReadOnlyList<VideoStreamOperationItem> GetVideoStreams(string mediaId)
    {
        return _core.GetFixtureStreams(mediaId)
            .Select(static stream => new VideoStreamOperationItem(
                stream.Id,
                stream.Label,
                stream.PlaylistUri,
                stream.RequestHeaders,
                stream.RequestCookies,
                stream.StreamType,
                stream.IsLive,
                stream.DrmProtected,
                stream.DrmScheme,
                stream.AudioTracks?.Select(static track => new VideoTrackOperationItem(
                    track.Id,
                    track.Label,
                    track.Language,
                    track.Codec,
                    track.IsDefault)).ToArray(),
                stream.SubtitleTracks?.Select(static track => new VideoTrackOperationItem(
                    track.Id,
                    track.Label,
                    track.Language,
                    track.Codec,
                    track.IsDefault)).ToArray(),
                stream.DefaultAudioTrackId,
                stream.DefaultSubtitleTrackId))
            .ToArray();
    }

    protected override VideoSegmentOperationItem? GetVideoSegment(string mediaId, string streamId, uint sequence)
    {
        var segment = _core.GetFixtureSegment(mediaId, streamId, sequence);
        return segment is null
            ? null
            : new VideoSegmentOperationItem(
                segment.Value.ContentType,
                Convert.ToBase64String(segment.Value.Payload));
    }

    protected override string Benchmark(string[] args)
    {
        var iterations = 5000;
        if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
        {
            iterations = Math.Clamp(parsed, 1, 1_000_000);
        }

        long checksum = 1469598103934665603;
        const ulong prime = 1099511628211;
        var generated = 0;

        for (var i = 0; i < iterations; i++)
        {
            var text = $"bench:{i}:{(i * 31) % 97}";
            foreach (var rune in text.EnumerateRunes())
            {
                checksum ^= rune.Value;
                checksum = (long)((ulong)checksum * prime);
            }

            generated += text.Length;
        }

        var result = new BenchmarkResult(
            iterations,
            checksum,
            generated,
            0);

        return JsonSerializer.Serialize(
            result,
            WasmJsonContext.Default.BenchmarkResult);
    }

    protected override string BenchmarkNetwork(string[] args, string stdinPayload)
    {
        var query = args.Length > 0 ? args[0] : "one piece";
        var payloadJson = PluginPayloadResolvers.ResolveProvidedOrFetched(
            WasmClient.ResolvePayloadContent(stdinPayload),
            () => _client.FetchSearchPayload(query));

        var payloadBytes = System.Text.Encoding.UTF8.GetByteCount(payloadJson ?? string.Empty);
        var itemCount = 0;

        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var data = PluginJsonElement.GetArray(doc.RootElement, "data");
            itemCount = data?.GetArrayLength() ?? 0;
        }

        var result = new NetworkBenchmarkResult(
            query,
            payloadBytes,
            itemCount,
            0);

        return JsonSerializer.Serialize(
            result,
            WasmJsonContext.Default.NetworkBenchmarkResult);
    }
}
#endif
