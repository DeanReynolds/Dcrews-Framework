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

        readonly Queue<T> _inactive;
        readonly HashSet<T> _active;
        readonly NewObjEvent _newObj;

        public ObjectPool(NewObjEvent newObj)
        {
            _newObj = newObj;
            _inactive = new Queue<T>();
            _active = new HashSet<T>();
        }
        public ObjectPool(NewObjEvent newObj, int initialCapacity)
        {
            _newObj = newObj;
            _inactive = new Queue<T>(initialCapacity);
            _active = new HashSet<T>(initialCapacity);
            Expand(initialCapacity);
        }

        public void Expand(int amount)
        {
            for (int i = 0; i < amount; i++)
                _inactive.Enqueue(_newObj());
        }

        public T Activate()
        {
            if (_inactive.Count <= 0)
                Expand(_overAllocateAmount);
            var obj = _inactive.Dequeue();
            _active.Add(obj);
            WakeObj?.Invoke(obj);
            return obj;
        }
        public bool Deactivate(T obj)
        {
            if (!_active.Contains(obj))
                return false;
            _active.Remove(obj);
            _inactive.Enqueue(obj);
            ResetObj?.Invoke(obj);
            return true;
        }
    }
}