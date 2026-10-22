using System.Buffers;

namespace AiNexus.Platform.Export;

/// <summary>工作表的一欄：標題列文字與欄寬（Excel 的字元寬度單位，中文字約佔 2）。</summary>
public sealed record SpreadsheetColumn(string Header, double Width);

/// <summary>儲存格的強調：一般、粗體、警示（深紅色字）。只有字型不同，數字與日期格式由值的型別決定。</summary>
public enum SpreadsheetTone
{
    Normal = 0,
    Strong = 1,
    Danger = 2
}

/// <summary>
/// 一個儲存格。<paramref name="Value"/> 的型別決定 Excel 的格式：字串（文字，不會被當成公式）、整數與浮點數、<see cref="bool"/>、
/// <see cref="DateOnly"/>（日期）、<see cref="TimeOnly"/>（時刻）、<see cref="DateTime"/>（日期時間，照字面寫入、忽略 Kind）、
/// <see cref="TimeSpan"/>（時數，可加總，格式 <c>[h]:mm:ss</c>）；null 為空白。
/// <see cref="DateTimeOffset"/> 刻意不收：試算表沒有時區，呼叫端要先換成要顯示的當地時間。
/// </summary>
public readonly record struct SpreadsheetCell(object? Value, SpreadsheetTone Tone = SpreadsheetTone.Normal);

/// <summary>
/// 一張工作表：第一列是欄位標題（凍結、加篩選、列印時每頁重複），其下逐列加入資料。
/// 名稱沿用 Excel 的限制（1～31 字、不含 <c>: \ / ? * [ ]</c>），不合法時拋例外：名稱是程式寫死的，不是使用者輸入。
/// </summary>
public sealed class SpreadsheetSheet
{
    /// <summary>標題列以外的資料列上限（Excel 一張工作表最多 1,048,576 列）。</summary>
    public const int MaxRows = 1_048_575;

    private static readonly SearchValues<char> InvalidNameChars = SearchValues.Create(@":\/?*[]");
    private readonly List<SpreadsheetCell[]> rows = [];
    private readonly List<int> breaks = [];

    public SpreadsheetSheet(string name, IReadOnlyList<SpreadsheetColumn> columns)
    {
        if (name.Length is < 1 or > 31 || name.AsSpan().ContainsAny(InvalidNameChars) || name.StartsWith('\'') || name.EndsWith('\''))
            throw new ArgumentException($"'{name}' is not a valid worksheet name.", nameof(name));
        if (columns.Count is < 1 or > 16384) throw new ArgumentOutOfRangeException(nameof(columns));
        Name = name;
        Columns = columns;
    }

    public string Name { get; }

    public IReadOnlyList<SpreadsheetColumn> Columns { get; }

    /// <summary>標題列是否加自動篩選。資料區夾有空白列或小計列（分段排版）時關掉，篩選會把它們當成資料。</summary>
    public bool Filter { get; init; } = true;

    /// <summary>標題列以外的資料列；每列的儲存格數不超過欄數，少的視為空白。</summary>
    public IReadOnlyList<IReadOnlyList<SpreadsheetCell>> Rows => rows;

    /// <summary>列印時在這些資料列（<see cref="Rows"/> 的索引）之前分頁。</summary>
    public IReadOnlyList<int> PageBreaks => breaks;

    /// <summary>
    /// 加入一列。值可以是 <see cref="SpreadsheetCell"/>（要強調時）或直接給值，型別見 <see cref="SpreadsheetCell"/>；
    /// 不支援的型別立刻拋例外，不等到寫檔才失敗。
    /// </summary>
    public SpreadsheetSheet Add(params object?[] values)
    {
        if (values.Length > Columns.Count) throw new ArgumentException($"A row has {values.Length} cells but the sheet has {Columns.Count} columns.", nameof(values));
        if (rows.Count >= MaxRows) throw new InvalidOperationException("The worksheet is full.");
        var cells = new SpreadsheetCell[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var cell = values[i] as SpreadsheetCell? ?? new SpreadsheetCell(values[i]);
            if (!Supported(cell.Value)) throw new ArgumentException($"{cell.Value!.GetType().Name} cannot be written to a spreadsheet cell.", nameof(values));
            cells[i] = cell;
        }
        rows.Add(cells);
        return this;
    }

    /// <summary>之後加入的列從新的一頁開始列印（例如每人一頁）；還沒有資料或剛分過頁時不重複。</summary>
    public SpreadsheetSheet BreakPage()
    {
        if (rows.Count > 0 && (breaks.Count == 0 || breaks[^1] != rows.Count)) breaks.Add(rows.Count);
        return this;
    }

    private static bool Supported(object? value) => value is null or string or bool or int or long or double or decimal or DateOnly or TimeOnly or DateTime or TimeSpan;
}
