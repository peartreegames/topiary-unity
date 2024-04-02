﻿using System;
using System.Collections;
using System.Collections.Generic;
using PeartreeGames.Evt.Variables;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PeartreeGames.Topiary.Unity
{
    public class Conversation : MonoBehaviour
    {
        public static event Action<Story, Conversation> OnStart;
        public static event Action<Story, Conversation> OnEnd;
        public static event Action<Story, Dialogue, TopiSpeaker> OnDialogue;
        public static event Action<Story, Choice[]> OnChoices;

        [SerializeField] private string bough;
        [SerializeField] private string[] tags;
        [SerializeField] private AssetReferenceT<ByteData> file;
        private ByteData _data;

        [SerializeField] private Library.Severity logs = Library.Severity.Error;
        public Story Story { get; private set; }
        public string[] Tags => tags;

        private TopiSpeaker _previousSpeaker;
        private List<EvtTopiReference> _evtReferences;

        public static Dictionary<string, TopiSpeaker> Speakers { get; } = new();
        public static void AddSpeaker(TopiSpeaker speaker) => Speakers[speaker.name] = speaker;
        public static void RemoveSpeaker(TopiSpeaker speaker) => Speakers.Remove(speaker.name);

        private static byte[] _tempState;

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
            Library.OnDebugLogMessage += Log;
            Story = new Story(_data.bytes, OnDialogueCallback, OnChoicesCallback, logs);
            if (!Story.IsValid) Log("[Topiary.Unity] Could not create Story ", Library.Severity.Error);
            else Story.BindFunctions(AppDomain.CurrentDomain.GetAssemblies());
            Library.OnDebugLogMessage -= Log;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            Library.IsUnityRuntime = true;
        }

        public void Log(string msg, Library.Severity severity)
        {
            switch (severity)
            {
                case Library.Severity.Debug:
                case Library.Severity.Info:
                    Debug.Log($"{name}: {msg}", gameObject);
                    break;
                case Library.Severity.Warn:
                    Debug.LogWarning($"{name}: {msg}", gameObject);
                    break;
                case Library.Severity.Error:
                    Debug.LogError($"{name}: {msg}", gameObject);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }

        private void OnDestroy()
        {
            Library.OnDebugLogMessage -= Log;
            UnloadAddressableTopiValues();
            Story?.Dispose();
            if (file.IsValid()) file.ReleaseAsset();
        }

        private void OnDialogueCallback(Story story, Dialogue dialogue)
        {
            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            if (Speakers.TryGetValue(dialogue.Speaker, out var speaker)) speaker.StartSpeaking();
            _previousSpeaker = speaker;
            OnDialogue?.Invoke(story, dialogue, speaker);
        }

        private void OnChoicesCallback(Story story, Choice[] choices) =>
            OnChoices?.Invoke(story, choices);

        public void PlayStory()
        {
            StartCoroutine(Play());
        }

        public IEnumerator Play()
        {
            if (!Story.IsValid)
            {
                Log("[Topiary.Unity] Invalid Story", Library.Severity.Warn);
                yield break;
            }
            Story.Library.SetDebugSeverity(logs);
            Library.OnDebugLogMessage += Log;
            yield return StartCoroutine(LoadAddressableTopiValues());
            OnStart?.Invoke(Story, this);
            Story.Start(bough);
            while (Story?.CanContinue ?? false)
            {
                try
                {
                    Story?.Run();
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

                while (Story?.IsWaiting ?? false) yield return null;
            }

            if (_previousSpeaker != null) _previousSpeaker.StopSpeaking();
            _previousSpeaker = null;
            OnEnd?.Invoke(Story, this);
            UnloadAddressableTopiValues();
            Library.OnDebugLogMessage -= Log;
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