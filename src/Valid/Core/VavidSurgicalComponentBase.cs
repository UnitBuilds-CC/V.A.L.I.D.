using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace Valid;

/// <summary>
/// Surgical component updating only on bit pulse.
/// </summary>
public abstract class VavidSurgicalComponentBase : ComponentBase, IDisposable
{
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<VavidSurgicalComponentBase>? Logger { get; set; }

    [Parameter] public IValidObject? Model { get; set; }
    [Parameter] public int BitIndex { get; set; }

    public static bool ForceStandardRendering { get; set; } = false;

    private IValidObject? _hookedModel;
    private DotNetObjectReference<VavidSurgicalComponentBase>? _dotNetRef;

    private ILogger _logger => Logger ?? NullLogger<VavidSurgicalComponentBase>.Instance;

    protected override void OnParametersSet()
    {
        if (Model != _hookedModel)
        {
            if (_hookedModel != null)
            {
                _hookedModel.BitPulse -= OnBitPulse;
            }
            
            _hookedModel = Model;
            
            if (_hookedModel != null)
            {
                _hookedModel.BitPulse += OnBitPulse;
            }
        }
    }

    protected override bool ShouldRender() => ForceStandardRendering || _shouldRender;
    private bool _shouldRender = true;

    private void OnBitPulse(int bitIndex, int maskType)
    {
        if (bitIndex == BitIndex)
        {
            _shouldRender = true;
            InvokeAsync(StateHasChanged);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Model != null)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("vavid.registerObject", 
                    Model.ValidId, 
                    Model.GetValidMetadata(),
                    _dotNetRef);
            }
            catch (Exception ex) 
            { 
                _logger.LogWarning("[VAVID] Surgical component registration failed: {Message}", ex.Message);
            }
        }
        
        _shouldRender = false;
    }

    [JSInvokable]
    public void UpdateProperty(string propertyName, string jsonValue)
    {
        Model?.UpdatePropertyFromJson(propertyName, jsonValue);
    }

    public void Dispose()
    {
        if (_hookedModel != null)
        {
            _hookedModel.BitPulse -= OnBitPulse;
        }
        _dotNetRef?.Dispose();
    }
}
