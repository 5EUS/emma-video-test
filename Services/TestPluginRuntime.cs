using EMMA.Contracts.Plugins;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System.IO;

namespace EMMA.TestPlugin.Services;

public sealed class TestPluginRuntime(
    ILogger<TestPluginRuntime> logger) : ITestPluginRuntime
{
    private const string SourceId = "emma.video.test";
    private const string MediaTypeVideo = "video";
    private const string DefaultSingleHlsUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4";
    private const string DefaultMulti1080pUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4";
    private const string DefaultMulti720pUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4";
    private const string DefaultMulti480pUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerFun.mp4";
    private const string DefaultSegmentBasicUri = "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyrides.mp4";

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
        },
        new MediaSummary
        {
            Id = "video-local-file",
            Source = SourceId,
            Title = "Video Test - Local File",
            MediaType = MediaTypeVideo,
            ThumbnailUrl = "https://example.invalid/posters/video-local-file.jpg",
            Description = "Optional local file stream from EMMA_VIDEO_TEST_LOCAL_FILE_PATH."
        }
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<StreamInfo>> StreamsByMediaId =
        BuildStreamsByMediaId();

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

        if (sequence >= 0
            && sequence <= 4
            && StreamsByMediaId.TryGetValue(mediaId, out var streams)
            && streams.Any(stream => string.Equals(stream.Id, streamId, StringComparison.OrdinalIgnoreCase)))
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

    private static IReadOnlyDictionary<string, IReadOnlyList<StreamInfo>> BuildStreamsByMediaId()
    {
        var singleUri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_SINGLE_URI", DefaultSingleHlsUri);
        var multi1080Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_1080_URI", DefaultMulti1080pUri);
        var multi720Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_720_URI", DefaultMulti720pUri);
        var multi480Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_480_URI", DefaultMulti480pUri);
        var segmentUri = GetEnvOrDefault("EMMA_VIDEO_TEST_SEGMENT_URI", DefaultSegmentBasicUri);
        var localFileUri = ResolveLocalFileUri(Environment.GetEnvironmentVariable("EMMA_VIDEO_TEST_LOCAL_FILE_PATH"));

        return new Dictionary<string, IReadOnlyList<StreamInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["video-hls-single"] =
            [
                new StreamInfo
                {
                    Id = "single-main",
                    Label = "Main",
                    PlaylistUri = singleUri
                }
            ],
            ["video-hls-multi"] =
            [
                new StreamInfo
                {
                    Id = "multi-1080p",
                    Label = "1080p",
                    PlaylistUri = multi1080Uri
                },
                new StreamInfo
                {
                    Id = "multi-720p",
                    Label = "720p",
                    PlaylistUri = multi720Uri
                },
                new StreamInfo
                {
                    Id = "multi-480p",
                    Label = "480p",
                    PlaylistUri = multi480Uri
                }
            ],
            ["video-segment-basic"] =
            [
                new StreamInfo
                {
                    Id = "segment-main",
                    Label = "Segment Main",
                    PlaylistUri = segmentUri
                }
            ],
            ["video-empty-streams"] = [],
            ["video-local-file"] = BuildLocalFileStreams(localFileUri)
        };
    }

    private static IReadOnlyList<StreamInfo> BuildLocalFileStreams(string? localFileUri)
    {
        if (string.IsNullOrWhiteSpace(localFileUri))
        {
            return [];
        }

        return
        [
            new StreamInfo
            {
                Id = "local-file-main",
                Label = "Local File",
                PlaylistUri = localFileUri
            }
        ];
    }

    private static string GetEnvOrDefault(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return fallback;
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
}
