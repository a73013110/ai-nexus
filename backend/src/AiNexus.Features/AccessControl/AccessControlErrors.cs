using AiNexus.Platform.Errors;

namespace AiNexus.Features.AccessControl;

/// <summary>Access failures every module shares.</summary>
public static class AccessControlErrors
{
    /// <summary>
    /// The account lacks the feature grant an operation needs. The one code for every feature, whether a policy refuses
    /// the request or a handler, transaction or background job re-checks the grant: the response does not name the
    /// feature (a policy may accept any of several), and the page that needs it already knows which one it is.
    /// </summary>
    public static readonly Error FeatureForbidden = Error.Forbidden("feature_forbidden");
}
