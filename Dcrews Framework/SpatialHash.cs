using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Dcrew.Framework
{
    /// <summary>For very fast but approximate spatial partitioning. See <see cref="Spacing"/> before use</summary>
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
                foreach (var obj in _stored.Keys)
                    Update(obj);
            }
        }
        
        static readonly IDictionary<int, HashSet<T>> _hash = new Dictionary<int, HashSet<T>>();
        static readonly IDictionary<T, int> _stored = new Dictionary<T, int>();

        static int _spacing = DEFAULT_SPACING;

        public static void Add(T obj)
        {
            var bucket = Bucket(obj);
            Add(obj, bucket);
            _stored.Add(obj, bucket);
        }
        public static bool Update(T obj)
        {
            var bucket = Bucket(obj);
            if (bucket == _stored[obj])
                return false;
            _hash[_stored[obj]].Remove(obj);
            Add(obj, bucket);
            _stored[obj] = bucket;
            return true;
        }
        public static void Remove(T obj)
        {
            _hash[_stored[obj]].Remove(obj);
            _stored.Remove(obj);
        }
        public static void Clear()
        {
            _hash.Clear();
            _stored.Clear();
        }
        public static IEnumerable<T> Query(Vector2 position)
        {
            var xn1 = (int)(position.X / Spacing);
            var yn1 = (int)(position.Y / Spacing);
            var bucket = (17 * 23 + xn1.GetHashCode()) * 23 + yn1.GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + (xn1 - 1).GetHashCode()) * 23 + yn1.GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + (xn1 + 1).GetHashCode()) * 23 + yn1.GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + xn1.GetHashCode()) * 23 + (yn1 - 1).GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + xn1.GetHashCode()) * 23 + (yn1 + 1).GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + (xn1 - 1).GetHashCode()) * 23 + (yn1 - 1).GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + (xn1 + 1).GetHashCode()) * 23 + (yn1 - 1).GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + (xn1 - 1).GetHashCode()) * 23 + (yn1 + 1).GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
            bucket = (17 * 23 + (xn1 + 1).GetHashCode()) * 23 + (yn1 + 1).GetHashCode();
            if (_hash.ContainsKey(bucket))
                foreach (var i in _hash[bucket])
                    yield return i;
        }

        public static bool Contains(T obj) => _stored.ContainsKey(obj);
        
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