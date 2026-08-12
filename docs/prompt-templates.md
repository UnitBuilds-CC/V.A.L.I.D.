# V.A.L.I.D. Prompt Templates

Ready-to-use prompt templates for LLMs to generate V.A.L.I.D. code.

## Creating a Valid Object

```
Create a V.A.L.I.D. object called [ClassName] with the following properties:
- [PropertyName]: [Type] with validation [Required/Range/StringLength]
- [PropertyName]: [Type] with validation [Required/Range/StringLength]

Requirements:
1. Mark the class with [ValidObject]
2. Inherit from ValidObjectBase
3. Make the class partial
4. Add [ValidProperty] to each property
5. Use SetProperty(ref field, value, bitIndex) in property setters
6. Add appropriate validation attributes ([Required], [Range], [StringLength])
```

## Adding Validation Rules

```
Add validation to my V.A.L.I.D. object:
- [PropertyName] must be [required/between X and Y/max length Z]
- Error code: [CODE-001]
- Error message: "[Custom error message]"
```

## Blazor Integration

```
Create a Blazor component that uses V.A.L.I.D.:
1. Inherit from VavidComponentBase
2. Set the Model parameter to my ValidObject
3. Add ChildContent for rendering
4. Handle BitPulse events for surgical updates
```

## MCP Integration

```
Set up MCP tools for my V.A.L.I.D. objects:
1. Register the MCP server with AddValidMcpServer()
2. Use valid_list_instances to see live objects
3. Use valid_inspect_state to check bitmask state
4. Use valid_mutate_property to test changes
```

## Configuration

```
Configure V.A.L.I.D. with:
- Outbox connection string: [Data Source=mydb.db]
- Encryption key: [32-byte key or null]
- Registry max capacity: [10000]
- Default batch size: [10]
```

## Example: Complete Customer Object

```csharp
using Valid;

[ValidObject]
public partial class Customer : ValidObjectBase
{
    private string _name = "";
    private int _age;

    [ValidProperty]
    [Required("Name is required", "CUST-001")]
    [StringLength(100, "Name too long", "CUST-002", MinimumLength = 3)]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, 0);
    }

    [ValidProperty]
    [Range(18, 120, "Age must be 18-120", "CUST-003")]
    public int Age
    {
        get => _age;
        set => SetProperty(ref _age, value, 1);
    }
}
```
