using System.Reflection;
using Valid;

namespace Valid.Sample.Blazor;

public class GridColumnDefinition
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public int BitIndex { get; set; }
    public string TextAlign { get; set; } = "left";
}

public static class GuiGenerator
{
    public static List<GridColumnDefinition> GenerateColumns<T>() where T : IValidObject, new()
    {
        var columns = new List<GridColumnDefinition>();
        var type = typeof(T);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var dummy = new T();
        
        foreach (var prop in properties)
        {
            var attr = prop.GetCustomAttribute<ValidPropertyAttribute>();
            if (attr == null) continue;

            var label = prop.Name;
            var align = "left";
            
            if (prop.PropertyType == typeof(double) || prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(int))
            {
                align = "right";
            }

            columns.Add(new GridColumnDefinition
            {
                Name = prop.Name,
                Label = label.ToUpper(),
                BitIndex = dummy.GetBitIndex(prop.Name),
                TextAlign = align
            });
        }
        
        return columns;
    }
}
