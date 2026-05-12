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
[JsonSerializable(typeof(VideoTrackOperationItem))]
[JsonSerializable(typeof(VideoTrackOperationItem[]))]
[JsonSerializable(typeof(List<VideoTrackOperationItem>))]
[JsonSerializable(typeof(IReadOnlyList<VideoTrackOperationItem>))]
[JsonSerializable(typeof(VideoStreamOperationItem[]))]
[JsonSerializable(typeof(VideoSegmentOperationItem))]
[JsonSerializable(typeof(SearchItem[]))]
[JsonSerializable(typeof(ChapterItem[]))]
[JsonSerializable(typeof(WasmChapterOperationItem[]))]
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
#endif
