namespace AiNexus.Platform.Errors;

// TEMPORARY during B5: lets not-yet-converted callers keep throwing. Deleted with ApiException before the PR is opened.
public static class TransitionalThrow
{
    public static T OrThrow<T>(this Result<T> result) => result.IsSuccess ? result.Value : throw result.Error.Throwable();
    public static void OrThrow(this Result result) { if (!result.IsSuccess) throw result.Error.Throwable(); }
    public static ApiException Throwable(this Error error) => new(Problems.Status(error.Kind), error.Code, error.Code);
}
