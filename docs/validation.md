# Validation & Rules

V.A.L.I.D. provides declarative validation through attributes, with both synchronous and asynchronous rule engines.

## Built-in Rules

### Required

```csharp
[Required]
public string CustomerName { get; set; } = "";
```

Fails if `null`, empty, or whitespace (for strings).

### Range

```csharp
[Range(0, 1_000_000)]
public decimal Amount { get; set; }

[Range(1, 100)]
public int Quantity { get; set; }
```

Works with any `IComparable`: `int`, `decimal`, `double`, `DateTime`, etc.

### Authorized

```csharp
[Authorized("Admin")]
public decimal CreditLimit { get; set; }
```

Role-based field access control. Set the resolver at startup:

```csharp
ValidSecurity.IsInRole = role => currentUser.Roles.Contains(role);
```

## Custom Rules

Extend `RuleAttribute`:

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class FutureDateAttribute : RuleAttribute
{
    public override bool IsValid(object? value, object container)
    {
        return value is DateTime dt && dt > DateTime.Today;
    }
}

// Usage
[FutureDate(ErrorMessage = "Due date must be in the future")]
public DateTime DueDate { get; set; }
```

### RuleAttribute Properties

| Property | Type | Description |
|---|---|---|
| `ErrorMessage` | `string?` | Custom error message |
| `TriggersOn` | `string[]?` | Other property names that trigger re-validation |
| `Severity` | `RuleSeverity` | `Error`, `Warning`, or `Information` |
| `Scope` | `string?` | Grouping scope |
| `RunAt` | `RuleLocation` | `Client`, `Server`, or `Both` |

## Synchronous Rule Engine

```csharp
// Validate entire object
var result = RuleEngine.Validate(invoice);

// Validate single property (e.g. on field change)
var result = RuleEngine.Validate(invoice, "Amount");

// Check result
if (!result.Success)
{
    foreach (var diag in result.Diagnostics)
    {
        // diag.PropertyName, diag.Message, diag.Severity, diag.Code
    }
}
```

The rule engine:
1. Scans `[ValidProperty]` attributes on the target property
2. Evaluates each rule's `IsValid()` method
3. Populates the object's `Diagnostics` collection
4. Sets error flags via `SetError(propertyName, hasError)`

## Async Rule Engine

For rules that call remote APIs, databases, or other async operations:

```csharp
public class UniqueEmailAttribute : AsyncRuleAttribute
{
    public override async Task<bool> IsValidAsync(object? value, object container)
    {
        if (value is not string email) return true;
        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"/api/check-email?email={email}");
        return response.IsSuccessStatusCode;
    }
}

// Usage
[UniqueEmail]
public string Email { get; set; } = "";
```

Run async validation:

```csharp
var result = await AsyncRuleEngine.ValidateAsync(invoice);
```

During async validation:
- `SetBusy(propertyName, true)` is called → UI shows spinner
- Rule executes asynchronously
- `SetBusy(propertyName, false)` when complete
- `SetError(propertyName, !isValid)` with result

All async rules run in parallel via `Task.WhenAll`.

## Diagnostics in the UI

The `ValidValidator` component bridges V.A.L.I.D. diagnostics to Blazor's `EditContext`:

```razor
<EditForm Model="@_invoice">
    <ValidValidator />  <!-- Wires up validation automatically -->
    <ValidField TValue="string" Model="@_invoice" PropertyName="CustomerName" />
</EditForm>
```

Or use `ValidForm` which includes `ValidValidator` automatically:

```razor
<ValidForm Model="@_invoice" OnValidSubmit="HandleSave">
    <ValidField TValue="string" Model="@_invoice" PropertyName="CustomerName" />
</ValidForm>
```
