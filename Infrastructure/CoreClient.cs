using System.Text;
using EMMA.Plugin.Common;

namespace EMMA.PluginTemplate.Infrastructure;

/// <summary>
/// Fixture-backed core data source used by both ASP.NET and WASM transports.
/// </summary>
internal sealed class CoreClient
{
    private const string VideoSeriesCollectionId = "video-series-space-odyssey";
    private const string VideoSeason1Episode1Id = "video-series-space-odyssey-s01e01";
    private const string VideoSeason1Episode2Id = "video-series-space-odyssey-s01e02";
    private const string VideoSeason1Episode3Id = "video-series-space-odyssey-s01e03";
    private const string VideoSeason2Episode1Id = "video-series-space-odyssey-s02e01";
    private const string VideoSeason2Episode2Id = "video-series-space-odyssey-s02e02";
    private const string VideoSeason2Episode3Id = "video-series-space-odyssey-s02e03";

    public readonly record struct StreamFixture(string Id, string Label, string PlaylistUri);

    public readonly record struct SegmentFixture(string ContentType, byte[] Payload);

    private static readonly SearchItem[] SearchFixturesCatalog =
	[
		new SearchItem(
			id: "video-movie-nightfall",
			source: "emma.video.test",
			title: "Video Test - Movie Nightfall",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-movie-nightfall.jpg",
			description: "Single-video movie style item."),
		new SearchItem(
			id: "video-series-space-odyssey",
			source: "emma.video.test",
			title: "Video Test - Space Odyssey",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-series-space-odyssey.jpg",
			description: "Series detail sample with 2 seasons and 6 episodes."),
		new SearchItem(
			id: "video-hls-single",
			source: "emma.video.test",
			title: "Video Test - HLS Single Stream",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-hls-single.jpg",
			description: "Scenario A: one playlist URI stream."),
		new SearchItem(
			id: "video-hls-multi",
			source: "emma.video.test",
			title: "Video Test - HLS Multi Quality",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-hls-multi.jpg",
			description: "Scenario B: multiple stream variants for selection."),
		new SearchItem(
			id: "video-segment-basic",
			source: "emma.video.test",
			title: "Video Test - Segment Mode",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-segment-basic.jpg",
			description: "Scenario C/D: deterministic segment fetch and miss path."),
		new SearchItem(
			id: "video-empty-streams",
			source: "emma.video.test",
			title: "Video Test - Empty Streams",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-empty-streams.jpg",
			description: "Scenario E: stream list intentionally empty."),
		new SearchItem(
			id: "video-local-file",
			source: "emma.video.test",
			title: "Video Test - Local File",
			mediaType: "video",
			thumbnailUrl: "https://example.invalid/posters/video-local-file.jpg",
			description: "Optional local file stream from EMMA_VIDEO_TEST_LOCAL_FILE_PATH.")
	];

    public IReadOnlyList<SearchItem> SearchFixtures(string query)
    {
        var normalized = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return SearchFixturesCatalog;
        }

        return [.. SearchFixturesCatalog
            .Where(item =>
                item.id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || item.title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || (item.description?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false))];
    }

    public IReadOnlyList<ChapterItem> GetFixtureChapters(string mediaId)
    {
        if (!string.Equals(mediaId, VideoSeriesCollectionId, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return
        [
            new ChapterItem(VideoSeason1Episode1Id, 1, "S1E1 - Lift Off", []),
            new ChapterItem(VideoSeason1Episode2Id, 2, "S1E2 - First Contact", []),
            new ChapterItem(VideoSeason1Episode3Id, 3, "S1E3 - Orbitfall", []),
            new ChapterItem(VideoSeason2Episode1Id, 4, "S2E1 - Signal Lost", []),
            new ChapterItem(VideoSeason2Episode2Id, 5, "S2E2 - Deep Relay", []),
            new ChapterItem(VideoSeason2Episode3Id, 6, "S2E3 - Home Vector", []),
        ];
    }

    public IReadOnlyList<StreamFixture> GetFixtureStreams(string mediaId)
    {
        var streamsByMediaId = BuildStreamsByMediaId();
        return streamsByMediaId.TryGetValue(mediaId, out var streams)
            ? streams
            : [];
    }

    public SegmentFixture? GetFixtureSegment(string mediaId, string streamId, uint sequence)
    {
        if (string.Equals(mediaId, "video-segment-basic", StringComparison.OrdinalIgnoreCase)
            && string.Equals(streamId, "segment-main", StringComparison.OrdinalIgnoreCase)
            && sequence <= 4)
        {
            var payload = Encoding.UTF8.GetBytes(
                $"SEGMENT|media={mediaId}|stream={streamId}|seq={sequence}");
            return new SegmentFixture("video/mp2t", payload);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StreamFixture>> BuildStreamsByMediaId()
    {
        var singleUri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_SINGLE_URI", "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");
        var multi1080Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_1080_URI", "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4");
        var multi720Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_720_URI", "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerEscapes.mp4");
        var multi480Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_480_URI", "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerFun.mp4");
        var segmentUri = GetEnvOrDefault("EMMA_VIDEO_TEST_SEGMENT_URI", "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyrides.mp4");
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
}