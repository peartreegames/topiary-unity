using System;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    [CreateAssetMenu(fileName = "so_", menuName = "TopiEnum", order = 10)]
    public class EnumObject : ScriptableObject
    {
        [SerializeField] private new string name;
        [SerializeField] private bool isSequence;
        [SerializeField] private string[] values;
        
        public string Name => name;
        public bool IsSequence => isSequence;
        public string[] Values => values;

        private void OnEnable()
        {
            Debug.Assert(values.Length > 0, "values.Length > 0", this); 
        }
    }

    [Serializable]
    public class EnumReference
    {
        [SerializeField] private EnumObject enumObject;
        [SerializeField] private string enumValue;

        public bool IsSequence => enumObject?.IsSequence ?? false;
        public string Name => enumObject?.Name ?? "";
        public string[] Values => enumObject?.Values ?? Array.Empty<string>();
        public string Value
        {
            get => enumValue;
            set => enumValue = value;
        }
    }
}