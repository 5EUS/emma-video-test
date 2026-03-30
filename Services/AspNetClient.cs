using Google.Protobuf;
using EMMA.Plugin.Common;
using EMMA.Plugin.AspNetCore;
using EMMA.Contracts.Plugins;
using EMMA.PluginTemplate.Infrastructure;
using Microsoft.Extensions.Logging;

namespace EMMA.PluginTemplate.Services;

/// <summary>
/// Deterministic fixture runtime for search/chapter/video validation.
/// </summary>
public sealed class AspNetClient(HttpClient httpClient, ILogger<AspNetClient> logger)
    : IPluginPagedMediaRuntime, IPluginVideoRuntime
{
    private static readonly CoreClient Core = new();

    private readonly HttpClient _ = httpClient;
    private readonly ILogger<AspNetClient> _logger = logger;

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

    public Task<IReadOnlyList<MediaSummary>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fixtures = Core.SearchFixtures(query);
        var results = new List<MediaSummary>(fixtures.Count);
        foreach (var item in fixtures)
        {
            results.Add(new MediaSummary
            {
                Id = item.id,
                Source = item.source,
                Title = item.title,
                MediaType = item.mediaType,
                ThumbnailUrl = item.thumbnailUrl ?? string.Empty,
                Description = item.description ?? string.Empty,
            });
        }

        _logger.LogInformation("Video fixture search query={Query} results={Count}", query, results.Count);
        return Task.FromResult<IReadOnlyList<MediaSummary>>(results);
    }

    public Task<IReadOnlyList<MediaChapter>> GetChaptersAsync(string mediaId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(mediaId, VideoSeriesCollectionId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<MediaChapter>>([]);
        }

        IReadOnlyList<MediaChapter> results =
        [
            new MediaChapter { Id = VideoSeason1Episode1Id, Number = 1, Title = "S1E1 - Lift Off" },
            new MediaChapter { Id = VideoSeason1Episode2Id, Number = 2, Title = "S1E2 - First Contact" },
            new MediaChapter { Id = VideoSeason1Episode3Id, Number = 3, Title = "S1E3 - Orbitfall" },
            new MediaChapter { Id = VideoSeason2Episode1Id, Number = 4, Title = "S2E1 - Signal Lost" },
            new MediaChapter { Id = VideoSeason2Episode2Id, Number = 5, Title = "S2E2 - Deep Relay" },
            new MediaChapter { Id = VideoSeason2Episode3Id, Number = 6, Title = "S2E3 - Home Vector" },
        ];

        return Task.FromResult(results);
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

        var streamsByMediaId = BuildStreamsByMediaId();
        var response = new StreamResponse();
        if (streamsByMediaId.TryGetValue(mediaId, out var streams))
        {
            response.Streams.AddRange(streams.Select(stream => new StreamInfo
            {
                Id = stream.Id,
                Label = stream.Label,
                PlaylistUri = stream.PlaylistUri,
            }));
        }

        _logger.LogInformation("Video fixture streams mediaId={MediaId} count={Count}", mediaId, response.Streams.Count);
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
                Payload = ByteString.CopyFromUtf8(payloadText),
            });
        }

        return Task.FromResult(new SegmentResponse());
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
            ["video-hls-single"] =
            [
                new StreamFixture("single-main", "Main", singleUri),
            ],
            ["video-movie-nightfall"] =
            [
                new StreamFixture("movie-main", "Movie", singleUri),
            ],
            ["video-hls-multi"] =
            [
                new StreamFixture("multi-1080p", "1080p", multi1080Uri),
                new StreamFixture("multi-720p", "720p", multi720Uri),
                new StreamFixture("multi-480p", "480p", multi480Uri),
            ],
            ["video-segment-basic"] =
            [
                new StreamFixture("segment-main", "Segment Main", segmentUri),
            ],
            [VideoSeason1Episode1Id] =
            [
                new StreamFixture("s1e1-main", "1080p", singleUri),
            ],
            [VideoSeason1Episode2Id] =
            [
                new StreamFixture("s1e2-main", "1080p", multi1080Uri),
            ],
            [VideoSeason1Episode3Id] =
            [
                new StreamFixture("s1e3-main", "720p", multi720Uri),
            ],
            [VideoSeason2Episode1Id] =
            [
                new StreamFixture("s2e1-main", "1080p", multi1080Uri),
            ],
            [VideoSeason2Episode2Id] =
            [
                new StreamFixture("s2e2-main", "720p", multi720Uri),
            ],
            [VideoSeason2Episode3Id] =
            [
                new StreamFixture("s2e3-main", "480p", multi480Uri),
            ],
            ["video-empty-streams"] = [],
            ["video-local-file"] = string.IsNullOrWhiteSpace(localFileUri)
                ? []
                : [new StreamFixture("local-file-main", "Local File", localFileUri)],
        };
    }
}
