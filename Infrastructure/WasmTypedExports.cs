#if PLUGIN_TRANSPORT_WASM
using System.Text.Json;
using System.Text.Json.Serialization;
using EMMA.Plugin.Common;
using LibraryWorld;
using LibraryWorld.wit.exports.emma.plugin;
using LibraryWorld.wit.imports.emma.plugin;

namespace LibraryWorld.wit.exports.emma.plugin;

public static partial class PluginImpl
{
    public static IPlugin.HandshakeResponse Handshake()
    {
        var handshake = EMMA.TestPlugin.Program.handshake();
        return new IPlugin.HandshakeResponse(handshake.version, handshake.message);
    }

    public static List<IPlugin.Capability> Capabilities()
    {
        var capabilities = EMMA.TestPlugin.Program.capabilities();
        return [.. capabilities.Select(capability => new IPlugin.Capability(
            capability.name,
            [.. capability.mediaTypes],
            [.. capability.operations]))];
    }

    public static List<IPlugin.MediaSearchItem> Search(string query, string payloadJson)
    {
        payloadJson = ResolvePayload(payloadJson, "search", query);
        var items = EMMA.TestPlugin.Program.search(query, payloadJson);

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
        payloadJson = ResolvePayload(payloadJson, "chapters", mediaId);
        var items = EMMA.TestPlugin.Program.chapters(mediaId, payloadJson);

        return [.. items.Select(item => new IPlugin.ChapterItem(
            item.id,
            checked((uint)item.number),
            item.title))];
    }

    public static IPlugin.PageItem? Page(string mediaId, string chapterId, uint pageIndex, string payloadJson)
    {
        payloadJson = ResolvePayload(
            payloadJson,
            "page",
            JsonSerializer.Serialize(
                new PageRequestArgs(mediaId, chapterId, pageIndex),
                PluginImplJsonContext.Default.PageRequestArgs));

        var page = EMMA.TestPlugin.Program.page(mediaId, chapterId, pageIndex, payloadJson);
        if (page is null)
        {
            return null;
        }

        return new IPlugin.PageItem(page.id, checked((uint)page.index), page.contentUri);
    }

    public static List<IPlugin.PageItem> Pages(string mediaId, string chapterId, uint startIndex, uint count, string payloadJson)
    {
        payloadJson = ResolvePayload(
            payloadJson,
            "pages",
            JsonSerializer.Serialize(
                new PagesRequestArgs(mediaId, chapterId, startIndex, count),
                PluginImplJsonContext.Default.PagesRequestArgs));

        var pages = EMMA.TestPlugin.Program.pages(mediaId, chapterId, startIndex, count, payloadJson);
        return [.. pages.Select(page => new IPlugin.PageItem(page.id, checked((uint)page.index), page.contentUri))];
    }

    public static List<IPlugin.VideoStreamItem> VideoStreams(string mediaId, string payloadJson)
    {
        payloadJson = ResolvePayload(payloadJson, "video-streams", mediaId);
        var streams = EMMA.TestPlugin.Program.videoStreams(mediaId, payloadJson);
        return [.. streams.Select(stream => new IPlugin.VideoStreamItem(stream.id, stream.label, stream.playlistUri))];
    }

    public static IPlugin.VideoSegmentItem? VideoSegment(string mediaId, string streamId, uint sequence, string payloadJson)
    {
        payloadJson = ResolvePayload(
            payloadJson,
            "video-segment",
            JsonSerializer.Serialize(
                new VideoSegmentRequestArgs(mediaId, streamId, sequence),
                PluginImplJsonContext.Default.VideoSegmentRequestArgs));

        var segment = EMMA.TestPlugin.Program.videoSegment(mediaId, streamId, sequence, payloadJson);
        if (segment is null)
        {
            return null;
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(segment.payload);
        }
        catch (FormatException)
        {
            payload = [];
        }

        return new IPlugin.VideoSegmentItem(segment.contentType, [.. payload]);
    }

    public static IPlugin.MediaOperationResponse Invoke(IPlugin.MediaOperationRequest request)
    {
        var payload = ResolveInvokePayload(request);

        var result = EMMA.TestPlugin.Program.invoke(new OperationRequest(
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
        if (!string.IsNullOrWhiteSpace(request.payloadJson))
        {
            return request.payloadJson;
        }

        var operationName = request.operation ?? string.Empty;
        return HostBridgeInterop.OperationPayload(operationName, request.argsJson);
    }

    private static string ResolvePayload(string payloadJson, string operation, string? operationArgs)
    {
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            return payloadJson;
        }

        if (string.IsNullOrWhiteSpace(operationArgs))
        {
            return string.Empty;
        }

        return HostBridgeInterop.OperationPayload(operation, operationArgs) ?? string.Empty;
    }

    private static WitException<IPlugin.OperationError> CreateOperationError(string? error)
    {
        var message = string.IsNullOrWhiteSpace(error) ? "operation failed" : error.Trim();

        if (message.StartsWith("unsupported-operation:", StringComparison.OrdinalIgnoreCase))
        {
            return new WitException<IPlugin.OperationError>(
                IPlugin.OperationError.UnsupportedOperation(message["unsupported-operation:".Length..]),
                0);
        }

        if (message.StartsWith("invalid-arguments:", StringComparison.OrdinalIgnoreCase))
        {
            return new WitException<IPlugin.OperationError>(
                IPlugin.OperationError.InvalidArguments(message["invalid-arguments:".Length..]),
                0);
        }

        if (message.StartsWith("failed:", StringComparison.OrdinalIgnoreCase))
        {
            return new WitException<IPlugin.OperationError>(
                IPlugin.OperationError.Failed(message["failed:".Length..]),
                0);
        }

        return new WitException<IPlugin.OperationError>(IPlugin.OperationError.Failed(message), 0);
    }

    private sealed record PageRequestArgs(string mediaId, string chapterId, uint pageIndex);

    private sealed record PagesRequestArgs(string mediaId, string chapterId, uint startIndex, uint count);

    private sealed record VideoSegmentRequestArgs(string mediaId, string streamId, uint sequence);

    [JsonSerializable(typeof(PageRequestArgs))]
    [JsonSerializable(typeof(PagesRequestArgs))]
    [JsonSerializable(typeof(VideoSegmentRequestArgs))]
    private sealed partial class PluginImplJsonContext : JsonSerializerContext
    {
    }
}
#endif
