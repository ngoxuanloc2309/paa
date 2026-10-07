namespace SimplePLC.Application.Logic.Runtime;

/// <summary>
/// Giao diện trừu tượng truy xuất và cập nhật giá trị Tag trong môi trường mô phỏng runtime.
/// Giúp tách biệt RuntimeEngine khỏi các lớp ViewModel/ObservableObject của UI Studio (Clean Architecture).
/// </summary>
public interface ITagValueStore
{
    int GetValue(int tagIndex);
    void SetValue(int tagIndex, int value);
    bool ContainsTag(int tagIndex);
    IReadOnlyCollection<int> AllTagIndices { get; }
}
