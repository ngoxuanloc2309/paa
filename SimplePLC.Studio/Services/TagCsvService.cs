using System.IO;
using System.Text;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Services;

public static class TagCsvService
{
    public static void ExportCsv(string filePath, IEnumerable<TagModel> tags)
    {
        var sb = new StringBuilder();
        // Header
        sb.AppendLine("Index,Name,Alias,Kind,Channel,Group,Value");

        foreach (var tag in tags)
        {
            string alias = EscapeCsvField(tag.Alias);
            string group = EscapeCsvField(tag.Group);
            sb.AppendLine($"{tag.Index},{tag.Name},{alias},{tag.Kind},{tag.Channel},{group},{tag.Value}");
        }

        // Use UTF8 with BOM so Microsoft Excel renders Vietnamese characters correctly
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        File.WriteAllText(filePath, sb.ToString(), encoding);
    }

    public static (int updatedCount, int addedCount, List<string> errors) ImportCsv(string filePath, TagCatalogViewModel catalog)
    {
        var errors = new List<string>();
        int updated = 0;
        int added = 0;

        if (!File.Exists(filePath))
        {
            errors.Add($"File not found: {filePath}");
            return (0, 0, errors);
        }

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length <= 1)
        {
            errors.Add("File is empty or only contains headers.");
            return (0, 0, errors);
        }

        // Parse header to map columns
        var header = ParseCsvLine(lines[0]);
        int idxCol = FindColumnIndex(header, "Index");
        int nameCol = FindColumnIndex(header, "Name");
        int aliasCol = FindColumnIndex(header, "Alias");
        int kindCol = FindColumnIndex(header, "Kind");
        int chanCol = FindColumnIndex(header, "Channel");
        int groupCol = FindColumnIndex(header, "Group");
        int valCol = FindColumnIndex(header, "Value");

        if (nameCol < 0)
        {
            errors.Add("CSV header must contain at least a 'Name' column.");
            return (0, 0, errors);
        }

        for (int lineNo = 2; lineNo <= lines.Length; lineNo++)
        {
            string line = lines[lineNo - 1].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count <= nameCol)
            {
                errors.Add($"Line {lineNo}: insufficient columns.");
                continue;
            }

            string name = cols[nameCol].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            string alias = (aliasCol >= 0 && aliasCol < cols.Count) ? cols[aliasCol].Trim() : string.Empty;
            string group = (groupCol >= 0 && groupCol < cols.Count) ? cols[groupCol].Trim() : string.Empty;

            int index = (idxCol >= 0 && idxCol < cols.Count && int.TryParse(cols[idxCol], out int parsedIdx)) ? parsedIdx : -1;
            int channel = (chanCol >= 0 && chanCol < cols.Count && int.TryParse(cols[chanCol], out int parsedChan)) ? parsedChan : 0;
            int value = (valCol >= 0 && valCol < cols.Count && int.TryParse(cols[valCol], out int parsedVal)) ? parsedVal : 0;

            TagKind kind = TagKind.None;
            if (kindCol >= 0 && kindCol < cols.Count)
            {
                kind = ParseTagKind(cols[kindCol]);
            }

            // Look for existing tag by Name or by Index
            var existing = catalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing == null && index >= 0)
            {
                existing = catalog.AllTags.FirstOrDefault(t => t.Index == index);
            }

            if (existing != null)
            {
                if (!string.IsNullOrEmpty(alias)) existing.Alias = alias;
                if (!string.IsNullOrEmpty(group)) existing.Group = group;
                if (kind != TagKind.None) existing.Kind = kind;
                existing.Channel = channel;
                existing.Value = value;
                updated++;
            }
            else
            {
                int newIdx = index >= 0 ? index : (catalog.AllTags.Count > 0 ? catalog.AllTags.Max(t => t.Index) + 1 : 0);
                var newTag = new TagModel
                {
                    Index = newIdx,
                    Name = name,
                    Alias = alias,
                    Kind = kind != TagKind.None ? kind : TagKind.DiscreteInput,
                    Channel = channel,
                    Group = group,
                    Value = value
                };
                catalog.AllTags.Add(newTag);
                added++;
            }
        }

        return (updated, added, errors);
    }

    private static string EscapeCsvField(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static int FindColumnIndex(List<string> header, string colName)
    {
        return header.FindIndex(c => string.Equals(c.Trim(), colName, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        result.Add(sb.ToString());
        return result;
    }

    public static TagKind ParseTagKind(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return TagKind.None;
        string clean = text.Trim();
        if (Enum.TryParse<TagKind>(clean, true, out var parsed))
            return parsed;

        return clean.ToUpperInvariant() switch
        {
            "DI" => TagKind.DiscreteInput,
            "DO" => TagKind.DiscreteOutput,
            "AI" => TagKind.AnalogInput,
            "VFLAG" => TagKind.VirtualFlag,
            "VREG" => TagKind.VirtualRegister,
            "MB_COIL" => TagKind.ModbusCoil,
            "MB_HOLDING" => TagKind.ModbusHolding,
            "VREG_R" or "VREG_RETAIN" => TagKind.VirtualRegisterRetain,
            "COUNTER" => TagKind.Counter,
            _ => TagKind.None
        };
    }
}
