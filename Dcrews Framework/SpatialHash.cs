using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Dcrew.Framework
{
    public class SpatialHash<T> where T : IAABB
    {
        readonly Dictionary<int, HashSet<T>> _hash = new Dictionary<int, HashSet<T>>();
        readonly int _spacing;
        readonly Dictionary<T, int> _objsAdded = new Dictionary<T, int>();
        readonly ObjectPool<HashSet<T>> _pool = new ObjectPool<HashSet<T>>(() => new HashSet<T>());

        public SpatialHash(int spacing) { _spacing = spacing; }

        public void Add(T obj)
        {
            var bucket = (17 * 23 + (obj.AABB.Center.X / _spacing).GetHashCode()) * 23 + (obj.AABB.Center.Y / _spacing).GetHashCode();
            _objsAdded.Add(obj, bucket);
            if (!_hash.ContainsKey(bucket))
            {
                var set = _pool.Spawn();
                set.Clear();
                set.Add(obj);
                _hash.Add(bucket, set);
                return;
            }
            _hash[bucket].Add(obj);
        }
        public bool Update(T obj)
        {
            if (!_objsAdded.ContainsKey(obj))
                return false;
            var bucket = (17 * 23 + (obj.AABB.Center.X / _spacing).GetHashCode()) * 23 + (obj.AABB.Center.Y / _spacing).GetHashCode();
            if (bucket == _objsAdded[obj])
                return false;
            _hash[_objsAdded[obj]].Remove(obj);
            _objsAdded[obj] = bucket;
            if (!_hash.ContainsKey(bucket))
            {
                var set = _pool.Spawn();
                set.Clear();
                set.Add(obj);
                _hash.Add(bucket, set);
                return true;
            }
            _hash[bucket].Add(obj);
            return true;
        }
        public void Remove(T obj)
        {
            _hash[_objsAdded[obj]].Remove(obj);
            _objsAdded.Remove(obj);
        }

        public bool Contains(T obj) => _objsAdded.ContainsKey(obj);

        public HashSet<T> Query(Vector2 position)
        {
            var xn1 = (int)(position.X / _spacing);
            var yn1 = (int)(position.Y / _spacing);
            var bucket = (17 * 23 + xn1.GetHashCode()) * 23 + yn1.GetHashCode();
            var objs = _pool.Spawn();
            objs.Clear();
            if (_hash.TryGetValue(bucket, out var set))
                objs.UnionWith(set);
            bucket = (17 * 23 + (xn1 - 1).GetHashCode()) * 23 + yn1.GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + (xn1 + 1).GetHashCode()) * 23 + yn1.GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + xn1.GetHashCode()) * 23 + (yn1 - 1).GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + xn1.GetHashCode()) * 23 + (yn1 + 1).GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + (xn1 - 1).GetHashCode()) * 23 + (yn1 - 1).GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + (xn1 + 1).GetHashCode()) * 23 + (yn1 - 1).GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + (xn1 - 1).GetHashCode()) * 23 + (yn1 + 1).GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            bucket = (17 * 23 + (xn1 + 1).GetHashCode()) * 23 + (yn1 + 1).GetHashCode();
            if (_hash.TryGetValue(bucket, out set))
                objs.UnionWith(set);
            return objs;
        }

        public void Clear()
        {
            _hash.Clear();
            _objsAdded.Clear();
        }

        public void Recycle(HashSet<T> objs) => _pool.Despawn(objs);
    }
}