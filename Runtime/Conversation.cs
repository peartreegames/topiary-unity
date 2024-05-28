using System;
using System.Collections;
using System.Collections.Generic;
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
        private List<EvtTopiReference> _evtReferences;
        public static event Action<Dialogue, Conversation> OnCreated;
        public static event Action<Dialogue, Conversation> OnStart;
        public static event Action<Dialogue, Conversation> OnEnd;
        public static event Action<Dialogue, Line, TopiSpeaker> OnLine;
        public static event Action<Dialogue, Choice[]> OnChoices;
        public static readonly Dictionary<string, TopiSpeaker> Speakers  = new();
        public static readonly Dictionary<IntPtr, Conversation> Conversations = new();
        public static void AddSpeaker(TopiSpeaker speaker) => Speakers[speaker.name] = speaker;
        public static void RemoveSpeaker(TopiSpeaker speaker) => Speakers.Remove(speaker.name);

        public static readonly State State = new();
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
                catch (System.Runtime.InteropServices.SEHException ex)
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


        private IEnumerator LoadAddressableTopiValues()
        {
            var ao = Addressables.LoadResourceLocationsAsync(new List<string> {"Topiary", "Evt"},
                Addressables.MergeMode.Intersection);
            yield return ao;
            var list = ao.Result;
            _evtReferences = new List<EvtTopiReference>();
            foreach (var item in list)
            {
                var key = item.PrimaryKey;
                if (!_data.ExternsSet.Contains(key)) continue;
                var aoEvt = Addressables.LoadAssetAsync<EvtVariable>(key);
                yield return aoEvt;

                EvtTopiReference evtRef = aoEvt.Result switch
                {
                    EvtTopiBool b => new EvtBoolReference(this, b),
                    EvtTopiFloat f => new EvtFloatReference(this, f),
                    EvtTopiInt i => new EvtIntReference(this, i),
                    EvtTopiString s => new EvtStringReference(this, s),
                    _ => null
                };
                if (evtRef == null) continue;
                _evtReferences.Add(evtRef);
            }
        }

        private void UnloadAddressableTopiValues()
        {
            if (_evtReferences == null) return;
            foreach (var reference in _evtReferences) reference?.Dispose();
            _evtReferences.Clear();
        }
    }
}