using EMMA.Contracts.Plugins;
using EMMA.Plugin.AspNetCore;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace EMMA.TestPlugin.Services;

/// <summary>
/// Minimal video provider stub for testing.
/// </summary>
public sealed class TestVideoProviderService(
    ITestPluginRuntime runtime,
    ILogger<TestVideoProviderService> logger) : VideoProvider.VideoProviderBase
{
    private readonly ITestPluginRuntime _runtime = runtime;
    private readonly ILogger<TestVideoProviderService> _logger = logger;

    public override async Task<StreamResponse> GetStreams(StreamRequest request, ServerCallContext context)
    {
        var correlationId = PluginRequestContext.GetCorrelationId(context, request.Context?.CorrelationId);

        var response = await _runtime.GetStreamsAsync(request.MediaId, context.CancellationToken);
        _logger.LogInformation(
            "Streams request {CorrelationId} mediaId={MediaId} count={Count}",
            correlationId,
            request.MediaId,
            response.Streams.Count);
        return response;
    }

    public override async Task<SegmentResponse> GetSegment(SegmentRequest request, ServerCallContext context)
    {
        var correlationId = PluginRequestContext.GetCorrelationId(context, request.Context?.CorrelationId);

        var response = await _runtime.GetSegmentAsync(
            request.MediaId,
            request.StreamId,
            request.Sequence,
            context.CancellationToken);

        _logger.LogInformation(
            "Segment request {CorrelationId} mediaId={MediaId} streamId={StreamId} sequence={Sequence} contentType={ContentType} bytes={Size}",
            correlationId,
            request.MediaId,
            request.StreamId,
            request.Sequence,
            response.ContentType,
            response.Payload.Length);

        return response;
    }
}
