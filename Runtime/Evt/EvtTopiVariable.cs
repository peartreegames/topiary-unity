using PeartreeGames.Evt.Variables;
using UnityEngine;

namespace PeartreeGames.Topiary.Unity
{
    public abstract class EvtTopiVariable<T> : EvtVariable<T>
    {
        [SerializeField] private string valueName;
        public string Name => valueName;
    }
}