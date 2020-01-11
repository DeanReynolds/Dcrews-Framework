using System.Collections.Generic;

namespace Dcrew.Framework
{
    public interface IPoolable
    {
        void Reset();
    }

    public static class Pool<T> where T : new()
    {
        const int _overAllocateAmount = 4;

        static readonly Stack<T> _objs = new Stack<T>();

        public static void Expand(int amount)
        {
            for (int i = 0; i < amount; i++)
                _objs.Push(new T());
        }

        public static T Spawn()
        {
            if (_objs.Count <= 0)
                Expand(_overAllocateAmount);
            return _objs.Pop();
        }
        public static void Free(T obj)
        {
            if (obj is IPoolable p)
                p.Reset();
            _objs.Push(obj);
        }
    }
}