using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using PeartreeGames.Evt.Variables;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PeartreeGames.Topiary.Unity
{
    public class Conversation : MonoBehaviour
    {
        [SerializeField] private string bough;
        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        [SerializeField] private Library.Severity logs = Library.Severity.Error;
        
        private ByteData _data;

        public Dialogue Dialogue { get; private set; }
        public string[] Tags => tags;

        private TopiSpeaker _previousSpeaker;
        public static event Action<Dialogue, Conversation> OnCreated;
        public static event Action<Dialogue, Conversation> OnStart;
        public static event Action<Dialogue, Conversation> OnEnd;
        public static event Action<Dialogue, Line, TopiSpeaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;
        public static readonly Dictionary<string, TopiSpeaker> Speakers  = new();
        public static readonly Dictionary<IntPtr, Conversation> Conversations = new();
        private static readonly Dictionary<string, EvtVariable> Variables = new();
        private static Delegates.Subscriber _subscriber;
        private List<AsyncOperationHandle<EvtVariable>> _aoHandles;
        public static void AddSpeaker(TopiSpeaker speaker) => Speakers[speaker.name] = speaker;
        public static void RemoveSpeaker(TopiSpeaker speaker) => Speakers.Remove(speaker.name);

        public static readonly State State = new();

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
            if (loc.Status != AsyncOperationStatus.Succeeded || loc.Result == null || loc.Result.Count == 0)
            {
                Log($"[Topiary.Unity] No file set: {file}", Library.Severity.Error);
                yield break;
            }
            

            var ao = Addressables.LoadAssetAsync<ByteData>(file);
            yield return ao;
            _data = ao.Result;
            Dialogue = new Dialogue(_data.bytes, OnLineCallback, OnChoicesCallback, LogCallback, logs);
            if (!Dialogue.IsValid)
            {
                Log("[Topiary.Unity] Could not create Dialogue ", Library.Severity.Error);
                yield break;
            }

            OnCreated?.Invoke(Dialogue, this);
            Conversations.Add(Dialogue.VmPtr, this);
        }

        [MonoPInvokeCallback(typeof(Delegates.OutputLogDelegate))]
        public static void LogCallback(IntPtr intPtr, Library.Severity severity) => Log(Library.PtrToUtf8String(intPtr), severity);

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
            if (Dialogue != null)
            {
                Conversations?.Remove(Dialogue.VmPtr);
                Dialogue?.Dispose();
            }
            if (file.IsValid()) file.ReleaseAsset();
        }

        [MonoPInvokeCallback(typeof(Delegates.OnLineDelegate))]
        private static void OnLineCallback(IntPtr vmPtr, Line line)
        {
            var convo = Conversations[vmPtr];
            if (convo._previousSpeaker != null) convo._previousSpeaker.StopSpeaking();
            if (Speakers.TryGetValue(line.Speaker, out var speaker)) speaker.StartSpeaking();
            convo._previousSpeaker = speaker;
            OnLine?.Invoke(Dialogue.Dialogues[vmPtr], line, speaker);
        }

        [MonoPInvokeCallback(typeof(Delegates.OnChoicesDelegate))]
        private static void OnChoicesCallback(IntPtr vmPtr, IntPtr choicesPtr, byte count) =>
            OnChoices?.Invoke(Dialogue.Dialogues[vmPtr], Choice.MarshalPtr(choicesPtr, count));

        public void PlayDialogue()
        {
            StartCoroutine(Play());
        }

        public IEnumerator Play()
        {
            if (!Dialogue.IsValid)
            {
                Log("[Topiary.Unity] Invalid Dialogue", Library.Severity.Warn);
                yield break;
            }

            _subscriber = ValueChanged;
            Dialogue.Library.SetSubscriberCallback(Dialogue.VmPtr, Marshal.GetFunctionPointerForDelegate(_subscriber));
            Dialogue.Library.SetDebugSeverity(logs);
            yield return StartCoroutine(LoadAddressableTopiValues());
            State.Inject(Dialogue);
            OnStart?.Invoke(Dialogue, this);
            Dialogue.Start(bough);
            while (Dialogue?.CanContinue ?? false)
            {
                try
                {
                    Dialogue?.Run();
                }
                catch (SEHException ex)
                {
                    Log($"Caught an SEHException: {ex}", Library.Severity.Error);
                    break;
                }
                catch (Exception e)
                {
                    Log(e.Message, Library.Severity.Error);
                    break;
                }

                while (Dialogue?.IsWaiting ?? false) yield return null;
            }

            State.Amend(Dialogue);
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(Dialogue, this);
            UnloadAddressableTopiValues();
        }

        [MonoPInvokeCallback(typeof(Delegates.Subscriber))]
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
                }
            }
        }
        
        private IEnumerator LoadAddressableTopiValues()
        {
            while (Dialogue?.Library == null) yield return null;
            var ao = Addressables.LoadResourceLocationsAsync(new List<string> {"Topiary", "Evt"},
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
                        Dialogue.Set(topiName, b.Value);
                        break;
                    case EvtTopiFloat f: 
                        topiName = f.Name;
                        Dialogue.Set(topiName, f.Value);
                        break; 
                    case EvtTopiInt i:
                        topiName = i.Name;
                        Dialogue.Set(topiName, i.Value);
                        break;
                    case EvtTopiString s: 
                        topiName = s.Name;
                        Dialogue.Set(topiName, s.Value);
                        break;
                }
                
                if (topiName == null)
                {
                    Addressables.Release(aoEvt);
                    continue;
                }
                
                Variables[topiName] = aoEvt.Result;
                Dialogue.Subscribe(topiName);
                _aoHandles.Add(aoEvt);
            }
        }

        private void UnloadAddressableTopiValues()
        {
            foreach (var kvp in Variables) Dialogue.Unsubscribe(kvp.Key);
            foreach (var handle in _aoHandles)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }
        }
    }
}