using System;

namespace Valid;

/// <summary>
/// Validation security assertion.
/// </summary>
public static class ValidSecurity
{
    /// <summary>
    /// Verifies client state matches server calculations.
    /// </summary>
    public static void AssertShadowValidation(IValidObject obj)
    {
        var trueState = obj.CalculateValidationState();
        if (obj.ErrorFlags != trueState)
        {
            throw new System.Security.SecurityException(
                $"[VALID ZERO-TRUST] Shadow validation failed. Client reported errors: {obj.ErrorFlags:X}, Server calculated: {trueState:X}");
        }
    }
}
