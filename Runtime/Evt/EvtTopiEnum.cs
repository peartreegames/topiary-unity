using System;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    [CreateAssetMenu(fileName = "enum_", menuName = "Evt/Topiary/Enum", order = 0)]
    public class EvtTopiEnum : EvtTopiVariable<string>
    {
        [SerializeField] private TopiEnumReference topiEnum;

        public TopiEnumReference Enum => topiEnum;
        
        public override string Value
        {
            get => EvtT.Value;
            set
            {
                topiEnum.Value = value;
                if (EvtT == null) return;
                var current = Array.FindIndex(topiEnum.Values, v => v == EvtT.Value);
                var next = Array.FindIndex(topiEnum.Values, v => v == value);
                if (topiEnum.IsSequence && current > next) return;
                if (IsEqual(EvtT.Value, value)) return;
                EvtT.Value = value;
            }
        }

        public void Set(TopiEnumReference reference)
        {
            topiEnum = reference;
            Value = reference.Value;
        }

        private void OnEnable()
        {
            Debug.Assert(topiEnum != null, "topiEnum != null", this); 
        }
    }
}