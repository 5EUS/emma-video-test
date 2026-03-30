#if PLUGIN_TRANSPORT_ASPNET
using EMMA.Plugin.Common;
using EMMA.Plugin.AspNetCore;
using EMMA.PluginTemplate.Infrastructure;
using EMMA.PluginTemplate.Services;
#else
using EMMA.Plugin.Common;
using EMMA.PluginTemplate.Infrastructure;
#endif

namespace EMMA.PluginTemplate;

/// <summary>
/// Transport entrypoint: wires AspNet host services or WASM operation host.
/// </summary>
public static partial class Program
{
#if PLUGIN_TRANSPORT_ASPNET
    public static void Main(string[] args)
    {
        var devMode = PluginEnvironment.IsDevelopmentMode();
        var hostOptions = new PluginAspNetHostOptions(
            DefaultPort: PluginEnvironment.GetPort(args, 5000),
            PortEnvironmentVariables: devMode
                ? ["EMMA_PLUGIN_PORT", "EMMA_PLUGIN_TEMPLATE_PORT"]
                : ["EMMA_PLUGIN_PORT"],
            PortArgumentName: devMode ? "--port" : string.Empty,
            RootMessage: "EMMA plugin template is running.");

        PluginBuilder.Create(args, hostOptions)
            .UseDefaultControlService(ConfigureDefaultControlService)
                .AddDefaultPagedProviders<AspNetClient>()
                .AddDefaultVideoProvider<AspNetClient>()
            .Run(mapDefaultEndpoints: devMode);
    }

    /// <summary>
    /// Applies manifest-driven control defaults exposed to the host.
    /// </summary>
    private static void ConfigureDefaultControlService(PluginSdkControlOptions options)
    {
        options.Message = "EMMA Video Test ready";
        options.CpuBudgetMs = 200;
        options.MemoryMb = 256;
        options.Capabilities.Add("plugin-template");
        options.Capabilities.Add("search");
        options.Capabilities.Add("pages");
        options.Capabilities.Add("video");

        options.Domains.Clear();
        options.Domains.Add("commondatastorage.googleapis.com");
        options.Paths.Clear();
        options.Paths.Add("/");
    }

#else
    private static readonly WasmClient OperationClient = new();

    public static void Main(string[] args)
    {
        Environment.ExitCode = PluginWasmCliHost.Run(
            args,
            PluginOperationNames.WasmCliKnownOperations,
            OperationClient.ExecuteOperationForCli);
    }

    public static HandshakeResponse handshake()
    {
        return OperationClient.Handshake();
    }

    public static CapabilityItem[] capabilities()
    {
        return OperationClient.Capabilities();
    }

    public static SearchItem[] search(string query, string payloadJson)
    {
        return OperationClient.Search(query, payloadJson);
    }

    public static ChapterItem[] chapters(string mediaId, string payloadJson)
    {
        return OperationClient.Chapters(mediaId, payloadJson);
    }

    public static PageItem? page(string mediaId, string chapterId, uint pageIndex, string payloadJson)
    {
        return OperationClient.Page(mediaId, chapterId, pageIndex, payloadJson);
    }

    public static PageItem[] pages(string mediaId, string chapterId, uint startIndex, uint count, string payloadJson)
    {
        return OperationClient.Pages(mediaId, chapterId, startIndex, count, payloadJson);
    }

    public static VideoStreamItem[] videoStreams(string mediaId, string payloadJson)
    {
        return OperationClient.VideoStreams(mediaId, payloadJson);
    }

    public static VideoSegmentItem? videoSegment(string mediaId, string streamId, uint sequence, string payloadJson)
    {
        return OperationClient.VideoSegment(mediaId, streamId, sequence, payloadJson);
    }

    public static OperationResult invoke(OperationRequest request)
    {
        return OperationClient.Invoke(request);
    }
#endif
}
