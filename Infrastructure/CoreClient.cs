using System.Collections.Generic;
using System.Text;
using EMMA.Plugin.Common;

namespace EMMA.VideoTest.Infrastructure;

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

    public readonly record struct TrackFixture(
        string Id,
        string Label,
        string? Language,
        string? Codec,
        bool IsDefault);

    public readonly record struct StreamFixture(
        string Id,
        string Label,
        string PlaylistUri,
        string StreamType,
        bool IsLive,
        bool DrmProtected,
        string? DrmScheme,
        IReadOnlyDictionary<string, string>? RequestHeaders,
        string? RequestCookies,
        IReadOnlyList<TrackFixture>? AudioTracks,
        IReadOnlyList<TrackFixture>? SubtitleTracks,
        string? DefaultAudioTrackId,
        string? DefaultSubtitleTrackId);

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
            id: "video-dash-single",
            source: "emma.video.test",
            title: "Video Test - DASH Single Stream",
            mediaType: "video",
            thumbnailUrl: "https://example.invalid/posters/video-dash-single.jpg",
            description: "Scenario F: MPEG-DASH stream fixture."),
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

        var matches = SearchFixturesCatalog
            .Where(item =>
                item.id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || item.title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || (item.description?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        return matches.Count > 0 ? matches : SearchFixturesCatalog;
    }

    public IReadOnlyList<ChapterItem> GetFixtureChapters(string mediaId)
    {
        if (string.Equals(mediaId, VideoSeriesCollectionId, StringComparison.OrdinalIgnoreCase))
        {
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

        if (GetFixtureStreams(mediaId).Count > 0)
        {
            return [new ChapterItem(mediaId, 1, "Episode 1", [])];
        }

        return [];
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
        var singleUri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_SINGLE_URI", "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8");
        var multi1080Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_1080_URI", "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8");
        var multi720Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_720_URI", "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8");
        var multi480Uri = GetEnvOrDefault("EMMA_VIDEO_TEST_HLS_480_URI", "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8");
        var dashUri = GetEnvOrDefault("EMMA_VIDEO_TEST_DASH_SINGLE_URI", "https://dash.akamaized.net/envivio/EnvivioDash3/manifest.mpd");
        var segmentUri = GetEnvOrDefault("EMMA_VIDEO_TEST_SEGMENT_URI", "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8");
        var localFileUri = ResolveLocalFileUri(Environment.GetEnvironmentVariable("EMMA_VIDEO_TEST_LOCAL_FILE_PATH"));

        var audioTracks = new List<TrackFixture>
        {
            new("audio-en", "English", "en", "aac", true),
            new("audio-ja", "Japanese", "ja", "aac", false)
        };
        var subtitleTracks = new List<TrackFixture>
        {
            new("sub-en", "English CC", "en", "webvtt", true)
        };

        StreamFixture Hls(string id, string label, string uri)
            => new(
                id,
                label,
                uri,
                "hls",
                false,
                false,
                null,
                null,
                null,
                audioTracks,
                subtitleTracks,
                "audio-en",
                "sub-en");

        StreamFixture Dash(string id, string label, string uri)
            => new(
                id,
                label,
                uri,
                "dash",
                false,
                false,
                null,
                null,
                null,
                audioTracks,
                subtitleTracks,
                "audio-en",
                "sub-en");

        return new Dictionary<string, IReadOnlyList<StreamFixture>>(StringComparer.OrdinalIgnoreCase)
        {
            ["video-hls-single"] = [Hls("single-main", "Main", singleUri)],
            ["video-movie-nightfall"] = [Hls("movie-main", "Movie", singleUri)],
            ["video-hls-multi"] =
            [
                Hls("multi-1080p", "1080p", multi1080Uri),
                Hls("multi-720p", "720p", multi720Uri),
                Hls("multi-480p", "480p", multi480Uri),
            ],
            ["video-dash-single"] = [Dash("dash-main", "Main", dashUri)],
            ["video-segment-basic"] = [Hls("segment-main", "Segment Main", segmentUri)],
            [VideoSeason1Episode1Id] = [Hls("s1e1-main", "1080p", singleUri)],
            [VideoSeason1Episode2Id] = [Hls("s1e2-main", "1080p", multi1080Uri)],
            [VideoSeason1Episode3Id] = [Hls("s1e3-main", "720p", multi720Uri)],
            [VideoSeason2Episode1Id] = [Hls("s2e1-main", "1080p", multi1080Uri)],
            [VideoSeason2Episode2Id] = [Hls("s2e2-main", "720p", multi720Uri)],
            [VideoSeason2Episode3Id] = [Hls("s2e3-main", "480p", multi480Uri)],
            ["video-empty-streams"] = [],
            ["video-local-file"] = string.IsNullOrWhiteSpace(localFileUri)
                ? []
                : [new StreamFixture("local-file-main", "Local File", localFileUri, "direct", false, false, null, null, null, null, null, null, null)],
            [VideoSeriesCollectionId] =
            [
                Hls("series-main-1080p", "1080p", multi1080Uri),
                Hls("series-main-720p", "720p", multi720Uri),
            ],
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