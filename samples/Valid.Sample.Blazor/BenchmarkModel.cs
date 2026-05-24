using Valid;
using System.Collections.Generic;

namespace Valid.Sample.Blazor;

[ValidObject]
public partial class BenchmarkItem : ValidObjectBase
{
    [ValidProperty] public string Value1 { get; set; } = "";
    [ValidProperty] public string Value2 { get; set; } = "";
    [ValidProperty] public string Value3 { get; set; } = "";
    [ValidProperty] public string Value4 { get; set; } = "";
    [ValidProperty] public string Value5 { get; set; } = "";

    // Industrial Cascading Logic
    private int _quantity;
    private int _unitPrice;
    private int _total;

    public int Quantity 
    { 
        get => _quantity; 
        set { _quantity = value; Recalculate(); } 
    }
    
    public int UnitPrice 
    { 
        get => _unitPrice; 
        set { _unitPrice = value; Recalculate(); } 
    }
    
    public int Total => _total;

    private void Recalculate()
    {
        _total = _quantity * _unitPrice;
        if (SlabIndex > 0)
        {
            WebWorkerBridge.SetObjectValues(SlabIndex, _quantity, _unitPrice, _total);
        }
    }
}

public class BenchmarkModel
{
    public List<BenchmarkItem> Items { get; set; } = new();

    public BenchmarkModel(int totalFields)
    {
        int itemsCount = totalFields / 5;
        for (int i = 0; i < itemsCount; i++)
        {
            Items.Add(new BenchmarkItem());
        }
    }
}
