using AiNexus.Features.Attachments;

namespace AiNexus.UnitTests.Attachments;

public sealed class FileAttachmentStorageTests
{
    [Fact]
    public void StorageRejectsWebsitePathsAndRelativePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "nexus-storage-root");
        var site = Path.Combine(root, "site");
        var attachments = Path.Combine(root, "data", "attachments");
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot("data", site));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(site, site));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(Path.Combine(site, "wwwroot", "files"), site));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(Path.Combine(root, "data", "..", "site", "files"), site));
        Assert.Equal(attachments, FileAttachmentStorage.ValidateRoot(attachments + Path.DirectorySeparatorChar, site));
        Assert.Equal(site + "-data", FileAttachmentStorage.ValidateRoot(site + "-data", site));
    }

    [Fact]
    public void StorageComparesWindowsPathsWithoutCaseAndRejectsDriveRelativePaths()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "只在 Windows 驗證磁碟機路徑語意。");
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(@"D:data\attachments", @"D:\site"));
        Assert.Throws<InvalidOperationException>(() => FileAttachmentStorage.ValidateRoot(@"d:\SITE\wwwroot\files", @"D:\site"));
        Assert.Equal(@"D:\CoreProject\AiNexus\data\attachments", FileAttachmentStorage.ValidateRoot(@"D:\CoreProject\AiNexus\data\attachments", @"D:\CoreProject\AiNexus\site"));
    }
}
