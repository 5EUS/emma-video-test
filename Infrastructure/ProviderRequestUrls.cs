using EMMA.Plugin.Common;

namespace EMMA.VideoTest.Infrastructure;

internal static class ProviderHttpProfile
{
    public static readonly PluginProviderHttpProfile Defaults = new(
        BaseUri: new Uri("https://download.samplelib.com"),
        UserAgent: "EMMA-VideoTest/1.0",
        AcceptMediaType: "application/json");
}

internal static class ProviderRequestUrls
{
    private static readonly IPluginProviderUrlStrategy Strategy = new VideoTestUrlStrategy();

    public static string? BuildSearchPath(string query)
    {
        return Strategy.BuildSearchPath(query);
    }

    public static string? BuildChaptersPath(string mediaId)
    {
        return Strategy.BuildChaptersPath(mediaId);
    }

    public static string? BuildAtHomePath(string chapterId)
    {
        return Strategy.BuildAtHomePath(chapterId);
    }

    public static string? BuildSearchAbsoluteUrl(string query)
    {
        return Strategy.BuildSearchAbsoluteUrl(query);
    }

    public static string? BuildSearchAbsoluteUrl(PluginSearchQuery query)
    {
        return Strategy.BuildSearchAbsoluteUrl(query);
    }

    public static string? BuildChaptersAbsoluteUrl(string mediaId)
    {
        return Strategy.BuildChaptersAbsoluteUrl(mediaId);
    }

    public static string? BuildAtHomeAbsoluteUrl(string chapterId)
    {
        return Strategy.BuildAtHomeAbsoluteUrl(chapterId);
    }

    private sealed class VideoTestUrlStrategy : IPluginProviderUrlStrategy
    {
        public string? BuildSearchAbsoluteUrl(PluginSearchQuery query)
        {
            return null;
        }

        public string? BuildChaptersAbsoluteUrl(string mediaId)
        {
            return null;
        }

        public string? BuildAtHomeAbsoluteUrl(string chapterId)
        {
            return null;
        }
    }
}