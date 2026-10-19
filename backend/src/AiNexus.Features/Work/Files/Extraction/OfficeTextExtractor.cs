using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AiNexus.Features.Work.Files.Extraction;

/// <summary>有上限、被動式的 OOXML 文字抽取；不計算公式、不執行巨集，也不跟隨外部關聯。</summary>
internal sealed partial class OfficeTextExtractor : IDisposable
{
    private readonly ZipArchive zip;
    private readonly int maxCharacters;
    private readonly CancellationToken ct;
    private readonly StringBuilder text = new();
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public OfficeTextExtractor(byte[] bytes, int maxCharacters, CancellationToken ct)
    {
        this.maxCharacters = maxCharacters; this.ct = ct;
        zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count > 1500 || zip.Entries.Sum(x => x.Length) > 32L * 1024 * 1024 || zip.Entries.Any(x => x.Length > 8L * 1024 * 1024))
        { zip.Dispose(); throw new DocumentRejectedException(FilesErrors.OfficeArchiveLimit); }
        if (zip.Entries.Any(x => x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
        { zip.Dispose(); throw new DocumentRejectedException(FilesErrors.OfficeMacrosUnsupported); }
    }
    private XDocument Read(string path)
    {
        ct.ThrowIfCancellationRequested();
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException();
        using var source = entry.Open();
        using var xml = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 });
        return XDocument.Load(xml);
    }
    private void Append(string value)
    {
        ct.ThrowIfCancellationRequested();
        if (text.Length + value.Length > maxCharacters) throw new DocumentRejectedException(FilesErrors.DocumentTooLarge);
        text.Append(value);
    }
    public string Extract(string extension)
    {
        var types = Read("[Content_Types].xml").ToString();
        if (types.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || types.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))
            throw new DocumentRejectedException(FilesErrors.OfficeMacrosUnsupported);
        if (extension == ".docx") {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            foreach (var p in Read("word/document.xml").Descendants(w + "p")) Append(string.Concat(p.Descendants(w + "t").Select(x => x.Value)) + "\n");
        }
        else if (extension == ".pptx") {
            var slides = zip.Entries.Select(x => SlidePath().Match(x.FullName)).Where(x => x.Success)
                .Select(x => (Path: x.Value, Number: x.Groups[1].Value)).OrderBy(x => int.Parse(x.Number, CultureInfo.InvariantCulture)).ToArray();
            if (slides.Length is < 1 or > 200) throw new DocumentRejectedException(FilesErrors.PresentationSlideLimit);
            foreach (var slide in slides) {
                Append($"\n## 投影片 {slide.Number}\n");
                foreach (var p in Read(slide.Path).Descendants(Drawing + "p")) Append(string.Concat(p.Descendants(Drawing + "t").Select(x => x.Value)) + "\n");
            }
        }
        else {
            var strings = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Read("xl/sharedStrings.xml").Descendants(Spreadsheet + "si")
                .Select(x => string.Concat(x.Descendants(Spreadsheet + "t").Select(t => t.Value))).ToArray();
            if (strings.Length > 100000 || strings.Sum(x => (long)x.Length) > 2000000) throw new DocumentRejectedException(FilesErrors.SpreadsheetStringLimit);
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var relationships = Read("xl/_rels/workbook.xml.rels").Root!.Elements().Where(x => (string?)x.Attribute("TargetMode") != "External")
                .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
            var sheets = Read("xl/workbook.xml").Descendants(Spreadsheet + "sheet").ToArray();
            if (sheets.Length is < 1 or > 128) throw new DocumentRejectedException(FilesErrors.SpreadsheetSheetLimit);
            foreach (var sheet in sheets) {
                var target = relationships[(string)sheet.Attribute(r + "id")!];
                var path = new Uri(new Uri("https://office.invalid/xl/"), target).AbsolutePath.TrimStart('/');
                if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains('%')) throw new InvalidDataException();
                Append($"\n## 工作表：{sheet.Attribute("name")?.Value}\n");
                var count = 0;
                foreach (var row in Read(path).Descendants(Spreadsheet + "row")) {
                    foreach (var cell in row.Elements(Spreadsheet + "c")) {
                        if (++count > 100000) throw new DocumentRejectedException(FilesErrors.SpreadsheetCellLimit);
                        var value = cell.Element(Spreadsheet + "v")?.Value ?? "";
                        var type = (string?)cell.Attribute("t");
                        if (type == "s") {
                            if (!int.TryParse(value, out var index) || index < 0 || index >= strings.Length) throw new InvalidDataException();
                            value = strings[index];
                        } else if (type == "inlineStr") value = string.Concat(cell.Descendants(Spreadsheet + "t").Select(x => x.Value));
                        else if (type == "b") value = value == "1" ? "TRUE" : "FALSE";
                        if (cell.Element(Spreadsheet + "f") is not null && value.Length == 0) value = "[公式沒有已保存的計算結果]";
                        if (value.Length > 0) Append($"{cell.Attribute("r")?.Value}: {value}\t");
                    }
                    Append("\n");
                }
            }
        }
        return text.ToString();
    }
    public void Dispose() => zip.Dispose();

    // 投影片依檔名中的編號排序並當作標題；群組 1 是編號。
    [GeneratedRegex(@"^ppt/slides/slide(\d+)\.xml$")]
    private static partial Regex SlidePath();
}
