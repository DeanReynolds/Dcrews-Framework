using System.Collections.Generic;

namespace Dcrew.Framework
{
    public class ObjectPool<T>
    {
        const int _overAllocateAmount = 4;

        public ToggleEvent WakeObj,
            ResetObj;

        public delegate T NewObjEvent();
        public delegate void ToggleEvent(T obj);

        readonly Stack<T> _inactive;
        readonly NewObjEvent _newObj;

        public ObjectPool(NewObjEvent newObj)
        {
            _newObj = newObj;
            _inactive = new Stack<T>();
        }
        public ObjectPool(NewObjEvent newObj, int initialCapacity)
        {
            _newObj = newObj;
            _inactive = new Stack<T>(initialCapacity);
            Expand(initialCapacity);
        }

        public void Expand(int amount)
        {
            for (int i = 0; i < amount; i++)
                _inactive.Push(_newObj());
        }

        public T Activate()
        {
            if (_inactive.Count <= 0)
                Expand(_overAllocateAmount);
            var obj = _inactive.Pop();
            WakeObj?.Invoke(obj);
            return obj;
        }
        public bool Deactivate(T obj)
        {
            _inactive.Push(obj);
            ResetObj?.Invoke(obj);
            return true;
        }
    }
}