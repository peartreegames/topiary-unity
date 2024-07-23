using PeartreeGames.Evt.Variables.Lists;

namespace PeartreeGames.Topiary.Unity
{
    public class EvtTopiVariableList<T> : EvtVariableList<T>
    {
        public new void Add(T item) => Value.Add(item);
        public new void Remove(T item) => Value.Remove(item);
        public new void Clear() => Value.Clear();
        public new bool Contains(T item) => Value.Contains(item);
    }
}