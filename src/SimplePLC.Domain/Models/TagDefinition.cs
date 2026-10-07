using SimplePLC.Domain.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Định nghĩa thực thể nghiệp vụ của một Tag trong hệ thống SimplePLC.
/// </summary>
public sealed class TagDefinition
{
    public ushort TagIndex { get; }
    public string Name { get; }
    public TagKind Kind { get; }
    public TagDataType DataType { get; }
    public bool IsReadOnly { get; }
    public string Description { get; }

    public TagDefinition(
        ushort tagIndex,
        string name,
        TagKind kind,
        TagDataType dataType,
        bool isReadOnly,
        string description = "")
    {
        TagIndex = tagIndex;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Tag name cannot be empty.", nameof(name)) : name;
        Kind = kind;
        DataType = dataType;
        IsReadOnly = isReadOnly;
        Description = description ?? string.Empty;
    }

    public override string ToString() => $"{Name} (Index={TagIndex}, Kind={Kind}, Type={DataType})";
}
