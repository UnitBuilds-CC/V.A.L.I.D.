# Blazor Components

V.A.L.I.D. provides drop-in Blazor components that integrate with MudBlazor and Blazor's `EditContext` validation system.

**Package:** `Valid.Infrastructure.Components`

## ValidForm

A wrapper around `<EditForm>` that includes `ValidValidator` automatically and runs `RuleEngine.Validate()` on submit:

```razor
<ValidForm Model="@_invoice" OnValidSubmit="HandleSave">
    <ValidField TValue="string" Model="@_invoice" PropertyName="CustomerName" Label="Customer" />
    <ValidField TValue="decimal" Model="@_invoice" PropertyName="Amount" Label="Amount" />
    <MudButton ButtonType="ButtonType.Submit" Disabled="@(!_invoice.IsDirty)">Save</MudButton>
</ValidForm>
```

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `Model` | `IValidObject` | The business object to validate |
| `ChildContent` | `RenderFragment` | Form content |
| `OnValidSubmit` | `EventCallback<EditContext>` | Called when validation passes |
| `OnInvalidSubmit` | `EventCallback<EditContext>` | Called when validation fails |

## ValidField\<TValue\>

A MudBlazor `MudTextField` wrapper that reads/writes via `IValidObject.GetPropertyValue<T>()` and shows validation state:

```razor
<ValidField TValue="string" 
            Model="@_batch" 
            PropertyName="Description" 
            Label="Description" 
            ReadOnly="false" />
```

### Features
- **Auto-dirty pulse**: CSS class `valid-pulse` applied when the model is dirty
- **Busy indicator**: Shows `MudProgressCircular` spinner when `IsBusy` is true (async validation running)
- **Error integration**: Uses Blazor `EditContext` validation messages for error display
- **Auto-label**: Falls back to `PropertyName` if `Label` is not specified

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `Model` | `IValidObject` | The business object (required) |
| `PropertyName` | `string` | The property to bind (required) |
| `Label` | `string?` | Field label (defaults to PropertyName) |
| `ReadOnly` | `bool` | Read-only mode |

## ValidValidator

Bridges V.A.L.I.D.'s `RuleEngine` + `DiagnosticResult` to Blazor's `EditContext` validation:

```razor
<EditForm Model="@_invoice">
    <ValidValidator />  <!-- Drop this in, you're done -->
    <!-- Your fields here -->
</EditForm>
```

### How It Works

1. **On field change**: Runs `RuleEngine.Validate(obj, propertyName)` for just that field
2. **On submit**: Runs `RuleEngine.Validate(obj)` for the entire object
3. **Maps diagnostics**: Converts `DiagnosticInfo` entries → Blazor `ValidationMessageStore`
4. **Listens to INotifyPropertyChanged**: Re-validates when properties change programmatically

## ValidComponentBase\<T\>

Base class for components that display a single V.A.L.I.D. object:

```csharp
public class InvoiceCard : ValidComponentBase<Invoice>
{
    // Inherited properties:
    //   [Parameter] T Model
    //   bool IsBusy, IsDirty, IsInvalid

    // Auto-re-renders when Model.PropertyChanged fires
}
```

```razor
@inherits ValidComponentBase<Invoice>

<MudCard>
    <MudCardContent>
        <MudText>@Model.CustomerName</MudText>
        <MudText Color="@(IsDirty ? Color.Warning : Color.Default)">
            @Model.Amount.ToString("C")
        </MudText>
    </MudCardContent>
</MudCard>
```

## ValidOptimisticComponentBase\<T\>

Extended base class for optimistic UI patterns with offline-first support:

```csharp
public class InvoiceEditor : ValidOptimisticComponentBase<Invoice>
{
    protected async Task Save()
    {
        await SaveOptimisticAsync("Save");
        // Validates locally, then pushes to SQLite outbox
        // SyncWorker handles background upload
    }
}
```

## ValidInputBase\<TValue\>

Base class for building custom input components:

```csharp
public abstract class ValidInputBase<TValue> : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] IValidObject Model;
    [Parameter, EditorRequired] string PropertyName;

    protected TValue Value
    {
        get => Model.GetPropertyValue<TValue>(PropertyName);
        set => Model.SetPropertyValue<TValue>(PropertyName, value);
    }

    protected bool IsDirty;  // From Model.IsDirty
    protected bool IsBusy;   // From Model.IsBusy
}
```

Use this to build custom inputs (dropdowns, date pickers, etc.) that integrate with V.A.L.I.D.:

```razor
@inherits ValidInputBase<DateTime>

<MudDatePicker Label="@Label"
               Date="@Value"
               DateChanged="v => Value = v"
               Error="@HasError" />
```

## CSS

Add the `valid-pulse` animation in your stylesheet:

```css
.valid-pulse {
    animation: validPulse 0.6s ease-in-out;
}

@keyframes validPulse {
    0% { box-shadow: 0 0 0 rgba(33, 150, 243, 0); }
    50% { box-shadow: 0 0 8px rgba(33, 150, 243, 0.4); }
    100% { box-shadow: 0 0 0 rgba(33, 150, 243, 0); }
}

.valid-busy-indicator {
    position: absolute;
    right: 8px;
    top: 50%;
    transform: translateY(-50%);
}
```
