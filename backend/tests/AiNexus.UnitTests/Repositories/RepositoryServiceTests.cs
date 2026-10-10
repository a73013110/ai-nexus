using AiNexus.Features.Repositories;

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
}
