using System.Diagnostics;
using System.Text.Json;
using EMMA.Plugin.Common;

namespace EMMA.PluginTemplate.Infrastructure;

/// <summary>
/// Maps provider JSON payloads into EMMA plugin contract models.
/// </summary>
internal sealed class CoreClient
{
    private static readonly SearchItem[] Fixtures =
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
            return Fixtures;
        }

        return [.. Fixtures
            .Where(item =>
                item.id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || item.title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || (item.description?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false))];
    }

    /// <summary>
    /// Parses and maps search payload while returning split timing metrics.
    /// </summary>
    public SearchParseMapResult SearchFromPayloadWithTimings(string payloadJson)
    {
        var normalizedPayload = ResolvePayloadContent(payloadJson);
        if (string.IsNullOrWhiteSpace(normalizedPayload))
        {
            return new SearchParseMapResult([], 0, 0);
        }

        var parseStopwatch = Stopwatch.StartNew();
        using var doc = JsonDocument.Parse(normalizedPayload);
        parseStopwatch.Stop();

        var mapStopwatch = Stopwatch.StartNew();
        var entries = PayloadMapper.ParseSearchEntries(doc.RootElement);
        var results = new List<SearchItem>(entries.Count);
        foreach (var entry in entries)
        {
            results.Add(new SearchItem(
                entry.Id,
                "emma.video.test",
                entry.Title,
                "video",
                entry.ThumbnailUrl,
                entry.Description));
        }

        mapStopwatch.Stop();
        return new SearchParseMapResult(results, parseStopwatch.ElapsedMilliseconds, mapStopwatch.ElapsedMilliseconds);
    }

    public IReadOnlyList<ChapterItem> GetChaptersFromPayload(string payloadJson)
    {
        var entries = ParseChapterEntries(payloadJson);
        var results = new List<ChapterItem>(entries.Count);
        foreach (var entry in entries)
        {
            results.Add(new ChapterItem(entry.Id, entry.Number, entry.Title, entry.UploaderGroups));
        }

        return results;
    }

    public IReadOnlyList<ChapterOperationItem> GetChapterOperationItemsFromPayload(string payloadJson)
    {
        var entries = ParseChapterEntries(payloadJson);
        var results = new List<ChapterOperationItem>(entries.Count);
        foreach (var entry in entries)
        {
            results.Add(new ChapterOperationItem(entry.Id, entry.Number, entry.Title, entry.UploaderGroups));
        }

        return results;
    }

    public PageItem? GetPageFromPayload(string chapterId, int pageIndex, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(chapterId) || pageIndex < 0)
        {
            return null;
        }

        if (!TryGetAtHomePayload(payloadJson, out var atHomePayload))
        {
            return null;
        }

        if (pageIndex >= atHomePayload.Files.Count)
        {
            return null;
        }

        var fileName = atHomePayload.Files[pageIndex];
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return new PageItem(
            $"{chapterId}:{pageIndex}",
            pageIndex,
            $"{atHomePayload.BaseUrl}/{atHomePayload.DataPathSegment}/{atHomePayload.Hash}/{fileName}");
    }

    public IReadOnlyList<PageItem> GetPagesFromPayload(
        string chapterId,
        int startIndex,
        int count,
        string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(chapterId) || startIndex < 0 || count <= 0)
        {
            return [];
        }

        if (!TryGetAtHomePayload(payloadJson, out var atHomePayload))
        {
            return [];
        }

        if (startIndex >= atHomePayload.Files.Count)
        {
            return [];
        }

        var endExclusive = Math.Min(atHomePayload.Files.Count, startIndex + count);
        var pages = new List<PageItem>(Math.Max(0, endExclusive - startIndex));

        for (var pageIndex = startIndex; pageIndex < endExclusive; pageIndex++)
        {
            var fileName = atHomePayload.Files[pageIndex];
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            pages.Add(new PageItem(
                $"{chapterId}:{pageIndex}",
                pageIndex,
                $"{atHomePayload.BaseUrl}/{atHomePayload.DataPathSegment}/{atHomePayload.Hash}/{fileName}"));
        }

        return pages;
    }

    public static string ResolvePayloadContent(string payload)
    {
        return PluginJsonPayload.Normalize(payload);
    }

    private static IReadOnlyList<MangadexChapterEntry> ParseChapterEntries(string payloadJson)
    {
        var normalizedPayload = ResolvePayloadContent(payloadJson);
        if (string.IsNullOrWhiteSpace(normalizedPayload))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(normalizedPayload);
        return PayloadMapper.ParseChapterEntries(doc.RootElement);
    }

    private static bool TryGetAtHomePayload(string payloadJson, out MangadexAtHomePayload payload)
    {
        var normalizedPayload = ResolvePayloadContent(payloadJson);
        if (string.IsNullOrWhiteSpace(normalizedPayload))
        {
            payload = default;
            return false;
        }

        return PayloadMapper.TryParseAtHomePayload(normalizedPayload, out payload);
    }
}

internal readonly record struct SearchParseMapResult(
    IReadOnlyList<SearchItem> Results,
    long ParseMs,
    long MapMs);