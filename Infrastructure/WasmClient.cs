using System.Text.Json;
using EMMA.Plugin.Common;

namespace EMMA.TestPlugin.Infrastructure;

internal sealed partial class WasmMangadexClient
{
	private static readonly SearchItem[] Fixtures =
	[
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

	public SearchParseMapResult SearchFromPayloadWithTimings(string query, string payloadJson)
	{
		var parseStart = DateTime.UtcNow;
		var items = ParsePayload(payloadJson);
		var parseMs = (long)(DateTime.UtcNow - parseStart).TotalMilliseconds;

		var mapStart = DateTime.UtcNow;
		IReadOnlyList<SearchItem> filtered;
		if (string.IsNullOrWhiteSpace(query))
		{
			filtered = items;
		}
		else
		{
			var normalized = query.Trim();
			filtered = [.. items
				.Where(item =>
					item.id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
					|| item.title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
					|| (item.description?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false))];
		}

		var mapMs = (long)(DateTime.UtcNow - mapStart).TotalMilliseconds;
		return new SearchParseMapResult(filtered, parseMs, mapMs);
	}

	public string FetchSearchPayload(string query)
	{
		var normalized = query?.Trim() ?? string.Empty;
		var filtered = string.IsNullOrWhiteSpace(normalized)
			? Fixtures
			: [.. Fixtures.Where(item =>
					item.id.Contains(normalized, StringComparison.OrdinalIgnoreCase)
					|| item.title.Contains(normalized, StringComparison.OrdinalIgnoreCase)
					|| (item.description?.Contains(normalized, StringComparison.OrdinalIgnoreCase) ?? false))];

		using var stream = new MemoryStream();
		using (var writer = new Utf8JsonWriter(stream))
		{
			writer.WriteStartArray();
			foreach (var item in filtered)
			{
				writer.WriteStartObject();
				writer.WriteString(nameof(SearchItem.id), item.id);
				writer.WriteString(nameof(SearchItem.source), item.source);
				writer.WriteString(nameof(SearchItem.title), item.title);
				writer.WriteString(nameof(SearchItem.mediaType), item.mediaType);
				writer.WriteString(nameof(SearchItem.thumbnailUrl), item.thumbnailUrl);
				writer.WriteString(nameof(SearchItem.description), item.description);
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
		}

		return System.Text.Encoding.UTF8.GetString(stream.ToArray());
	}

	public string FetchChaptersPayload(string mediaId)
	{
		_ = mediaId;
		return "[]";
	}

	public string FetchAtHomePayload(string chapterId)
	{
		_ = chapterId;
		return "[]";
	}

	public IReadOnlyList<ChapterItem> GetChaptersFromPayload(string mediaId, string payloadJson)
	{
		_ = mediaId;
		_ = payloadJson;
		return [];
	}

	public PageItem? GetPageFromPayload(string chapterId, int pageIndex, string payloadJson)
	{
		_ = chapterId;
		_ = pageIndex;
		_ = payloadJson;
		return null;
	}

	public IReadOnlyList<PageItem> GetPagesFromPayload(string chapterId, int startIndex, int count, string payloadJson)
	{
		_ = chapterId;
		_ = startIndex;
		_ = count;
		_ = payloadJson;
		return [];
	}

	public static string ResolvePayloadContent(string? payloadJson)
	{
		return payloadJson ?? string.Empty;
	}

	private static IReadOnlyList<SearchItem> ParsePayload(string payloadJson)
	{
		if (string.IsNullOrWhiteSpace(payloadJson))
		{
			return Fixtures;
		}

		try
		{
			using var doc = JsonDocument.Parse(payloadJson);
			if (doc.RootElement.ValueKind != JsonValueKind.Array)
			{
				return Fixtures;
			}

			var parsed = new List<SearchItem>();
			foreach (var item in doc.RootElement.EnumerateArray())
			{
				if (item.ValueKind != JsonValueKind.Object)
				{
					continue;
				}

				var id = TryGetString(item, nameof(SearchItem.id));
				var source = TryGetString(item, nameof(SearchItem.source));
				var title = TryGetString(item, nameof(SearchItem.title));
				var mediaType = TryGetString(item, nameof(SearchItem.mediaType));
				if (string.IsNullOrWhiteSpace(id)
					|| string.IsNullOrWhiteSpace(source)
					|| string.IsNullOrWhiteSpace(title)
					|| string.IsNullOrWhiteSpace(mediaType))
				{
					continue;
				}

				parsed.Add(new SearchItem(
					id,
					source,
					title,
					mediaType,
					TryGetString(item, nameof(SearchItem.thumbnailUrl)),
					TryGetString(item, nameof(SearchItem.description))));
			}

			return parsed.Count > 0 ? parsed : Fixtures;
		}
		catch
		{
			return Fixtures;
		}
	}

	private static string? TryGetString(JsonElement element, string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value))
		{
			return null;
		}

		return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	}

	public sealed record SearchParseMapResult(
		IReadOnlyList<SearchItem> Results,
		long ParseMs,
		long MapMs);
}
