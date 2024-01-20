using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    public class ByteData : ScriptableObject, ISerializationCallbackReceiver
    {
        public byte[] bytes;
        [SerializeField] private string[] externs;
        public SortedSet<string> ExternsSet;
        public void OnBeforeSerialize() => externs = ExternsSet?.ToArray();
        public void OnAfterDeserialize() => ExternsSet = new SortedSet<string>(externs);
    }
}