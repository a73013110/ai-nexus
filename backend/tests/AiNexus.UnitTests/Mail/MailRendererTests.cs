using AiNexus.Data.App.Mail;
using AiNexus.Features.Admin.Mail;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Mail;

public sealed class MailRendererTests
{
    private sealed class Notice : MailTemplate<string>
    {
        public override string Category => "test.notice";
        public override MailPriority Priority => MailPriority.Security;
        public override bool Sensitive => true;
        public override string Subject(string model) => $"通知\r\nBcc: eve@evil.test {model}";
        public override void Body(string model, MailBody body) => body.Heading("嗨 " + model).Paragraph("第一行\n第二行").Code("012345").Button("開啟", "/work/chat?x=1&y=2").Note("不是你本人操作請忽略。").KeyValue("帳號", model);
    }

    private sealed class Linked(string path) : MailTemplate<int>
    {
        public override string Category => "test.linked";
        public override string Subject(int model) => "連結";
        public override void Body(int model, MailBody body) => body.Button("開啟", path);
    }

    private sealed class Misnamed : MailTemplate<int>
    {
        public override string Category => "Test Category";
        public override string Subject(int model) => "x";
        public override void Body(int model, MailBody body) { }
    }

    private static readonly MailRenderer Renderer = new(Options.Create(new MailOptions { PublicBaseUrl = "https://ainexus.company.internal" }));

    [Fact]
    public void ValuesAreEncodedAndBothBodiesCarryTheSameBlocks()
    {
        var mail = Renderer.Render(new Notice(), "<script>alert(1)</script>");
        Assert.Equal(("test.notice", MailPriority.Security, true), (mail.Category, mail.Priority, mail.Sensitive));
        Assert.DoesNotContain('\n', mail.Subject);
        Assert.DoesNotContain('\r', mail.Subject);
        Assert.DoesNotContain("<script>", mail.Html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", mail.Html, StringComparison.Ordinal);
        Assert.Contains("第一行<br>第二行", mail.Html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://ainexus.company.internal/work/chat?x=1&amp;y=2\"", mail.Html, StringComparison.Ordinal);
        Assert.Contains("<script>alert(1)</script>", mail.Text, StringComparison.Ordinal);
        Assert.Contains("開啟：https://ainexus.company.internal/work/chat?x=1&y=2", mail.Text, StringComparison.Ordinal);
        Assert.Contains("012345", mail.Text, StringComparison.Ordinal);
        Assert.Contains(MailRenderer.Footer, mail.Text, StringComparison.Ordinal);
        Assert.Contains(MailRenderer.Footer, mail.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.test/login")]
    [InlineData("//evil.test/login")]
    [InlineData("/\\evil.test")]
    [InlineData("javascript:alert(1)")]
    public void LinksMustBeSiteRelativePaths(string path)
        => Assert.Throws<ArgumentException>(() => Renderer.Render(new Linked(path), 0));

    [Fact]
    public void CategoriesMustBeLowercaseDottedNames()
        => Assert.Throws<ArgumentException>(() => Renderer.Render(new Misnamed(), 0));

    [Fact]
    public void SubjectsAreSingleLineAndBounded()
    {
        Assert.Equal("a b", MailRenderer.Subject("a\r\n\tb"));
        Assert.Equal(MailRenderer.ProductName, MailRenderer.Subject(" \r\n "));
        Assert.Equal(OutboundMessage.SubjectMaxLength, MailRenderer.Subject(new string('字', 500)).Length);
    }
}
