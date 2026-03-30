#if PLUGIN_TRANSPORT_WASM
using System.Text.Json;
using EMMA.Plugin.Common;
using LibraryWorld.wit.exports.emma.plugin;

namespace EMMA.PluginTemplate.Infrastructure;

internal sealed class WasmClient
{
    private static readonly CoreClient Core = new();

    public string ExecuteOperationForCli(string operation, string[] args, string inputPayload)
    {
        try
        {
            return operation switch
            {
                "handshake" => JsonSerializer.Serialize(Handshake()),
                "capabilities" => JsonSerializer.Serialize(Capabilities()),
                "search" => JsonSerializer.Serialize(Search(args.Length > 0 ? args[0] : string.Empty, inputPayload)),
                "chapters" => JsonSerializer.Serialize(Chapters(args.Length > 0 ? args[0] : string.Empty, inputPayload)),
                "page" => SerializePageForCli(args, inputPayload),
                "pages" => SerializePagesForCli(args, inputPayload),
                "invoke" => SerializeInvokeForCli(args, inputPayload),
                _ => string.Empty
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    public HandshakeResponse Handshake()
    {
        return new HandshakeResponse("1.0.0", "EMMA wasm component ready");
    }

    public CapabilityItem[] Capabilities()
    {
        return
        [
            new CapabilityItem("search", ["video"], ["search", "invoke"]),
            new CapabilityItem("paged-navigation", ["video"], ["chapters", "page", "pages", "invoke"]),
            new CapabilityItem("media-operation", ["video"], ["invoke"]),
        ];
    }

    public SearchItem[] Search(string query, string payloadJson)
    {
        return [.. Core.SearchFixtures(query)];
    }

    public ChapterItem[] Chapters(string mediaId, string payloadJson)
    {
        return [.. Core.GetFixtureChapters(mediaId)];
    }

    public PageItem? Page(string mediaId, string chapterId, uint pageIndex, string payloadJson)
    {
        return null;
    }

    public PageItem[] Pages(string mediaId, string chapterId, uint startIndex, uint count, string payloadJson)
    {
        return [];
    }

    public VideoStreamItem[] VideoStreams(string mediaId, string payloadJson)
    {
        var streams = Core.GetFixtureStreams(mediaId);
        if (streams.Count == 0)
        {
            return [];
        }

        var result = new List<VideoStreamItem>(streams.Count);
        foreach (var stream in streams)
        {
            result.Add(new VideoStreamItem(stream.Id, stream.Label, stream.PlaylistUri));
        }

        return [.. result];
    }

    public VideoSegmentItem? VideoSegment(string mediaId, string streamId, uint sequence, string payloadJson)
    {
        var fixture = Core.GetFixtureSegment(mediaId, streamId, sequence);
        if (fixture is null)
        {
            return null;
        }

        return new VideoSegmentItem(fixture.Value.ContentType, fixture.Value.Payload);
    }

    public OperationResult Invoke(OperationRequest request)
    {
        var operation = request.NormalizedOperation();
        try
        {
            return operation switch
            {
                "search" => BuildOperationJsonResult(JsonSerializer.Serialize(Search(ResolveSearchQuery(request.argsJson), request.payloadJson ?? string.Empty))),
                "chapters" => BuildOperationJsonResult(JsonSerializer.Serialize(Chapters(SafeResolveMediaId(request), request.payloadJson ?? string.Empty))),
                "page" => BuildOperationJsonResult("null"),
                "pages" => BuildOperationJsonResult("[]"),
                "video-streams" => BuildOperationJsonResult(JsonSerializer.Serialize(VideoStreams(SafeResolveMediaId(request), request.payloadJson ?? string.Empty))),
                "video-segment" => BuildOperationJsonResult(JsonSerializer.Serialize(VideoSegment(
                    SafeResolveMediaId(request),
                    ResolveStreamId(request.argsJson),
                    ResolveSequence(request.argsJson),
                    request.payloadJson ?? string.Empty))),
                _ => OperationResult.UnsupportedOperation(operation),
            };
        }
        catch
        {
            return OperationResult.InvalidArguments("invalid operation arguments");
        }
    }

    private static OperationResult BuildOperationJsonResult(string payloadJson)
    {
        return new OperationResult(false, null, "application/json", payloadJson);
    }

    private static string ResolveSearchQuery(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
        {
            return string.Empty;
        }

        var parsed = PluginSearchQuery.Parse(argsJson);
        return parsed.Query ?? string.Empty;
    }

    private static string SafeResolveMediaId(OperationRequest request)
    {
        try
        {
            return request.ResolveMediaId();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveStreamId(string? argsJson)
    {
        return PluginJsonArgs.GetString(argsJson, "streamId") ?? string.Empty;
    }

    private static uint ResolveSequence(string? argsJson)
    {
        return PluginJsonArgs.GetUInt32(argsJson, "sequence") ?? 0;
    }

    private string SerializePageForCli(string[] args, string inputPayload)
    {
        if (args.Length < 3 || !uint.TryParse(args[2], out var pageIndex))
        {
            return string.Empty;
        }

        var page = Page(args[0], args[1], pageIndex, inputPayload);
        return page is null ? "null" : JsonSerializer.Serialize(page);
    }

    private string SerializePagesForCli(string[] args, string inputPayload)
    {
        if (args.Length < 4
            || !uint.TryParse(args[2], out var startIndex)
            || !uint.TryParse(args[3], out var count))
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(Pages(args[0], args[1], startIndex, count, inputPayload));
    }

    private string SerializeInvokeForCli(string[] args, string inputPayload)
    {
        if (args.Length == 0)
        {
            return JsonSerializer.Serialize(OperationResult.InvalidArguments("missing operation"));
        }

        var request = new OperationRequest(
            args[0],
            args.Length > 1 ? args[1] : null,
            args.Length > 2 ? args[2] : null,
            args.Length > 3 ? args[3] : null,
            inputPayload);

        return JsonSerializer.Serialize(Invoke(request));
    }
}

namespace LibraryWorld.wit.exports.emma.plugin;

public static class PluginImpl
{
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
        var items = EMMA.PluginTemplate.Program.chapters(mediaId, payloadJson);
        return [.. items.Select(item => new IPlugin.ChapterItem(
            item.id,
            checked((uint)item.number),
            item.title,
            [.. item.uploaderGroups ?? []]))];
    }

    public static IPlugin.PageItem? Page(string mediaId, string chapterId, uint pageIndex, string payloadJson)
    {
        var page = EMMA.PluginTemplate.Program.page(mediaId, chapterId, pageIndex, payloadJson);
        if (page is null)
        {
            return null;
        }

        return new IPlugin.PageItem(page.id, checked((uint)page.index), page.contentUri);
    }

    public static List<IPlugin.PageItem> Pages(string mediaId, string chapterId, uint startIndex, uint count, string payloadJson)
    {
        var pages = EMMA.PluginTemplate.Program.pages(mediaId, chapterId, startIndex, count, payloadJson);
        return [.. pages.Select(page => new IPlugin.PageItem(page.id, checked((uint)page.index), page.contentUri))];
    }

    public static List<IPlugin.VideoStreamItem> VideoStreams(string mediaId, string payloadJson)
    {
        var streams = EMMA.PluginTemplate.Program.videoStreams(mediaId, payloadJson);
        return [.. streams.Select(stream => new IPlugin.VideoStreamItem(stream.id, stream.label, stream.playlistUri))];
    }

    public static IPlugin.VideoSegmentItem? VideoSegment(string mediaId, string streamId, uint sequence, string payloadJson)
    {
        var segment = EMMA.PluginTemplate.Program.videoSegment(mediaId, streamId, sequence, payloadJson);
        if (segment is null)
        {
            return null;
        }

        return new IPlugin.VideoSegmentItem(segment.contentType, segment.payload);
    }

    public static IPlugin.MediaOperationResponse Invoke(IPlugin.MediaOperationRequest request)
    {
        var result = EMMA.PluginTemplate.Program.invoke(new OperationRequest(
            request.operation,
            request.mediaId,
            request.mediaType,
            request.argsJson,
            request.payloadJson));

        if (result.isError)
        {
            throw CreateOperationError(result.error);
        }

        return new IPlugin.MediaOperationResponse(result.contentType, result.payloadJson);
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
}
#endif