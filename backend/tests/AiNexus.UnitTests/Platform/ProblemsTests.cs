using AiNexus.Platform.Errors;

namespace AiNexus.UnitTests.Platform;

public sealed class ProblemsTests
{
    [Fact]
    public void EveryErrorKindMapsToAClientOrServerStatus()
    {
        foreach (var kind in Enum.GetValues<ErrorKind>())
            Assert.InRange(Problems.Status(kind), 400, 599);
    }
}
