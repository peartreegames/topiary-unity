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

        public string[] Tags => tags;
        private TopiSpeaker _previousSpeaker;
        private GCHandle _pinnedHandle;
        private IntPtr _vmPtr;
        public static event Action<Dialogue> OnCreated;
        public static event Action<Dialogue> OnStart;
        public static event Action<Dialogue> OnEnd;
        public static event Action<Line, TopiSpeaker> OnLine;
        public static event Action<Choice[]> OnChoices;

        public static readonly Dictionary<string, TopiSpeaker> Speakers = new();
        public static readonly Dictionary<IntPtr, Dialogue> Conversations = new();
        private static readonly Dictionary<string, EvtVariable> Variables = new();
        [ShowInInspector]
        public static readonly State State = new();

        private List<AsyncOperationHandle<EvtVariable>> _aoHandles;
        public static void AddSpeaker(TopiSpeaker speaker) => Speakers[speaker.name] = speaker;
        public static void RemoveSpeaker(TopiSpeaker speaker) => Speakers.Remove(speaker.name);

        private static Library.OnChoicesDelegate _onChoicesCallback;
        private static Library.OnLineDelegate _onLineCallback;
        private static Library.SubscriberDelegate _subscriberCallback;
        private static Library.OutputLogDelegate _onLogCallback;
        

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            Speakers.Clear();
            Conversations.Clear();
            Variables.Clear();
            _onChoicesCallback ??= OnChoicesCallback;
            _onLineCallback ??= OnLineCallback;
            _onLogCallback ??= LogCallback;
            var logPtr = Marshal.GetFunctionPointerForDelegate(_onLogCallback);
            Library.setDebugLog(logPtr);
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
            var linePtr = Marshal.GetFunctionPointerForDelegate(_onLineCallback);
            var choicesPtr = Marshal.GetFunctionPointerForDelegate(_onChoicesCallback);
            _vmPtr = Library.createVm(sourcePtr, _data.bytes.Length, linePtr, choicesPtr);

            OnCreated?.Invoke(this);
            Conversations.Add(_vmPtr, this);
        }

        [MonoPInvokeCallback(typeof(Library.OutputLogDelegate))]
        public static void LogCallback(IntPtr intPtr, Library.Severity severity) =>
            Log(Library.PtrToUtf8String(intPtr), severity);

        public static void Log(string msg, Library.Severity severity)
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

        private void OnDestroy()
        {
            UnloadAddressableTopiValues();
            if (_pinnedHandle.IsAllocated) _pinnedHandle.Free();
            if (_vmPtr != IntPtr.Zero) Library.destroyVm(_vmPtr);
            _vmPtr = IntPtr.Zero;
            if (file.IsValid()) file.ReleaseAsset();
        }

        [MonoPInvokeCallback(typeof(Library.OnLineDelegate))]
        private static void OnLineCallback(IntPtr vmPtr, Line line)
        {
            var convo = Conversations[vmPtr];
            if (convo._previousSpeaker != null) convo._previousSpeaker.StopSpeaking();
            if (Speakers.TryGetValue(line.Speaker, out var speaker)) speaker.StartSpeaking();
            convo._previousSpeaker = speaker;
            OnLine?.Invoke(line, speaker);
        }

        [MonoPInvokeCallback(typeof(Library.OnChoicesDelegate))]
        private static void OnChoicesCallback(IntPtr vmPtr, IntPtr choicesPtr, byte count) =>
            OnChoices?.Invoke(Choice.MarshalPtr(choicesPtr, count));

        public void PlayDialogue()
        {
            StartCoroutine(Play());
        }

        public void Continue() => Library.selectContinue(_vmPtr);
        public void SelectChoice(int index) => Library.selectChoice(_vmPtr, index);

        public IEnumerator Play()
        {
            if (_vmPtr.Equals(IntPtr.Zero))
            {
                Log("[Topiary.Unity] Invalid Dialogue", Library.Severity.Warn);
                yield break;
            }

            _subscriberCallback = ValueChanged;
            Library.setSubscriberCallback(_vmPtr,
                Marshal.GetFunctionPointerForDelegate(_subscriberCallback));
            Library.setDebugSeverity(logs);
            yield return StartCoroutine(LoadAddressableTopiValues());
            State.Inject(this);
            OnStart?.Invoke(this);
            Library.start(_vmPtr, bough, bough.Length);
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

            State.Amend(this);
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(this);
            UnloadAddressableTopiValues();
        }

        public string SaveState()
        {
            var capacity = Library.calculateStateSize(_vmPtr);
            var output = new byte[capacity];
            _ = Library.saveState(_vmPtr, output, output.Length);
            return System.Text.Encoding.UTF8.GetString(output);
        }

        public void LoadState(string json) => Library.loadState(_vmPtr, json, json.Length);
        
        [MonoPInvokeCallback(typeof(Library.SubscriberDelegate))]
        public static void ValueChanged(string name, ref TopiValue value)
        {
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
        }

        private IEnumerator LoadAddressableTopiValues()
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
                        Set(topiName, b.Value);
                        break;
                    case EvtTopiFloat f:
                        topiName = f.Name;
                        Set(topiName, f.Value);
                        break; 
                    case EvtTopiInt i:
                        topiName = i.Name;
                        Set(topiName, i.Value);
                        break;
                    case EvtTopiString s:
                        topiName = s.Name;
                        Set(topiName, s.Value);
                        break;
                    case EvtTopiEnum e:
                        topiName = e.Name;
                        Set(topiName, e.Enum.Name, e.Value);
                        break;
                }

                if (topiName == null)
                {
                    Addressables.Release(aoEvt);
                    continue;
                }

                Variables[topiName] = aoEvt.Result;
                Subscribe(topiName);
                _aoHandles.Add(aoEvt);
            }
        }

        private void UnloadAddressableTopiValues()
        {
            foreach (var kvp in Variables) Unsubscribe(kvp.Key);
            foreach (var handle in _aoHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }

        public bool Subscribe(string variableName) =>
            Library.subscribe(_vmPtr, variableName, variableName.Length);

        /// <summary>
        /// Unsubscribe when a Global variable changes
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        public bool Unsubscribe(string variableName) =>
            Library.unsubscribe(_vmPtr, variableName, variableName.Length);

        /// <summary>
        /// Set an Extern variable to a bool value
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="value">The value to set</param>
        public void Set(string variableName, bool value) =>
            Library.setExternBool(_vmPtr, variableName, variableName.Length, value);

        /// <summary>
        /// Set an Extern variable to a float value
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="value">The value to set</param>
        public void Set(string variableName, float value) =>
            Library.setExternNumber(_vmPtr, variableName, variableName.Length, value);

        /// <summary>
        /// Set an Extern variable to an enum value
        /// </summary>
        /// <param name="variableName"></param>
        /// <param name="enumName"></param>
        /// <param name="enumValue"></param>
        public void Set(string variableName, string enumName, string enumValue) =>
            Library.setExternEnum(_vmPtr, variableName, variableName.Length, enumName, enumName.Length, enumValue,
                enumValue.Length);

        /// <summary>
        /// Set an Extern variable to a float value
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        /// <param name="value">The value to set</param>
        public void Set(string variableName, string value) =>
            Library.setExternString(_vmPtr, variableName, variableName.Length, value, value.Length);

        /// <summary>
        /// Set a Global Extern variable to a function value
        /// Note: It is easier to use the TopiAttribute instead with the BindFunctions method
        /// However this is kept in case you need more control 
        /// </summary>
        /// <param name="function">The function to set</param>
        public void Set(Library.ExternFunctionDelegate function)
        {
            var methodInfo = function.Method;
            var topiAttributes = methodInfo.GetCustomAttributes(typeof(TopiAttribute), false);
            if (topiAttributes.Length == 0)
                throw new InvalidOperationException(
                    $"Missing TopiAttribute on function {methodInfo.Name}.");
            if (topiAttributes.Length > 1)
                throw new InvalidOperationException(
                    $"Only one instance of TopiAttribute is allowed on function {methodInfo.Name}");

            foreach (TopiAttribute topiAttribute in topiAttributes)
            {
                var topiName = topiAttribute.Name;
                var arity = topiAttribute.Arity;
                Library.setExternFunc(_vmPtr, topiName, topiName.Length,
                    Marshal.GetFunctionPointerForDelegate(function), arity);
            }
        }

        /// <summary>
        /// Set an Extern variable to a nil value
        /// </summary>
        /// <param name="variableName">The name of the variable</param>
        public void Unset(string variableName) => Library.setExternNil(_vmPtr, variableName, variableName.Length);
    }
}