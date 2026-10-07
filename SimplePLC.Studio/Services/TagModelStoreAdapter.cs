using SimplePLC.Application.Logic.Runtime;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Adapter chuyển đổi danh sách TagModel (ObservableObject của WPF Studio)
/// sang giao diện trung tính ITagValueStore của Application layer.
/// </summary>
public sealed class TagModelStoreAdapter : ITagValueStore
{
    private readonly Dictionary<int, TagModel> _tagMap;

    public TagModelStoreAdapter(IEnumerable<TagModel> tags)
    {
        _tagMap = tags.ToDictionary(t => t.Index);
    }

    public int GetValue(int tagIndex) =>
        _tagMap.TryGetValue(tagIndex, out var tag) ? tag.Value : 0;

    public void SetValue(int tagIndex, int value)
    {
        if (_tagMap.TryGetValue(tagIndex, out var tag))
            tag.Value = value;
    }

    public bool ContainsTag(int tagIndex) => _tagMap.ContainsKey(tagIndex);

    public IReadOnlyCollection<int> AllTagIndices => _tagMap.Keys;
}
