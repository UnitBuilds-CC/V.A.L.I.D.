using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;

namespace Valid;

/// <summary>
/// Base class for Blazor components using the VALID engine.
/// </summary>
public abstract class VavidComponentBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IValidObject? Model { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private IValidObject? _currentModel;

    public static bool SuppressSurgicalUpdates { get; set; } = false;

    protected override async Task OnInitializedAsync()
    {
        await SyncModelAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await SyncModelAsync();
    }

    private async Task SyncModelAsync()
    {
        if (Model == _currentModel) return;

        if (_currentModel is ValidObjectBase oldVob)
        {
            oldVob.PropertyChanged -= OnModelPropertyChanged;
            try 
            {
                await JSRuntime.InvokeVoidAsync("vavid.unregisterObject", _currentModel.GetHashCode().ToString());
            }
            catch (Exception) { }
        }

        _currentModel = Model;

        if (_currentModel is ValidObjectBase newVob)
        {
            newVob.PropertyChanged += OnModelPropertyChanged;
            
            try 
            {
                var dotNetRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("vavid.registerObject", 
                    _currentModel.GetHashCode().ToString(), 
                    _currentModel.GetValidMetadata(),
                    dotNetRef);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Vavid Extension Registration Failed: {ex.Message}");
            }
        }
    }

    [JSInvokable]
    public void UpdateProperty(string propertyName, string jsonValue)
    {
        if (Model == null) return;

        try
        {
            Model.UpdatePropertyFromJson(propertyName, jsonValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VAVID] Reflection-Free Update Failed for {propertyName}: {ex.Message}");
        }
    }

    [JSInvokable]
    public void RestoreHistory(int stepsBack)
    {
        if (Model is ValidObjectBase vob)
        {
            vob.RestoreHistory(stepsBack);
        }
    }

    private bool _isUpdating = false;

    private async void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Model == null || _isUpdating || SuppressSurgicalUpdates) return;

        try 
        {
            _isUpdating = true;
            await JSRuntime.InvokeVoidAsync("vavid.updateState", 
                Model.GetHashCode().ToString(), 
                Model.DirtyFlags.ToString("X"), 
                Model.BusyFlags.ToString("X"), 
                Model.ErrorFlags.ToString("X"));

            await JSRuntime.InvokeVoidAsync("vavid.logDelta",
                Model.GetHashCode().ToString(),
                Model.GetDeltaJson());
        }
        catch (JSDisconnectedException) { }
        catch (Exception) { }
        finally
        {
            _isUpdating = false;
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (Model is ValidObjectBase vob)
        {
            vob.PropertyChanged -= OnModelPropertyChanged;
            try 
            {
                await JSRuntime.InvokeVoidAsync("vavid.unregisterObject", Model.GetHashCode().ToString());
            }
            catch (Exception) { }
        }
        GC.SuppressFinalize(this);
    }

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        if (ChildContent != null)
        {
            builder.OpenComponent<CascadingValue<IValidObject>>(0);
            builder.AddAttribute(1, "Value", Model);
            builder.AddAttribute(2, "IsFixed", true);
            builder.AddAttribute(3, "ChildContent", ChildContent);
            builder.CloseComponent();
        }
    }
}

/// <summary>
/// Generic base class for Vavid components.
/// </summary>
public abstract class VavidComponent<T> : VavidComponentBase where T : class, IValidObject
{
    [Parameter, EditorRequired]
    public T Value { get; set; } = default!;

    protected override void OnParametersSet()
    {
        Model = Value;
    }
}
