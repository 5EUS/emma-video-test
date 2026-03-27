using EMMA.Contracts.Plugins;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace EMMA.TestPlugin.Services;

public sealed class TestPluginRuntime(
    ILogger<TestPluginRuntime> logger) : ITestPluginRuntime
{
    private const string SourceId = "emma.video.test";
    private const string MediaTypeVideo = "video";

    private static readonly IReadOnlyList<MediaSummary> VideoFixtures =
    [
        new MediaSummary
        {
            Id = "video-hls-single",
            Source = SourceId,
            Title = "Video Test - HLS Single Stream",
            MediaType = MediaTypeVideo,
            ThumbnailUrl = "https://example.invalid/posters/video-hls-single.jpg",
            Description = "Scenario A: one playlist URI stream."
        },
        new MediaSummary
        {
            Id = "video-hls-multi",
            Source = SourceId,
            Title = "Video Test - HLS Multi Quality",
            MediaType = MediaTypeVideo,
            ThumbnailUrl = "https://example.invalid/posters/video-hls-multi.jpg",
            Description = "Scenario B: multiple stream variants for selection."
        },
        new MediaSummary
        {
            Id = "video-segment-basic",
            Source = SourceId,
            Title = "Video Test - Segment Mode",
            MediaType = MediaTypeVideo,
            ThumbnailUrl = "https://example.invalid/posters/video-segment-basic.jpg",
            Description = "Scenario C/D: deterministic segment fetch and miss path."
        },
        new MediaSummary
        {
            Id = "video-empty-streams",
            Source = SourceId,
            Title = "Video Test - Empty Streams",
            MediaType = MediaTypeVideo,
            ThumbnailUrl = "https://example.invalid/posters/video-empty-streams.jpg",
            Description = "Scenario E: stream list intentionally empty."
        }
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<StreamInfo>> StreamsByMediaId =
        new Dictionary<string, IReadOnlyList<StreamInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["video-hls-single"] =
            [
                new StreamInfo
                {
                    Id = "single-main",
                    Label = "Main",
                    PlaylistUri = "https://example.invalid/video-hls-single/master.m3u8"
                }
            ],
            ["video-hls-multi"] =
            [
                new StreamInfo
                {
                    Id = "multi-1080p",
                    Label = "1080p",
                    PlaylistUri = "https://example.invalid/video-hls-multi/1080p.m3u8"
                },
                new StreamInfo
                {
                    Id = "multi-720p",
                    Label = "720p",
                    PlaylistUri = "https://example.invalid/video-hls-multi/720p.m3u8"
                },
                new StreamInfo
                {
                    Id = "multi-480p",
                    Label = "480p",
                    PlaylistUri = "https://example.invalid/video-hls-multi/480p.m3u8"
                }
            ],
            ["video-segment-basic"] =
            [
                new StreamInfo
                {
                    Id = "segment-main",
                    Label = "Segment Main",
                    PlaylistUri = "https://example.invalid/video-segment-basic/master.m3u8"
                }
            ],
            ["video-empty-streams"] = []
        };

    private readonly ILogger<TestPluginRuntime> _logger = logger;

    public Task<IReadOnlyList<MediaSummary>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<MediaSummary> results;
        if (string.IsNullOrWhiteSpace(query))
        {
            results = VideoFixtures;
        }
        else
        {
            var normalized = query.Trim();
            results = VideoFixtures
                .Where(item =>
                    item.Id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || item.Title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || (item.Description?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        _logger.LogInformation("Video fixture search query={Query} results={Count}", query, results.Count);
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<MediaChapter>> GetChaptersAsync(string mediaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MediaChapter>>([]);
    }

    public Task<MediaPage?> GetPageAsync(string chapterId, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<MediaPage?>(null);
    }

    public Task<(IReadOnlyList<MediaPage> Pages, bool ReachedEnd)> GetPagesAsync(
        string chapterId,
        int startIndex,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult((Pages: (IReadOnlyList<MediaPage>)[], ReachedEnd: true));
    }

    public Task<StreamResponse> GetStreamsAsync(string mediaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = new StreamResponse();

        if (StreamsByMediaId.TryGetValue(mediaId, out var streams))
        {
            response.Streams.AddRange(streams.Select(stream => new StreamInfo
            {
                Id = stream.Id,
                Label = stream.Label,
                PlaylistUri = stream.PlaylistUri
            }));
        }

        _logger.LogInformation("Streams mediaId={MediaId} count={Count}", mediaId, response.Streams.Count);
        return Task.FromResult(response);
    }

    public Task<SegmentResponse> GetSegmentAsync(string mediaId, string streamId, int sequence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(mediaId, "video-segment-basic", StringComparison.OrdinalIgnoreCase)
            && string.Equals(streamId, "segment-main", StringComparison.OrdinalIgnoreCase)
            && sequence >= 0
            && sequence <= 4)
        {
            var payloadText = $"SEGMENT|media={mediaId}|stream={streamId}|seq={sequence}";
            return Task.FromResult(new SegmentResponse
            {
                ContentType = "video/mp2t",
                Payload = ByteString.CopyFromUtf8(payloadText)
            });
        }

        _logger.LogInformation(
            "Segment miss mediaId={MediaId} streamId={StreamId} sequence={Sequence}",
            mediaId,
            streamId,
            sequence);

        return Task.FromResult(new SegmentResponse());
    }
}
