using System.IO.Compression;
using System.Text;
using AiNexus.Features.Attachments;
using Microsoft.Extensions.Options;

namespace AiNexus.UnitTests.Attachments;

public sealed class DocumentExtractorTests
{
    [Fact]
    public void OfficeExtractionReadsWorksheetsCachedFormulasAndSlidesAndRejectsMacrosOrXmlEntities()
    {
        var extractor = new DocumentExtractor(Options.Create(new AttachmentOptions()));
        var types = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />";
        var sheet = "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><f>1+1</f><v>2</v></c><c r=\"C1\"><f>SUM(A1:B1)</f></c></row></sheetData></worksheet>";
        var bytes = Zip(new() { ["[Content_Types].xml"] = types, ["xl/workbook.xml"] = "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"成本\" r:id=\"rId1\" /></sheets></workbook>", ["xl/_rels/workbook.xml.rels"] = "<Relationships><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\" /></Relationships>", ["xl/sharedStrings.xml"] = "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>季度目標</t></si></sst>", ["xl/worksheets/sheet1.xml"] = sheet });
        var (type, text) = extractor.Extract("modern.xlsx", bytes, CancellationToken.None).Value; Assert.Contains("spreadsheetml", type); Assert.Contains("工作表：成本", text); Assert.Contains("A1: 季度目標", text); Assert.Contains("B1: 2", text); Assert.Contains("公式沒有已保存", text);
        var ppt = Zip(new() { ["[Content_Types].xml"] = types, ["ppt/slides/slide1.xml"] = "<root xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><a:p><a:r><a:t>投影片文字</a:t></a:r></a:p></root>" });
        Assert.Contains("投影片文字", extractor.Extract("slides.pptx", ppt, CancellationToken.None).Value.Text);
        var macro = Zip(new() { ["[Content_Types].xml"] = types, ["xl/vbaProject.bin"] = "macro" });
        Assert.Equal("office_macros_unsupported", extractor.Extract("disguised.xlsx", macro, CancellationToken.None).Error?.Code);
        var entity = Zip(new() { ["[Content_Types].xml"] = "<!DOCTYPE Types [<!ENTITY x SYSTEM 'file:///secret'>]><Types>&x;</Types>" });
        Assert.Equal("document_unreadable", extractor.Extract("entity.docx", entity, CancellationToken.None).Error?.Code);
        var bounded = new DocumentExtractor(Options.Create(new AttachmentOptions { MaxExtractedCharacters = 10 }));
        Assert.Equal("document_too_large", bounded.Extract("too-long.xlsx", bytes, CancellationToken.None).Error?.Code);
    }

    private static byte[] Zip(Dictionary<string, string> files)
    {
        using var bytes = new MemoryStream(); using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true)) foreach (var (path, text) in files) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8); writer.Write(text); }
        return bytes.ToArray();
    }
}
