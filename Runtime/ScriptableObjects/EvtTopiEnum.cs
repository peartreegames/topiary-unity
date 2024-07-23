using System;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    [CreateAssetMenu(fileName = "enum_", menuName = "Evt/Topiary/Enum", order = 0)]
    public class EvtTopiEnum : EvtTopiVariable<string>
    {
        [SerializeField] private EnumReference @enum;

        public EnumReference Enum => @enum;
        
        public override string Value
        {
            get => EvtT.Value;
            set
            {
                @enum.Value = value;
                if (EvtT == null) return;
                var current = Array.FindIndex(@enum.Values, v => v == EvtT.Value);
                var next = Array.FindIndex(@enum.Values, v => v == value);
                if (@enum.IsSequence && current > next) return;
                if (IsEqual(EvtT.Value, value)) return;
                EvtT.Value = value;
            }
        }

        public void Set(EnumReference reference)
        {
            @enum = reference;
            Value = reference.Value;
        }

        private void OnEnable()
        {
            Debug.Assert(@enum != null, "topiEnum != null", this); 
        }
    }
}