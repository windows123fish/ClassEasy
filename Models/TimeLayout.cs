using System.Collections.ObjectModel;
using System.Linq;

namespace ClassEasy.Models;

/// <summary>
/// 时间表：一天中所有时间点的集合，如第一节课 08:00-08:45、第二节课 08:55-09:40 等。
/// </summary>
public class TimeLayout
{
    public string Name { get; set; } = "新时间表";

    /// <summary>所有时间点，按开始时间排序</summary>
    public ObservableCollection<TimeLayoutItem> Items { get; set; } = new();

    /// <summary>只包含上课类型的时间点</summary>
    public ObservableCollection<TimeLayoutItem> ClassItems
        => new(Items.Where(i => i.TimeType == 0).OrderBy(i => i.StartTime));

    /// <summary>插入时间点并按开始时间排序</summary>
    public void InsertSorted(TimeLayoutItem item)
    {
        var index = Items.Count(i => i.StartTime < item.StartTime);
        Items.Insert(index, item);
    }

    /// <summary>按开始时间重新排序</summary>
    public void Sort()
    {
        var sorted = Items.OrderBy(i => i.StartTime).ToList();
        Items.Clear();
        foreach (var item in sorted)
            Items.Add(item);
    }
}
