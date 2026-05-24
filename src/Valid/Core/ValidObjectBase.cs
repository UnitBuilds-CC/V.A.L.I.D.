using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.Json;
using System.Numerics;

namespace Valid;

/// <summary>
/// Base class for all VALID objects.
/// </summary>
public abstract class ValidObjectBase : IValidObject, INotifyPropertyChanged
{
    protected System.UInt128 _dirtyFlags;
    protected System.UInt128 _busyFlags;
    protected System.UInt128 _errorFlags;
    protected System.UInt128 _stateFlags;
    protected System.UInt128 _immutableFlags;

    private SpinLock _maskSpinLock = new SpinLock();

    private readonly string[] _stateHistory = new string[16];
    private int _historyIndex = 0;
    protected bool _isRestoring = false;
    public int SlabIndex { get; set; } = -1;

    public System.UInt128 DirtyFlags => _dirtyFlags;
    public System.UInt128 BusyFlags => _busyFlags;
    public System.UInt128 ErrorFlags => _errorFlags;
    public System.UInt128 StateFlags => _stateFlags;
    public bool IsDirty => _dirtyFlags != System.UInt128.Zero;
    public bool IsNew { get; set; } = true;
    public bool IsValid => _errorFlags == System.UInt128.Zero;
    public void Validate() 
    {
        var oldErrors = _errorFlags;
        var newErrors = CalculateValidationState();
        if (oldErrors == newErrors) return;

        _errorFlags = newErrors;

        var diff = oldErrors ^ newErrors;
        if (diff == System.UInt128.Zero) return;

        ulong low = (ulong)(diff & ulong.MaxValue);
        while (low != 0)
        {
            int i = BitOperations.TrailingZeroCount(low);
            OnBitPulse(i, 1);
            low &= ~(1UL << i);
        }

        ulong high = (ulong)(diff >> 64);
        while (high != 0)
        {
            int i = BitOperations.TrailingZeroCount(high);
            OnBitPulse(i + 64, 1);
            high &= ~(1UL << i);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int, int>? BitPulse;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void OnBitPulse(int bitIndex, int maskType)
    {
        if (SlabIndex >= 0)
        {
            WebWorkerBridge.SetObjectState(SlabIndex, _dirtyFlags, _busyFlags, _errorFlags);
        }
        BitPulse?.Invoke(bitIndex, maskType);
    }

    private void CaptureSnapshot()
    {
        LockMasks(() => {
            var originalDirty = _dirtyFlags;
            
            _dirtyFlags = ~System.UInt128.Zero; 
            
            var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>(256);
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                WriteDelta(writer);
            }
            _stateHistory[_historyIndex] = System.Text.Encoding.UTF8.GetString(bufferWriter.WrittenSpan);

            _dirtyFlags = originalDirty; 
            _historyIndex = (_historyIndex + 1) % _stateHistory.Length;
        });
    }

    public abstract void WriteDelta(System.Text.Json.Utf8JsonWriter writer);

    /// <summary>
    /// Executes action under mask spin lock.
    /// </summary>
    protected void LockMasks(Action action)
    {
        bool lockTaken = false;
        try
        {
            _maskSpinLock.Enter(ref lockTaken);
            action();
        }
        finally
        {
            if (lockTaken) _maskSpinLock.Exit();
        }
    }

    public string[] GetStateHistory() => _stateHistory;

    /// <summary>
    /// Sets property and dirty bit.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, int bitIndex, [CallerMemberName] string? propertyName = null)
    {
        bool changed = false;
        bool lockTaken = false;
        try
        {
            _maskSpinLock.Enter(ref lockTaken);
            if ((_immutableFlags & ((System.UInt128)1 << bitIndex)) != 0) return false;

            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            _dirtyFlags |= ((System.UInt128)1 << bitIndex);
            changed = true;
        }
        finally
        {
            if (lockTaken) _maskSpinLock.Exit();
        }

        if (changed)
        {
            OnBitPulse(bitIndex, 0);
            OnPropertyChanged(propertyName);
            if (!_isRestoring) CaptureSnapshot();
        }
        return changed;
    }

    public abstract IEnumerable<DiagnosticResult> GetDiagnostics();

    public abstract string GetValidMetadata();

    public abstract void SetPropertyValue(string propertyName, object value);

    public abstract object? GetPropertyValue(string propertyName);

    public abstract string GetDeltaJson();

    public abstract System.Type GetPropertyType(string propertyName);

    public abstract int GetBitIndex(string propertyName);

    /// <summary>
    /// Resets dirty flags.
    /// </summary>
    public void ResetDirty()
    {
        bool lockTaken = false;
        try
        {
            _maskSpinLock.Enter(ref lockTaken);
            _dirtyFlags = System.UInt128.Zero;
        }
        finally
        {
            if (lockTaken) _maskSpinLock.Exit();
        }
    }

    /// <summary>
    /// Restores history snapshot.
    /// </summary>
    public void RestoreHistory(int stepsBack)
    {
        if (stepsBack < 0 || stepsBack >= _stateHistory.Length) return;
        int targetIndex = (_historyIndex - 1 - stepsBack) % _stateHistory.Length;
        if (targetIndex < 0) targetIndex += _stateHistory.Length;
        
        var json = _stateHistory[targetIndex];
        if (!string.IsNullOrEmpty(json)) 
        {
            _isRestoring = true;
            try 
            {
                ApplyRawDelta(json);
                _stateFlags |= 0x01;
                OnPropertyChanged("RestoreHistory");
            }
            finally 
            { 
                _isRestoring = false; 
            }
        }
    }

    /// <summary>
    /// Applies JSON delta.
    /// </summary>
    public virtual void ApplyRawDelta(string json)
    {
        UpdatePropertyFromJson("", json);
    }

    public abstract void UpdatePropertyFromJson(string propertyName, string jsonValue);

    /// <summary>
    /// Calculates validation state.
    /// </summary>
    public abstract System.UInt128 CalculateValidationState();
}
