using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AiNexus.BuildingBlocks;

namespace AiNexus.Modules.Attachments;

/// <summary>Bounded, passive OOXML extraction. Never evaluates formulas, macros or external relationships.</summary>
internal sealed class OfficeTextExtractor : IDisposable
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
        { zip.Dispose(); throw new ApiException(400, "office_archive_limit", "Office 文件解壓內容過大，請拆分後上傳。"); }
        if (zip.Entries.Any(x => x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
        { zip.Dispose(); throw new ApiException(400, "office_macros_unsupported", "不接受巨集文件，請另存為不含巨集的格式。"); }
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
        if (text.Length + value.Length > maxCharacters) throw new ApiException(400, "document_too_large", $"文件文字最多 {maxCharacters:N0} 字元，請拆分後上傳。");
        text.Append(value);
    }
    public string Extract(string extension)
    {
        var types = Read("[Content_Types].xml").ToString();
        if (types.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || types.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(400, "office_macros_unsupported", "不接受巨集文件，請另存為不含巨集的格式。");
        if (extension == ".docx") {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            foreach (var p in Read("word/document.xml").Descendants(w + "p")) Append(string.Concat(p.Descendants(w + "t").Select(x => x.Value)) + "\n");
        }
        else if (extension == ".pptx") {
            var slides = zip.Entries.Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.FullName, @"^ppt/slides/slide\d+\.xml$"))
                .OrderBy(x => int.Parse(System.Text.RegularExpressions.Regex.Match(x.FullName, @"\d+").Value, CultureInfo.InvariantCulture)).ToArray();
            if (slides.Length is < 1 or > 200) throw new ApiException(400, "presentation_slide_limit", "簡報需為 1 至 200 頁。");
            foreach (var slide in slides) {
                Append($"\n## 投影片 {System.Text.RegularExpressions.Regex.Match(slide.FullName, @"\d+").Value}\n");
                foreach (var p in Read(slide.FullName).Descendants(Drawing + "p")) Append(string.Concat(p.Descendants(Drawing + "t").Select(x => x.Value)) + "\n");
            }
        }
        else {
            var strings = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Read("xl/sharedStrings.xml").Descendants(Spreadsheet + "si")
                .Select(x => string.Concat(x.Descendants(Spreadsheet + "t").Select(t => t.Value))).ToArray();
            if (strings.Length > 100000 || strings.Sum(x => (long)x.Length) > 2000000) throw new ApiException(400, "spreadsheet_string_limit", "試算表文字表過大，請拆分工作表。");
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var relationships = Read("xl/_rels/workbook.xml.rels").Root!.Elements().Where(x => (string?)x.Attribute("TargetMode") != "External")
                .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
            var sheets = Read("xl/workbook.xml").Descendants(Spreadsheet + "sheet").ToArray();
            if (sheets.Length is < 1 or > 128) throw new ApiException(400, "spreadsheet_sheet_limit", "試算表需為 1 至 128 個工作表。");
            foreach (var sheet in sheets) {
                var target = relationships[(string)sheet.Attribute(r + "id")!];
                var path = new Uri(new Uri("https://office.invalid/xl/"), target).AbsolutePath.TrimStart('/');
                if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains('%')) throw new InvalidDataException();
                Append($"\n## 工作表：{sheet.Attribute("name")?.Value}\n");
                var count = 0;
                foreach (var row in Read(path).Descendants(Spreadsheet + "row")) {
                    foreach (var cell in row.Elements(Spreadsheet + "c")) {
                        if (++count > 100000) throw new ApiException(400, "spreadsheet_cell_limit", "單一工作表最多 100,000 個非空儲存格。");
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
}
