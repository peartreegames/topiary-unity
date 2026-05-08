using UnityEngine;
using UnityEngine.Events;

namespace PeartreeGames.Topiary.Unity
{
    public class Speaker : MonoBehaviour
    {
        [field: SerializeField, Tooltip("The name your wrote in the topi file")] public string Id { get; private set; }
        [SerializeField] private UnityEvent<Speaker> onStartSpeaking;
        [SerializeField] private UnityEvent<Speaker> onStopSpeaking;
        public UnityEvent<Speaker> OnStartSpeaking => onStartSpeaking;
        public UnityEvent<Speaker> OnStopSpeaking => onStopSpeaking;
        
        private void Awake()
        {
            Dialogue.AddSpeaker(this);
        }

        private void OnDestroy()
        {
            Dialogue.RemoveSpeaker(this);
        }

        public void StartSpeaking() => onStartSpeaking.Invoke(this);
        public void StopSpeaking() => onStopSpeaking.Invoke(this);

        private void OnValidate()
        {
            Debug.Assert(!string.IsNullOrEmpty(Id),
                $"Speaker '{name}' must have an Id matching the speaker name in the .topi file");
        }
    }
}