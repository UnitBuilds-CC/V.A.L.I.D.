using Valid;

namespace Valid.Testing.Models;

[ValidObject]
public partial class BasicTestModel
{
    [ValidProperty]
    public partial int Age { get; set; }

    [ValidProperty]
    public partial string Name { get; set; }
}

[ValidObject]
public partial class AdvancedTestModel
{
    [ValidField]
    private decimal _balance;
    
    [ValidField]
    private BasicTestModel? _childInfo;
}
