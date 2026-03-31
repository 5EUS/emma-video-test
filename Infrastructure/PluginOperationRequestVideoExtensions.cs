using EMMA.Plugin.Common;

namespace EMMA.VideoTest.Infrastructure;

internal static class PluginOperationRequestVideoExtensions
{
    // TODO move to Plugin.Common
    public static bool IsVideoMediaRequest(this OperationRequest request)
    {
        return string.IsNullOrWhiteSpace(request.mediaType)
            || string.Equals(request.mediaType.Trim(), PluginMediaTypes.Video, StringComparison.OrdinalIgnoreCase);
    }
}