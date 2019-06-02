using System.Collections.Generic;

namespace Dcrew.Framework
{
    public class ObjectPool<T>
    {
        const int _overAllocateAmount = 4;

        public NewObjEvent NewObj;
        public ToggleEvent OnActivate,
            OnDeactivate;

        public delegate T NewObjEvent();
        public delegate void ToggleEvent(T obj);

        readonly Queue<T> _inactive;
        readonly HashSet<T> _active;

        public ObjectPool(NewObjEvent newObj)
        {
            NewObj = newObj;
            _inactive = new Queue<T>();
            _active = new HashSet<T>();
        }
        public ObjectPool(NewObjEvent newObj, ToggleEvent onActivate, ToggleEvent onDeactivate) : this(newObj)
        {
            OnActivate = onActivate;
            OnDeactivate = onDeactivate;
        }
        public ObjectPool(NewObjEvent newObj, int initialCapacity)
        {
            NewObj = newObj;
            _inactive = new Queue<T>(initialCapacity);
            _active = new HashSet<T>(initialCapacity);
            Expand(initialCapacity);
        }
        public ObjectPool(NewObjEvent newObj, ToggleEvent onActivate, ToggleEvent onDeactivate, int initialCapacity) : this(newObj, initialCapacity)
        {
            OnActivate = onActivate;
            OnDeactivate = onDeactivate;
        }

        public void Expand(int amount)
        {
            if (NewObj == null)
                return;
            for (int i = 0; i < amount; i++)
                _inactive.Enqueue(NewObj());
        }

        public T Activate()
        {
            if (_inactive.Count <= 0)
                Expand(_overAllocateAmount);
            var obj = _inactive.Dequeue();
            _active.Add(obj);
            OnActivate?.Invoke(obj);
            return obj;
        }
        public bool Deactivate(T obj)
        {
            if (!_active.Contains(obj))
                return false;
            _active.Remove(obj);
            _inactive.Enqueue(obj);
            OnDeactivate?.Invoke(obj);
            return true;
        }
    }
}