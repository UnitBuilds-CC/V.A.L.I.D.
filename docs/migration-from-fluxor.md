# Migration from Fluxor to V.A.L.I.D.

Fluxor (Redux-pattern) and V.A.L.I.D. represent two different philosophies. This guide helps you move from "Global State Store" to "Object-Oriented Lifecycle".

## Concept Mapping

| Fluxor Concept | V.A.L.I.D. Equivalent | Difference |
|---|---|---|
| `State<T>` | `IValidObject` | State is encapsulated in the object itself, not a global store. |
| `Action` | Property Setter | Just set the property. No need to dispatch events. |
| `Reducer` | `RuleEngine` / Property Logic | Logic lives next to the data in the object or shuttle. |
| `Effect` | `AsyncRuleAttribute` / Shuttle | Side effects are managed by the persistence layer or async rules. |
| `Feature` | `ValidObject` | Features are modeled as rich business objects. |

## Why Migrate?
- **Reduced Boilerplate**: V.A.L.I.D. removes the need for Actions, Reducers, and Store boilerplate.
- **Local-First**: V.A.L.I.D. objects handle their own dirty tracking and validation without global overhead.
- **HUD Integration**: V.A.L.I.D. provides a visual diagnostic layer (HUD) that Fluxor lacks.

## Migration Steps

1. **Entities to ValidObjects**: Turn your State records into classes marked with `[ValidObject]`.
2. **Selectors to Properties**: Convert complex Selectors into computed properties (C# `get => ...`) on your object.
3. **Actions to Methods**: Instead of dispatching an action, call a method on your object.
4. **Reducers to Rules**: Move validation logic from reducers into property attributes (`[Required]`, `[Range]`).
5. **Effects to Shuttles**: Move API calls from Effects into `IDataShuttle.SaveAsync`.

## Example: Before (Fluxor)
```csharp
public record CounterState(int ClickCount);
public class IncrementCounterAction {}
public static class Reducers {
    [ReducerMethod]
    public static CounterState Reduce(CounterState state, IncrementCounterAction action) 
        => state with { ClickCount = state.ClickCount + 1 };
}
```

## Example: After (V.A.L.I.D.)
```csharp
[ValidObject]
public class Counter : IValidObject {
    public int ClickCount { get; set; }
    public void Increment() => ClickCount++;
}
```
