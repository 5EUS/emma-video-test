#if PLUGIN_TRANSPORT_WASM
using System.IO;
using System.Text.Json;
using EMMA.Plugin.Common;
using EMMA.PluginTemplate.Infrastructure;
using LibraryWorld;
using LibraryWorld.wit.exports.emma.plugin;
using LibraryWorld.wit.imports.emma.plugin;

namespace LibraryWorld.wit.exports.emma.plugin;

/// <summary>
/// WIT export bridge that adapts typed component calls to template program handlers.
/// </summary>
public static class PluginImpl
{
    private static readonly PluginOperationPayloadRouter InvokePayloadRouter = BuildInvokePayloadRouter();
    private const string VideoSeriesCollectionId = "video-series-space-odyssey";
    private const string VideoSeason1Episode1Id = "video-series-space-odyssey-s01e01";
    private const string VideoSeason1Episode2Id = "video-series-space-odyssey-s01e02";
    private const string VideoSeason1Episode3Id = "video-series-space-odyssey-s01e03";
    private const string VideoSeason2Episode1Id = "video-series-space-odyssey-s02e01";
    private const string VideoSeason2Episode2Id = "video-series-space-odyssey-s02e02";
    private const string VideoSeason2Episode3Id = "video-series-space-odyssey-s02e03";
    private const string DefaultSingleUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";
    private const string DefaultMulti1080Uri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4";
    private const string DefaultMulti720Uri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4";
    private const string DefaultMulti480Uri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerFun.mp4";
    private const string DefaultSegmentUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyrides.mp4";

    private sealed record StreamFixture(string Id, string Label, string PlaylistUri);

    public static IPlugin.HandshakeResponse Handshake()
    {
        var handshake = EMMA.PluginTemplate.Program.handshake();
        return new IPlugin.HandshakeResponse(handshake.version, handshake.message);
    }

    public static List<IPlugin.Capability> Capabilities()
    {
        var capabilities = EMMA.PluginTemplate.Program.capabilities();
        return [.. capabilities.Select(capability => new IPlugin.Capability(
            capability.name,
            [.. capability.mediaTypes],
            [.. capability.operations]))];
    }

    public static List<IPlugin.MediaSearchItem> Search(string query, string payloadJson)
    {
        payloadJson = ResolvePayload(payloadJson, "search", ProviderRequestUrls.BuildSearchAbsoluteUrl(query));
        var items = EMMA.PluginTemplate.Program.search(query, payloadJson);

        return [.. items.Select(item => new IPlugin.MediaSearchItem(
            item.id,
            item.source,
            item.title,
            item.mediaType,
            item.thumbnailUrl,
            item.description,
            []))];
    }

    public static List<IPlugin.ChapterItem> Chapters(string mediaId, string payloadJson)
    {
        payloadJson = ResolvePayload(payloadJson, "chapters", ProviderRequestUrls.BuildChaptersAbsoluteUrl(mediaId));
        var items = EMMA.PluginTemplate.Program.chapters(mediaId, payloadJson);

        return [.. items.Select(item => new IPlugin.ChapterItem(
            item.id,
            checked((uint)item.number),
            item.title,
            [.. item.uploaderGroups ?? []]))];
    }

    public static IPlugin.PageItem? Page(string mediaId, string chapterId, uint pageIndex, string payloadJson)
    {
        payloadJson = ResolvePayload(payloadJson, "page", ProviderRequestUrls.BuildAtHomeAbsoluteUrl(chapterId));

        var page = EMMA.PluginTemplate.Program.page(mediaId, chapterId, pageIndex, payloadJson);
        if (page is null)
        {
            return null;
        }

        return new IPlugin.PageItem(page.id, checked((uint)page.index), page.contentUri);
    }

    public static List<IPlugin.PageItem> Pages(string mediaId, string chapterId, uint startIndex, uint count, string payloadJson)
    {
        payloadJson = ResolvePayload(payloadJson, "pages", ProviderRequestUrls.BuildAtHomeAbsoluteUrl(chapterId));

        var pages = EMMA.PluginTemplate.Program.pages(mediaId, chapterId, startIndex, count, payloadJson);
        return [.. pages.Select(page => new IPlugin.PageItem(page.id, checked((uint)page.index), page.contentUri))];
    }

    public static List<IPlugin.VideoStreamItem> VideoStreams(string mediaId, string payloadJson)
    {
        var streamsByMediaId = BuildStreamsByMediaId();
        if (!streamsByMediaId.TryGetValue(mediaId, out var streams))
        {
            return [];
        }

        return [.. streams.Select(stream => new IPlugin.VideoStreamItem(stream.Id, stream.Label, stream.PlaylistUri))];
    }

    public static IPlugin.VideoSegmentItem? VideoSegment(string mediaId, string streamId, uint sequence, string payloadJson)
    {
        if (string.Equals(mediaId, "video-segment-basic", StringComparison.OrdinalIgnoreCase)
            && string.Equals(streamId, "segment-main", StringComparison.OrdinalIgnoreCase)
            && sequence <= 4)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(
                $"SEGMENT|media={mediaId}|stream={streamId}|seq={sequence}");
            return new IPlugin.VideoSegmentItem("video/mp2t", payload);
        }

        return null;
    }

    public static IPlugin.MediaOperationResponse Invoke(IPlugin.MediaOperationRequest request)
    {
        var payload = ResolveInvokePayload(request);

        var result = EMMA.PluginTemplate.Program.invoke(new OperationRequest(
            request.operation,
            request.mediaId,
            request.mediaType,
            request.argsJson,
            payload));

        if (result.isError)
        {
            throw CreateOperationError(result.error);
        }

        return new IPlugin.MediaOperationResponse(result.contentType, result.payloadJson);
    }

    private static string? ResolveInvokePayload(IPlugin.MediaOperationRequest request)
    {
        var operationRequest = new OperationRequest(
            request.operation,
            request.mediaId,
            request.mediaType,
            request.argsJson,
            request.payloadJson);

        return InvokePayloadRouter.Resolve(
            operationRequest,
            (operation, hint) => HostBridgeInterop.OperationPayload(operation, hint),
            useArgsJsonFallbackHint: true);
    }

    private static string ResolvePayload(string payloadJson, string operation, string? payloadUrl)
    {
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            return payloadJson;
        }

        if (string.IsNullOrWhiteSpace(payloadUrl))
        {
            return string.Empty;
        }

        return HostBridgeInterop.OperationPayload(operation, payloadUrl) ?? string.Empty;
    }

    private static WitException<IPlugin.OperationError> CreateOperationError(string? error)
    {
        if (!PluginOperationError.TryParse(error, out var parsed))
        {
            return new WitException<IPlugin.OperationError>(IPlugin.OperationError.Failed("operation failed"), 0);
        }

        return parsed.Kind switch
        {
            PluginOperationErrorKind.UnsupportedOperation => new WitException<IPlugin.OperationError>(
                IPlugin.OperationError.UnsupportedOperation(parsed.Message),
                0),
            PluginOperationErrorKind.InvalidArguments => new WitException<IPlugin.OperationError>(
                IPlugin.OperationError.InvalidArguments(parsed.Message),
                0),
            _ => new WitException<IPlugin.OperationError>(
                IPlugin.OperationError.Failed(parsed.Message),
                0)
        };
    }

    private static PluginOperationPayloadRouter BuildInvokePayloadRouter()
    {
        return new PluginOperationPayloadRouter()
            .Register("search", request => ProviderRequestUrls.BuildSearchAbsoluteUrl(PluginSearchQuery.Parse(request.argsJson)))
            .Register("benchmark-network", request => ProviderRequestUrls.BuildSearchAbsoluteUrl(PluginSearchQuery.Parse(request.argsJson)))
            .Register("chapters", request => ProviderRequestUrls.BuildChaptersAbsoluteUrl(request.ResolveMediaId()))
            .Register("page", request => ProviderRequestUrls.BuildAtHomeAbsoluteUrl(request.ResolveChapterId()))
            .Register("pages", request => ProviderRequestUrls.BuildAtHomeAbsoluteUrl(request.ResolveChapterId()));
    }

    private static string GetEnvOrDefault(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? ResolveLocalFileUri(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var trimmed = configuredPath.Trim();
        if (!Path.IsPathRooted(trimmed))
        {
            trimmed = Path.GetFullPath(trimmed);
        }

        if (!File.Exists(trimmed))
        {
            return null;
        }

        return new Uri(trimmed).AbsoluteUri;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StreamFixture>> BuildStreamsByMediaId()
    {
        var singleUri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_SINGLE_URI", DefaultSingleUri);
        var multi1080Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_1080_URI", DefaultMulti1080Uri);
        var multi720Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_720_URI", DefaultMulti720Uri);
        var multi480Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_480_URI", DefaultMulti480Uri);
        var segmentUri = GetEnvOrDefault("EMMA_VIDEO_TEST_SEGMENT_URI", DefaultSegmentUri);
        var localFileUri = ResolveLocalFileUri(Environment.GetEnvironmentVariable("EMMA_VIDEO_TEST_LOCAL_FILE_PATH"));

        return new Dictionary<string, IReadOnlyList<StreamFixture>>(StringComparer.OrdinalIgnoreCase)
        {
            ["video-hls-single"] = [new StreamFixture("single-main", "Main", singleUri)],
            ["video-movie-nightfall"] = [new StreamFixture("movie-main", "Movie", singleUri)],
            ["video-hls-multi"] =
            [
                new StreamFixture("multi-1080p", "1080p", multi1080Uri),
                new StreamFixture("multi-720p", "720p", multi720Uri),
                new StreamFixture("multi-480p", "480p", multi480Uri),
            ],
            ["video-segment-basic"] = [new StreamFixture("segment-main", "Segment Main", segmentUri)],
            [VideoSeason1Episode1Id] = [new StreamFixture("s1e1-main", "1080p", singleUri)],
            [VideoSeason1Episode2Id] = [new StreamFixture("s1e2-main", "1080p", multi1080Uri)],
            [VideoSeason1Episode3Id] = [new StreamFixture("s1e3-main", "720p", multi720Uri)],
            [VideoSeason2Episode1Id] = [new StreamFixture("s2e1-main", "1080p", multi1080Uri)],
            [VideoSeason2Episode2Id] = [new StreamFixture("s2e2-main", "720p", multi720Uri)],
            [VideoSeason2Episode3Id] = [new StreamFixture("s2e3-main", "480p", multi480Uri)],
            ["video-empty-streams"] = [],
            ["video-local-file"] = string.IsNullOrWhiteSpace(localFileUri)
                ? []
                : [new StreamFixture("local-file-main", "Local File", localFileUri)],
            [VideoSeriesCollectionId] = [],
        };
    }
}
#endif
