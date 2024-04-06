using System;

namespace PeartreeGames.Topiary.Unity
{
    public abstract class EvtTopiReference : IDisposable
    {
        private protected readonly Conversation _conversation;

        protected EvtTopiReference(Conversation conversation)
        {
            _conversation = conversation;
        }

        public abstract void Dispose();
    }
    public abstract class EvtTopiReference<T> : EvtTopiReference
    {
        private protected readonly EvtTopiVariable<T> _evtVariable;

        public EvtTopiReference(Conversation conversation, EvtTopiVariable<T> evtVariable) : base(conversation)
        {
            _evtVariable = evtVariable;
            _evtVariable.OnEvt += OnValueChanged;
            _conversation.Dialogue.Subscribe(_evtVariable.Name, OnTopiValueChanged);
        }

        public override void Dispose()
        {
            _evtVariable.OnEvt -= OnValueChanged;
            _conversation.Dialogue.Unsubscribe(_evtVariable.Name, OnTopiValueChanged);
        }

        protected abstract void OnValueChanged(T value);

        protected abstract void OnTopiValueChanged(ref TopiValue topiValue);
    }

    public class EvtIntReference : EvtTopiReference<int>
    {
        public EvtIntReference(Conversation conversation, EvtTopiVariable<int> evtVariable) : base(
            conversation, evtVariable)
        {
            conversation.Dialogue.Set(evtVariable.Name, evtVariable.Value);
        }

        protected override void OnValueChanged(int value)
        {
            var name = _evtVariable.Name;
            var current = _conversation.Dialogue.GetValue(name);
            if (current.tag != TopiValue.Tag.Number) return;
            _conversation.Dialogue.Set(name, value);
        }

        protected override void OnTopiValueChanged(ref TopiValue topiValue)
        {
            if (topiValue.tag != TopiValue.Tag.Number) return;
            _evtVariable.Value = topiValue.Int;
        }
    }

    public class EvtFloatReference : EvtTopiReference<float>
    {
        public EvtFloatReference(Conversation conversation, EvtTopiVariable<float> evtVariable) :
            base(
                conversation, evtVariable)
        {
            conversation.Dialogue.Set(evtVariable.Name, evtVariable.Value);
        }

        protected override void OnValueChanged(float value)
        {
            var name = _evtVariable.Name;
            var current = _conversation.Dialogue.GetValue(name);
            if (current.tag != TopiValue.Tag.Number) return;
            _conversation.Dialogue.Set(name, value);
        }

        protected override void OnTopiValueChanged(ref TopiValue topiValue)
        {
            if (topiValue.tag != TopiValue.Tag.Number) return;
            _evtVariable.Value = topiValue.Float;
        }
    }


    public class EvtBoolReference : EvtTopiReference<bool>
    {
        public EvtBoolReference(Conversation conversation, EvtTopiVariable<bool> evtVariable) :
            base(
                conversation, evtVariable)
        {
            conversation.Dialogue.Set(evtVariable.Name, evtVariable.Value);
        }

        protected override void OnValueChanged(bool value)
        {
            var name = _evtVariable.Name;
            var current = _conversation.Dialogue.GetValue(name);
            if (current.tag != TopiValue.Tag.Number) return;
            _conversation.Dialogue.Set(name, value);
        }

        protected override void OnTopiValueChanged(ref TopiValue topiValue)
        {
            if (topiValue.tag != TopiValue.Tag.Number) return;
            _evtVariable.Value = topiValue.Bool;
        }
    }

    public class EvtStringReference : EvtTopiReference<string>
    {
        public EvtStringReference(Conversation conversation, EvtTopiVariable<string> evtVariable) :
            base(
                conversation, evtVariable)
        {
            conversation.Dialogue.Set(evtVariable.Name, evtVariable.Value);
        }

        protected override void OnValueChanged(string value)
        {
            var name = _evtVariable.Name;
            var current = _conversation.Dialogue.GetValue(name);
            if (current.tag != TopiValue.Tag.Number) return;
            _conversation.Dialogue.Set(name, value);
        }

        protected override void OnTopiValueChanged(ref TopiValue topiValue)
        {
            if (topiValue.tag != TopiValue.Tag.Number) return;
            _evtVariable.Value = topiValue.String;
        }
    }
}