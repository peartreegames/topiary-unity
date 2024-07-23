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
        
        protected void Awake()
        {
            Dialogue.AddSpeaker(this);
        }

        protected void OnDestroy()
        {
            Dialogue.RemoveSpeaker(this);
        }

        public void StartSpeaking() => onStartSpeaking.Invoke(this);
        public void StopSpeaking() => onStopSpeaking.Invoke(this);

        private void OnValidate()
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "TopiSpeaker must have a name"); 
        }
    }
}