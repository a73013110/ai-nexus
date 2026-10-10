using System.ComponentModel.DataAnnotations;

namespace AiNexus.Platform.Configuration;

/// <summary>
/// A server-controlled HTTP(S) endpoint: absolute, without user info, query or fragment, so credentials and
/// parameters never travel in a configured URL. An empty value passes; combine with <see cref="RequiredAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HttpEndpointAttribute : ValidationAttribute
{
    /// <summary>Plain HTTP is accepted only for loopback hosts.</summary>
    public bool HttpsOutsideLoopback { get; set; }

    public HttpEndpointAttribute() : base("{0} must be an absolute HTTP(S) URL without user info, query or fragment.") { }

    public override bool IsValid(object? value) => value is null or "" || value is string text && Valid(text, HttpsOutsideLoopback);

    public static bool Valid(string value, bool httpsOutsideLoopback = false) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && (!httpsOutsideLoopback || uri.IsLoopback))
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
