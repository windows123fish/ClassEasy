using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace ClassEasy.Services;

/// <summary>
/// 轻量 .xlsx 读取器：xlsx 本质是 zip + XML，直接用 System.IO.Compression 解析，
/// 避免引入 ClosedXML / OpenXML 重型依赖（体积小、Win7 完全兼容）。
/// 只读取第一个工作表，返回按行列索引（从 0 开始）的二维字符串数组。
/// </summary>
public static class XlsxReader
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ns = SpreadsheetNs;
    private static readonly XNamespace R = RelNs;
    private static readonly XNamespace PackageR = XNamespace.Get(PackageRelNs);

    /// <summary>
    /// 读取 xlsx 第一个工作表。失败时返回 null 并输出 error 信息。
    /// </summary>
    public static string[,]? ReadSheet(string path, out string error)
    {
        error = string.Empty;
        try
        {
            using var archive = ZipFile.OpenRead(path);

            // 1. sharedStrings（可选）
            var sharedStrings = ReadSharedStrings(archive);

            // 2. 样式（用于识别时间单元格）
            var sharedStyles = ReadSharedStyles(archive);

            // 3. 找到第一个 sheet 的文件路径
            var sheetPath = GetFirstSheetPath(archive);
            if (sheetPath is null)
            {
                error = "未找到工作表（sheet）数据。";
                return null;
            }

            var sheetEntry = archive.GetEntry(sheetPath);
            if (sheetEntry is null)
            {
                error = "无法读取工作表内容。";
                return null;
            }

            using var sheetStream = sheetEntry.Open();
            var sheetDoc = XDocument.Load(sheetStream);

            // 4. 收集所有行，确定最大行/列
            var rows = sheetDoc.Root!.Elements(Ns + "sheetData").Elements(Ns + "row").ToList();
            if (rows.Count == 0)
            {
                error = "工作表是空的。";
                return null;
            }

            int maxRow = 0, maxCol = 0;
            foreach (var row in rows)
            {
                int r = (int)ParseRowIndex(row.Attribute("r")?.Value);
                maxRow = Math.Max(maxRow, r);
                foreach (var c in row.Elements(Ns + "c"))
                {
                    var (_, col) = ParseCellRef(c.Attribute("r")?.Value);
                    maxCol = Math.Max(maxCol, col);
                }
            }

            // 5. 填充二维数组
            var grid = new string[maxRow, maxCol + 1];
            foreach (var row in rows)
            {
                int r = (int)ParseRowIndex(row.Attribute("r")?.Value) - 1;
                foreach (var c in row.Elements(Ns + "c"))
                {
                    var refStr = c.Attribute("r")?.Value;
                    if (refStr is null) continue;
                    var (_, colIdx) = ParseCellRef(refStr);
                    grid[r, colIdx] = GetCellText(c, sharedStrings, sharedStyles);
                }
            }

            return grid;
        }
        catch (Exception ex)
        {
            error = "解析失败：" + ex.Message;
            return null;
        }
    }

    private static List<string>? ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return null;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        return doc.Root!
            .Elements(Ns + "si")
            .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)))
            .ToList();
    }

    /// <summary>
    /// 读取 styles.xml，提取 numFmtId 列表（cellXfs/xf/numFmtId），
    /// 用于判断某个单元格是否应用了时间格式。
    /// </summary>
    private static int[]? ReadSharedStyles(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/styles.xml");
        if (entry is null) return null;

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        return doc.Root?
            .Element(Ns + "cellXfs")?
            .Elements(Ns + "xf")
            .Select(xf =>
            {
                var raw = xf.Attribute("numFmtId")?.Value;
                return int.TryParse(raw, out var n) ? n : 0;
            })
            .ToArray();
    }

    /// <summary>
    /// 内置 numFmtId 集合：14/15/16/17/22/45/46/47 是 Excel 内置的时间格式。
    /// 自定义格式（numFmtId >= 164）若 formatCode 含 'h' 或 's' 也视为时间。
    /// </summary>
    private static readonly HashSet<int> BuiltinTimeFormatIds = new() { 14, 15, 16, 17, 22, 45, 46, 47 };

    private static bool IsTimeFormatStyle(string styleIdxStr, int[]? sharedStyles)
    {
        if (sharedStyles is null) return false;
        if (!int.TryParse(styleIdxStr, out var idx) || idx < 0 || idx >= sharedStyles.Length)
            return false;
        var fmtId = sharedStyles[idx];
        if (BuiltinTimeFormatIds.Contains(fmtId)) return true;
        return false; // 自定义格式不在此实现范围，避免误判
    }

    private static string? GetFirstSheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        if (workbookEntry is null || relsEntry is null) return null;

        XDocument workbook;
        using (var ws = workbookEntry.Open()) workbook = XDocument.Load(ws);

        XDocument rels;
        using (var rs = relsEntry.Open()) rels = XDocument.Load(rs);

        // 第一个 sheet 的 r:id
        var sheetId = workbook.Root!
            .Elements(Ns + "sheets").Elements(Ns + "sheet").FirstOrDefault()?
            .Attribute(R + "id")?.Value;
        if (sheetId is null) return null;

        // r:id -> Target（.rels 文件中的 Relationship 元素属于 package 命名空间）
        var rel = rels.Root!
            .Elements(PackageR + "Relationship")
            .FirstOrDefault(x => x.Attribute("Id")?.Value == sheetId);
        var target = rel?.Attribute("Target")?.Value;
        if (target is null) return null;

        return "xl/" + target;
    }

    private static string GetCellText(XElement c, List<string>? sharedStrings, int[]? sharedStyles)
    {
        var t = c.Attribute("t")?.Value;
        var v = c.Element(Ns + "v")?.Value;

        switch (t)
        {
            case "s": // shared string
                if (v is not null && int.TryParse(v, out var idx) && sharedStrings is not null && idx >= 0 && idx < sharedStrings.Count)
                    return sharedStrings[idx].Trim();
                return string.Empty;
            case "inlineStr": // inline string
                return string.Concat(c.Descendants(Ns + "t").Select(x => x.Value)).Trim();
            case "str": // formula string
                return v?.Trim() ?? string.Empty;
            case "b": // boolean
                return v == "1" ? "TRUE" : "FALSE";
            default: // numeric
                if (v is null) return string.Empty;
                // 仅当单元格显式标记为时间格式（numFmtId ∈ 内置时间格式集）时才把序列号转成 HH:mm，
                // 否则保持数字原文，避免把 0.3333（分数）误识别为 08:00。
                if (c.Attribute("s")?.Value is { } styleIdx
                    && IsTimeFormatStyle(styleIdx, sharedStyles)
                    && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var num)
                    && num >= 0 && num < 1)
                {
                    // 转秒后四舍五入，避免浮点误差（如 8:00 显示成 07:59）
                    var ts = TimeSpan.FromSeconds(Math.Round(num * 86400));
                    return ts.ToString(@"hh\:mm");
                }
                return v.Trim();
        }
    }

    private static (int row, int col) ParseCellRef(string? refStr)
    {
        if (string.IsNullOrEmpty(refStr))
            return (0, 0);

        int col = 0;
        int i = 0;
        while (i < refStr.Length && char.IsLetter(refStr[i]))
        {
            col = col * 26 + (refStr[i] - 'A' + 1);
            i++;
        }

        var rowStr = refStr.Substring(i);
        int row = int.TryParse(rowStr, out var r) ? r : 0;
        return (row, col - 1);
    }

    private static int ParseRowIndex(string? s)
        => int.TryParse(s, out var r) ? r : 0;
}
