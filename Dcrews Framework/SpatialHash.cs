using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Dcrew.Framework
{
    /// <summary>For very fast but approximate spatial partitioning</summary>
    public static class SpatialHash<T> where T : IAABB
    {
        const int DEFAULT_SPACING = 50;

        /// <summary>Set to your largest object collision radius. Defaults at 50</summary>
        public static int Spacing
        {
            get => _spacing;
            set
            {
                _spacing = value;
                foreach (var obj in _objsAdded.Keys)
                    Update(obj);
            }
        }

        static readonly HashSet<HashSet<T>> _setsToRecycle = new HashSet<HashSet<T>>();
        static readonly IDictionary<int, HashSet<T>> _hash = new Dictionary<int, HashSet<T>>();
        static readonly IDictionary<T, int> _objsAdded = new Dictionary<T, int>();

        static int _spacing = DEFAULT_SPACING;

        public static void Add(T obj)
        {
            var bucket = Bucket(obj);
            Add(obj, bucket);
            _objsAdded.Add(obj, bucket);
        }
        public static bool Update(T obj)
        {
            var bucket = Bucket(obj);
            if (bucket == _objsAdded[obj])
                return false;
            _hash[_objsAdded[obj]].Remove(obj);
            Add(obj, bucket);
            _objsAdded[obj] = bucket;
            return true;
        }
        public static void Remove(T obj)
        {
            _hash[_objsAdded[obj]].Remove(obj);
            _objsAdded.Remove(obj);
        }
        public static void Clear()
        {
            _hash.Clear();
            _objsAdded.Clear();
        }

        public static bool Contains(T obj) => _objsAdded.ContainsKey(obj);

        /// <summary>Call <see cref="Recycle(HashSet{T})"/> on the returned set when done with it</summary>
        public static HashSet<T> Query(Vector2 position)
        {
            var xn1 = (int)(position.X / Spacing);
            var yn1 = (int)(position.Y / Spacing);
            var bucket = (17 * 23 + xn1.GetHashCode()) * 23 + yn1.GetHashCode();
            var objs = Pool<HashSet<T>>.Spawn();
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
            _setsToRecycle.Add(objs);
            return objs;
        }

        public static void Recycle(HashSet<T> objs)
        {
            Pool<HashSet<T>>.Free(objs);
            _setsToRecycle.Remove(objs);
        }
        public static void Recycle()
        {
            foreach (var set in _setsToRecycle)
                Pool<HashSet<T>>.Free(set);
            _setsToRecycle.Clear();
        }

        static int Bucket(Point p) => (17 * 23 + (p.X / Spacing).GetHashCode()) * 23 + (p.Y / Spacing).GetHashCode();
        static int Bucket(T obj) => Bucket(obj.AABB.Center);

        static void Add(T obj, int bucket)
        {
            if (!_hash.ContainsKey(bucket))
            {
                var set = Pool<HashSet<T>>.Spawn();
                set.Clear();
                set.Add(obj);
                _hash.Add(bucket, set);
                return;
            }
            _hash[bucket].Add(obj);
        }
    }
}