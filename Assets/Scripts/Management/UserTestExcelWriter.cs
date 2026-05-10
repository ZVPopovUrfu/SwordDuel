using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

public static class UserTestExcelWriter
{
    public static void WriteWorkbook(
        string filePath,
        IReadOnlyList<string> roundHeaders,
        IReadOnlyList<string[]> roundRows,
        IReadOnlyList<string> actionHeaders,
        IReadOnlyList<string[]> actionRows)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is empty.");

        string folder = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        string tempPath = filePath + ".tmp";

        if (File.Exists(tempPath))
            File.Delete(tempPath);

        using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite))
        using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            AddEntry(archive, "[Content_Types].xml", GetContentTypesXml());
            AddEntry(archive, "_rels/.rels", GetRootRelsXml());
            AddEntry(archive, "xl/workbook.xml", GetWorkbookXml());
            AddEntry(archive, "xl/_rels/workbook.xml.rels", GetWorkbookRelsXml());
            AddEntry(archive, "xl/styles.xml", GetStylesXml());

            AddEntry(
                archive,
                "xl/worksheets/sheet1.xml",
                BuildSheetXml("Rounds", roundHeaders, roundRows)
            );

            AddEntry(
                archive,
                "xl/worksheets/sheet2.xml",
                BuildSheetXml("Actions", actionHeaders, actionRows)
            );
        }

        if (File.Exists(filePath))
            File.Delete(filePath);

        File.Move(tempPath, filePath);
    }

    private static void AddEntry(ZipArchive archive, string entryName, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Fastest); using (Stream stream = entry.Open())
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(content);
        }
    }

    private static string BuildSheetXml(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");
        sb.AppendLine(@"<sheetViews><sheetView workbookViewId=""0""/></sheetViews>");
        sb.AppendLine(@"<sheetFormatPr defaultRowHeight=""15""/>");
        sb.AppendLine(@"<sheetData>");

        int rowIndex = 1;

        if (headers != null && headers.Count > 0)
        {
            WriteRow(sb, rowIndex, headers);
            rowIndex++;
        }

        if (rows != null)
        {
            foreach (string[] row in rows)
            {
                WriteRow(sb, rowIndex, row);
                rowIndex++;
            }
        }

        sb.AppendLine(@"</sheetData>");
        sb.AppendLine(@"</worksheet>");

        return sb.ToString();
    }

    private static void WriteRow(StringBuilder sb, int rowIndex, IReadOnlyList<string> values)
    {
        sb.Append(@"<row r=""").Append(rowIndex).Append(@""">");

        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                string cellRef = GetColumnName(i + 1) + rowIndex;
                string value = values[i] ?? "";

                sb.Append(@"<c r=""").Append(cellRef).Append(@""" t=""inlineStr""><is><t>");
                sb.Append(EscapeXml(value));
                sb.Append(@"</t></is></c>");
            }
        }

        sb.AppendLine(@"</row>");
    }

    private static string GetColumnName(int columnNumber)
    {
        string columnName = "";

        while (columnNumber > 0)
        {
            int modulo = (columnNumber - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnNumber = (columnNumber - modulo) / 26;
        }

        return columnName;
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string GetContentTypesXml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/worksheets/sheet2.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
</Types>";
    }

    private static string GetRootRelsXml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";
    }

    private static string GetWorkbookXml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""Rounds"" sheetId=""1"" r:id=""rId1""/>
    <sheet name=""Actions"" sheetId=""2"" r:id=""rId2""/>
  </sheets>
</workbook>";
    }

    private static string GetWorkbookRelsXml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet2.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";
    }

    private static string GetStylesXml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""1""><font><sz val=""11""/><name val=""Calibri""/></font></fonts>
  <fills count=""1""><fill><patternFill patternType=""none""/></fill></fills>
  <borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/></cellXfs>
</styleSheet>";
    }
}
