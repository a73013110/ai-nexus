using AiNexus.Features.Work.Repositories;

namespace AiNexus.UnitTests.Repositories;

public sealed class RepositoryServiceTests
{
    [Theory]
    [InlineData("../private")]
    [InlineData("hanglong/../../private")]
    [InlineData("hanglong/nexus?token=bad")]
    public void RepositoryPathsCannotEscapeTheControlledHost(string repository) => Assert.False(RepositoryService.IsRepository(repository));

    [Theory]
    [InlineData("../secrets")]
    [InlineData("a/../../secret")]
    [InlineData("/absolute")]
    [InlineData("folder\\file")]
    public void FilePathsRejectTraversal(string path) => Assert.False(RepositoryService.IsFilePath(path, false));

    [Fact]
    public void BranchNamesCannotMasqueradeAsPinnedCommits() => Assert.False(RepositoryService.IsCommit("main"));

    [Theory]
    [InlineData("main", true)]
    [InlineData("release/1.0", true)]
    [InlineData("feature..evil", false)]
    [InlineData("main?token=x", false)]
    [InlineData("main branch", false)]
    public void RefsAreLimitedToPlainBranchAndTagNames(string value, bool valid) => Assert.Equal(valid, RepositoryService.IsRef(value));
}
