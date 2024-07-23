using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using PeartreeGames.Evt.Variables;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PeartreeGames.Topiary.Unity
{
    public class Dialogue : MonoBehaviour
    {
        [SerializeField] private string bough;
        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        [SerializeField] private Library.Severity logs = Library.Severity.Error;

        private ByteData _data;
        private Speaker _previousSpeaker;
        private GCHandle _pinnedHandle;
        private IntPtr _vmPtr;
        private List<AsyncOperationHandle<EvtVariable>> _aoHandles;

        public string[] Tags => tags;

        public static event Action<Dialogue> OnStart;
        public static event Action<Dialogue> OnEnd;
        public static event Action<Dialogue, Line, Speaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;
        public static event Action<Dialogue, string, TopiValue> OnValueChanged;

        [ShowInInspector]
        public static readonly State State = new();
        public static readonly Dictionary<string, Speaker> Speakers = new();
        public static readonly Dictionary<IntPtr, Dialogue> Dialogues = new();
        private static readonly Dictionary<string, EvtVariable> Variables = new();
        private static readonly Dictionary<string, Delegate> Callbacks = new();
        private static readonly List<TopiAttribute.FuncPtr> FunctionPtrs = new();

        private static Delegates.OnChoicesDelegate _onChoicesCallback;
        private static Delegates.OnLineDelegate _onLineCallback;
        private static Delegates.SubscriberDelegate _subscriberCallback;
        private static Delegates.OutputLogDelegate _onLogCallback;
        private static Delegates.FreeDelegate _freeCallback;
        private static IntPtr _choicesPtr;
        private static IntPtr _linePtr;
        private static IntPtr _subscriberPtr;
        private static IntPtr _logPtr;
        private static IntPtr _freePtr;

        private bool IsVmValid
        {
            get
            {
                if (!_vmPtr.Equals(IntPtr.Zero)) return true;
                Log("[Topiary.Unity] Invalid Vm", Library.Severity.Error);
                return false;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Speakers.Clear();
            Dialogues.Clear();
            Variables.Clear();
            Callbacks.Clear();
            FunctionPtrs.Clear();
            _onChoicesCallback ??= OnChoicesCallback;
            _onLineCallback ??= OnLineCallback;
            _onLogCallback ??= LogCallback;
            _subscriberCallback ??= ValueChangedCallback;
            _freeCallback ??= Free;
            _subscriberPtr = Marshal.GetFunctionPointerForDelegate(_subscriberCallback);
            _linePtr = Marshal.GetFunctionPointerForDelegate(_onLineCallback);
            _choicesPtr = Marshal.GetFunctionPointerForDelegate(_onChoicesCallback);
            _logPtr = Marshal.GetFunctionPointerForDelegate(_onLogCallback);
            _freePtr = Marshal.GetFunctionPointerForDelegate(_freeCallback);
            FunctionPtrs.AddRange(TopiAttribute.GetAllTopiMethodPtrs());
        }

        private void Awake()
        {
            _aoHandles = new List<AsyncOperationHandle<EvtVariable>>();
        }

        private IEnumerator Start()
        {
            if (file == null)
            {
                Log($"[Topiary.Unity] No file set: {file}", Library.Severity.Error);
                yield break;
            }

            var loc = Addressables.LoadResourceLocationsAsync(file);
            yield return loc;
            if (loc.Status != AsyncOperationStatus.Succeeded || loc.Result == null ||
                loc.Result.Count == 0)
            {
                Log($"[Topiary.Unity] No file set: {file}", Library.Severity.Error);
                yield break;
            }


            var ao = file.LoadAssetAsync<ByteData>();
            yield return ao;
            _data = ao.Result;
            if (_data == null)
            {
                Log($"[Topiary.Unity] ByteData could not be loaded: {file}",
                    Library.Severity.Error);
                yield break;
            }


            _pinnedHandle = GCHandle.Alloc(_data.bytes, GCHandleType.Pinned);
            var sourcePtr = Marshal.UnsafeAddrOfPinnedArrayElement(_data.bytes, 0);
            _vmPtr = Library.createVm(sourcePtr, _data.bytes.Length, _linePtr, _choicesPtr,
                _subscriberPtr, _logPtr, logs);
            Dialogues.Add(_vmPtr, this);
        }

        private void OnDestroy()
        {
            UnloadTopiValues();
            if (_pinnedHandle.IsAllocated) _pinnedHandle.Free();
            if (_vmPtr != IntPtr.Zero) Library.destroyVm(_vmPtr);
            _vmPtr = IntPtr.Zero;
            if (file.IsValid()) file.ReleaseAsset();
        }

        public static void AddSpeaker(Speaker speaker) => Speakers[speaker.Id] = speaker;
        public static void RemoveSpeaker(Speaker speaker) => Speakers.Remove(speaker.Id);

        public void Continue()
        {
            if (IsVmValid) Library.selectContinue(_vmPtr);
        }

        public void SelectChoice(int index)
        {
            if (_vmPtr.Equals(IntPtr.Zero))
            {
                Log("[Topiary.Unity] Invalid Vm", Library.Severity.Error);
                return;
            }

            Library.selectChoice(_vmPtr, index);
        }

        public void PlayDialogue() => StartCoroutine(Play());

        public IEnumerator Play()
        {
            if (!IsVmValid) yield break;
            LoadState(State.Value);
            LoadFunctions();
            yield return StartCoroutine(LoadTopiValues());

            OnStart?.Invoke(this);
            Library.start(_vmPtr, bough);
            while (Library.canContinue(_vmPtr))
            {
                try
                {
                    Library.run(_vmPtr);
                }
                catch (SEHException ex)
                {
                    Log($"Caught an SEHException: {ex}", Library.Severity.Error);
                    break;
                }

                while (Library.isWaiting(_vmPtr)) yield return null;
            }

            State.Amend(SaveState());
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(this);
            UnloadTopiValues();
        }

        public string SaveState()
        {
            if (!IsVmValid) return null;
            var capacity = Library.calculateStateSize(_vmPtr);
            var output = new byte[capacity];
            _ = Library.saveState(_vmPtr, output, output.Length);
            return System.Text.Encoding.UTF8.GetString(output);
        }

        public void LoadState(string json)
        {
            if (json != null && IsVmValid) Library.loadState(_vmPtr, json, json.Length);
        }

        public bool Subscribe(string variableName) =>
            IsVmValid && Library.subscribe(_vmPtr, variableName);

        /// <summary>
        /// Unsubscribe when a Global variable changes
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        public bool Unsubscribe(string variableName) =>
            IsVmValid && Library.unsubscribe(_vmPtr, variableName);

        /// <summary>
        /// Set an Extern variable to a TopiValue
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="value">The value to set</param>
        public void Set(string variableName, TopiValue value) =>
            Library.setExtern(_vmPtr, variableName, value, _freePtr);

        [MonoPInvokeCallback(typeof(Delegates.OnLineDelegate))]
        private static void OnLineCallback(IntPtr vmPtr, Line line)
        {
            if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
            {
                Log($"[Topiary.Unity] Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                    Library.Severity.Error);
                return;
            }

            if (dialogue._previousSpeaker != null) dialogue._previousSpeaker.StopSpeaking();
            if (Speakers.TryGetValue(line.Speaker, out var speaker)) speaker.StartSpeaking();
            dialogue._previousSpeaker = speaker;
            OnLine?.Invoke(dialogue, line, speaker);
        }

        [MonoPInvokeCallback(typeof(Delegates.OnChoicesDelegate))]
        private static void OnChoicesCallback(IntPtr vmPtr, IntPtr choicesPtr, byte count)
        {
            if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
            {
                Log($"[Topiary.Unity] Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                    Library.Severity.Error);
                return;
            }

            OnChoices?.Invoke(dialogue, Choice.MarshalPtr(choicesPtr, count));
        }

        [MonoPInvokeCallback(typeof(Delegates.SubscriberDelegate))]
        private static void ValueChangedCallback(IntPtr vmPtr, string name, TopiValue value)
        {
            if (!Dialogues.TryGetValue(vmPtr, out var dialogue))
            {
                Log($"[Topiary.Unity] Dialogue not found for vmPtr {vmPtr.ToInt64()}",
                    Library.Severity.Error);
                return;
            }

            if (Variables.TryGetValue(name, out var variable))
            {
                switch (value.tag)
                {
                    case TopiValue.Tag.Bool when variable is EvtTopiBool b:
                        b.Value = value.Bool;
                        break;
                    case TopiValue.Tag.Number when variable is EvtTopiInt i:
                        i.Value = value.Int;
                        break;
                    case TopiValue.Tag.Number when variable is EvtTopiFloat f:
                        f.Value = value.Float;
                        break;
                    case TopiValue.Tag.String when variable is EvtTopiString s:
                        s.Value = value.String;
                        break;
                    case TopiValue.Tag.Enum when variable is EvtTopiEnum e:
                        Debug.Assert(e.Enum.Name == value.Enum.Name,
                            $"{name} is not instance of {e.Enum.Name}");
                        var enumValue = value.Enum.Value;
                        Debug.Assert(Array.Exists(e.Enum.Values, v => v == enumValue),
                            $"{e.Enum.Name} does not contain {enumValue}");
                        e.Value = value.Enum.Value;
                        break;
                }
            }

            OnValueChanged?.Invoke(dialogue, name, value);
        }

        [MonoPInvokeCallback(typeof(Delegates.OutputLogDelegate))]
        private static void LogCallback(StringBuffer str, Library.Severity severity) =>
            Log(str.Value, severity);

        [MonoPInvokeCallback(typeof(Delegates.FreeDelegate))]
        internal static void Free(IntPtr ptr) => Marshal.FreeHGlobal(ptr);

        private static void Log(string msg, Library.Severity severity)
        {
            switch (severity)
            {
                case Library.Severity.Debug:
                case Library.Severity.Info:
                    Debug.Log(msg);
                    break;
                case Library.Severity.Warn:
                    Debug.LogWarning(msg);
                    break;
                case Library.Severity.Error:
                    Debug.LogError(msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }

        private void LoadFunctions()
        {
            foreach (var func in FunctionPtrs)
            {
                if (_data.ExternsSet.Contains(func.Name))
                    Library.setExternFunc(_vmPtr, func.Name, func.Ptr, func.Arity, _freePtr);
            }
        }

        private IEnumerator LoadTopiValues()
        {
            var ao = Addressables.LoadResourceLocationsAsync(new List<string> { "Topiary", "Evt" },
                Addressables.MergeMode.Intersection);
            yield return ao;
            var list = ao.Result;
            foreach (var item in list)
            {
                var key = item.PrimaryKey;
                if (!_data.ExternsSet.Contains(key)) continue;
                var aoEvt = Addressables.LoadAssetAsync<EvtVariable>(key);
                yield return aoEvt;

                string topiName = null;
                switch (aoEvt.Result)
                {
                    case EvtTopiBool b:
                        topiName = b.Name;
                        Set(topiName, new TopiValue(b));
                        Callbacks[topiName] =
                            new Action<bool>(v => Set(topiName, new TopiValue(v)));
                        b.OnEvt += (Action<bool>)Callbacks[topiName];
                        break;
                    case EvtTopiFloat f:
                        topiName = f.Name;
                        Set(topiName, new TopiValue(f));
                        Callbacks[topiName] =
                            new Action<float>(v => Set(topiName, new TopiValue(v)));
                        f.OnEvt += (Action<float>)Callbacks[topiName];
                        break;
                    case EvtTopiInt i:
                        topiName = i.Name;
                        Set(topiName, new TopiValue(i));
                        Callbacks[topiName] = new Action<int>(v => Set(topiName, new TopiValue(v)));
                        i.OnEvt += (Action<int>)Callbacks[topiName];
                        break;
                    case EvtTopiString s:
                        topiName = s.Name;
                        Set(topiName, new TopiValue(s));
                        Callbacks[topiName] =
                            new Action<string>(v => Set(topiName, new TopiValue(v)));
                        s.OnEvt += (Action<string>)Callbacks[topiName];
                        break;
                    case EvtTopiEnum e:
                        topiName = e.Name;
                        Debug.Log($"Setting {topiName} to {e.Name}.{e.Value}");
                        Set(topiName, new TopiValue(e.Name, e.Value));
                        Callbacks[topiName] =
                            new Action<string>(v => Set(topiName, new TopiValue(e.Name, v)));
                        e.OnEvt += (Action<string>)Callbacks[topiName];
                        break;
                }

                if (topiName == null)
                {
                    Addressables.Release(aoEvt);
                    continue;
                }

                if (!Subscribe(topiName))
                {
                    Log($"[Topiary.Unity] Could not Subscribe to {topiName}",
                        Library.Severity.Warn);
                    UnsubscribeEvt(topiName, aoEvt.Result);
                    Addressables.Release(aoEvt);
                    continue;
                }

                Variables[topiName] = aoEvt.Result;
                _aoHandles.Add(aoEvt);
            }
        }

        private void UnloadTopiValues()
        {
            foreach (var kvp in Variables)
            {
                Unsubscribe(kvp.Key);
                UnsubscribeEvt(kvp.Key, kvp.Value);
            }

            foreach (var handle in _aoHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        private static void UnsubscribeEvt(string topiName, EvtVariable evt)
        {
            if (!Callbacks.TryGetValue(topiName, out var del)) return;
            switch (evt)
            {
                case EvtTopiBool b:
                    b.OnEvt -= (Action<bool>)del;
                    break;
                case EvtTopiFloat f:
                    f.OnEvt -= (Action<float>)del;
                    break;
                case EvtTopiInt i:
                    i.OnEvt -= (Action<int>)del;
                    break;
                case EvtTopiString s:
                    s.OnEvt -= (Action<string>)del;
                    break;
                case EvtTopiEnum e:
                    e.OnEvt -= (Action<string>)del;
                    break;
            }
        }
    }
}