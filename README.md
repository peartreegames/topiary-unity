# Topiary.Unity

Unity Integration for the dialogue scripting tool [topiary](https://github.com/peartreegames/topiary).

Checkout the [syntax](https://peartree.games/topiary/docs/syntax) file if you're new to writing with Topi.

## Installation

Can be installed via the Package Manager > Add Package From Git URL...

This package also depends on the Unity Addressables package, which should automatically be installed, but if you have any issues please install it manually from the Package Manager.

then the repo can be added

`https://github.com/peartreegames/topiary-unity.git`

## Setup

You'll need some sort of singleton DialogueRunner to go between Topiary and your UI.
Here's a rough sketch:

```csharp
public class DialogueRunner : MonoBehaviour
{
    private void Awake()
    {
        Dialogue.OnStart += OnStart;
        Dialogue.OnEnd += OnEnd;
        Dialogue.OnLine += OnLine;
        Dialogue.OnChoices += OnChoices;
    }

    private void OnDestroy()
    {
        Dialogue.OnStart -= OnStart;
        Dialogue.OnEnd -= OnEnd;
        Dialogue.OnLine -= OnLine;
        Dialogue.OnChoices -= OnChoices;
    }

    private void OnStart(Dialogue dialogue)
    {
        // Open/Update the UI
        // Perform any tasks for dialogue to start
    }

    private void OnLine(Dialogue dialogue, Line line, Speaker speaker)
    {
        // Update the UI to display the next line
        // Use dialogue.Continue() to continue the script
    }

    private void OnChoices(Dialogue dialogue, Choice[] choices)
    {
        // Update the UI to display the choices
        // Use dialogue.SelectChoice(int) when the player makes their selection
    }

    private void OnEnd(Dialogue dialogue)
    {
        // Close/Update your UI
        // Perform any clean up tasks
        // Revert back to game mode
    }
}
```

Any `.topi` file will automatically be compiled and converted into a `.topi.byte` bytecode.

Once your file is compiled it will automatically be added to a Topiary Addressables group with the labels `Topiary` and `Topi`.

Add a `Dialogue` MonoBehaviour to a GameObject and select the `.topi` file you want to associate with that Dialogue. 

Trigger the start of the Dialogue with `dialogue.Play()` or `StartCoroutine(dialogue.PlayCoroutine())` if you want to `yield return` on the play loop from another coroutine.

A `Speaker` MonoBehaviour on a GameObject self-registers (keyed by its `Id` field, which must match the speaker name in the `.topi` source) and exposes `OnStartSpeaking` / `OnStopSpeaking` UnityEvents that fire as the VM toggles speakers across lines.

## Functions

Topiary can call external functions that are marked with the `Topi` attribute.
Any **static** function taking a single `TopiValue[]` argument and returning
`TopiValue` is valid.

```csharp
public static class DialogueFunctions
{
    // playAnim will be replaced with the C# method.
    // We give the playAnim function a body in our .topi file for testing.
    // A warning will be shown if any extern isn't set when we start our Dialogue.
    // ex .topi file:
    //      extern const playAnim = |name, clip| {}
    //      playAnim("Player", "Laugh")
    [Topi("playAnim", 2)]
    public static TopiValue PlayAnim(TopiValue[] args)
    {
        var speakerName = args[0];
        var animClip = args[1];
        if (!Dialogue.Speakers.TryGetValue(speakerName.String, out var topi)) return default;
        // get Animator component and play clip
        return default;
    }
}
```

If you target IL2CPP with aggressive code stripping, add `[Preserve]` to the method
or list the assembly in a `link.xml` so the reflection-based discovery can find it.

## TopiValue

I wanted to hide away the [TopiValue](https://github.com/peartreegames/topiary-unity/blob/main/Runtime/TopiValue.cs) implementation details, 
but without boxing everything to just `object` it didn't seem viable.

So instead here's your warning: TopiValues have data that are explicitly mapped out in memory 
and different fields are overlaying each other.

```csharp
    [StructLayout(LayoutKind.Sequential)]
    public struct TopiValue : IEquatable<TopiValue>, IDisposable
    {
        public Tag tag;
        private TopiValueData _data;
        ...
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct TopiValueData
    {
        [FieldOffset(0)] public byte boolValue;
        [FieldOffset(0)] public float numberValue;
        [FieldOffset(0)] public StringBuffer stringValue;
        [FieldOffset(0)] public TopiList listValue;
        [FieldOffset(0)] public TopiEnum enumValue;
    }
```

This means you are required to check the `tag` field before accessing the data to know which field is currently active or use a switch statement.
If you prefer to accept an `object` you can use the `Value` property. Or if you're sure of the type (like in the Topi functions above)
you can use the named properties directly, just be careful. `Bool`, `Int`, `Float`, `String`, `List`, `Set`, `Map`, `Enum`.

```csharp
var value = new TopiValue(true);
value.Float // Incorrect property access, throws an error or returns malformed/incorrect data
value.String // Incorrect property access, throws an error or returns malformed/incorrect data
value.Bool // true
```

`String`, `List`, `Set`, `Map`, and `Value` re-walk native memory on every access — capture into a local for hot loops:

```csharp
var list = value.List; // walk once
foreach (var item in list) { ... }
```

### Memory ownership

`TopiValue` implements `IDisposable`. String / Enum / List / Set / Map values own unmanaged buffers; when you return one from a `[Topi]` extern, the native side releases it for you. If you construct one and **don't** hand it to native (rare — usually for tests or advanced cases), call `Dispose()` to free.

For containers, ownership transfers downward — wrapping `TopiValue`s in a list takes ownership of their inner allocations. Don't dispose the originals separately, and don't double-dispose.

## State persistence

`Dialogue.State` is a static singleton that aggregates JSON state across `Play()` runs via `State.Amend`. To restore, call `State.Set(json)` or `State.Amend(json)` before `Play()` and the VM will be primed with that data. Both methods early-return on null/empty input and log on JSON parse errors.
