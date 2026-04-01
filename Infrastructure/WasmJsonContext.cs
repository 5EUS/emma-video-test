#if PLUGIN_TRANSPORT_WASM
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EMMA.Plugin.Common;

namespace EMMA.VideoTest.Infrastructure;

[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, string>))]
[JsonSerializable(typeof(HandshakeResponse))]
[JsonSerializable(typeof(CapabilityItem[]))]
[JsonSerializable(typeof(SearchItem[]))]
[JsonSerializable(typeof(ChapterItem[]))]
[JsonSerializable(typeof(WasmChapterOperationItem[]))]
[JsonSerializable(typeof(WasmVideoStreamOperationItem[]))]
[JsonSerializable(typeof(WasmVideoTrackOperationItem[]))]
[JsonSerializable(typeof(WasmVideoSegmentOperationItem))]
[JsonSerializable(typeof(PageItem))]
[JsonSerializable(typeof(PageItem[]))]
[JsonSerializable(typeof(OperationResult))]
[JsonSerializable(typeof(BenchmarkResult))]
[JsonSerializable(typeof(NetworkBenchmarkResult))]
internal sealed partial class WasmJsonContext : JsonSerializerContext
{
}

internal sealed record WasmChapterOperationItem(
    string id,
    int number,
    string title,
    string[] uploaderGroups);

internal sealed record WasmVideoStreamOperationItem(
    string id,
    string label,
    string playlistUri,
    IReadOnlyDictionary<string, string>? requestHeaders = null,
    string? requestCookies = null,
    string? streamType = null,
    bool isLive = false,
    bool drmProtected = false,
    string? drmScheme = null,
    WasmVideoTrackOperationItem[]? audioTracks = null,
    WasmVideoTrackOperationItem[]? subtitleTracks = null,
    string? defaultAudioTrackId = null,
    string? defaultSubtitleTrackId = null);

internal sealed record WasmVideoTrackOperationItem(
    string id,
    string label,
    string? language = null,
    string? codec = null,
    bool isDefault = false);

internal sealed record WasmVideoSegmentOperationItem(
    string contentType,
    string payload);
#endif
