using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Dcrew.Framework
{
    /// <summary>For fast and accurate spatial partitioning. Set <see cref="Bounds"/> before use</summary>
    public static class QuadTree<T> where T : IAABB
    {
        public static Rectangle Bounds
        {
            get => _mainNode.Bounds;
            set
            {
                if (_mainNode != null)
                {
                    var items = _stored.Keys.ToArray();
                    Pool<Node>.Free(_mainNode);
                    _mainNode = Pool<Node>.Spawn();
                    _mainNode.Bounds = value;
                    foreach (var i in items)
                        _stored[i] = _mainNode.Add(i);
                }
                else
                {
                    _mainNode = Pool<Node>.Spawn();
                    _mainNode.Bounds = value;
                }
                _mainNode._parent = null;
                Pool<Node>.EnsureSize(MGMath.Ceil(_mainNode.Bounds.Width / 32f * _mainNode.Bounds.Height / 32f));
            }
        }

        public static readonly IDictionary<T, Node> _stored = new Dictionary<T, Node>();

        static Node _mainNode;

        static readonly HashSet<HashSet<T>> _setsToRecycle = new HashSet<HashSet<T>>();

        public static bool Add(T item)
        {
            Node n;
            bool t = _stored.ContainsKey(item);
            if ((n = _mainNode.Add(item)) != null)
            {
                _stored.Add(item, n);
                return true;
            }
            return false;
        }
        public static void Remove(T item)
        {
            _stored[item].Remove(item);
            _stored.Remove(item);
        }
        public static void Update(T item)
        {
            Remove(item);
            Add(item);
        }

        /// <summary>Call <see cref="Recycle(HashSet{T})"/> on the returned set when done with it</summary>
        public static HashSet<T> Query(Rectangle area)
        {
            var r = Pool<HashSet<T>>.Spawn();
            r.Clear();
            r.UnionWith(_mainNode.Query(area));
            _setsToRecycle.Add(r);
            return r;
        }
        /// <summary>Call <see cref="Recycle(HashSet{T})"/> on the returned set when done with it</summary>
        public static HashSet<T> Query(Vector2 pos)
        {
            var r = Pool<HashSet<T>>.Spawn();
            r.Clear();
            r.UnionWith(_mainNode.Query(new Rectangle(MGMath.Round(pos.X), MGMath.Round(pos.Y), 1, 1)));
            _setsToRecycle.Add(r);
            return r;
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

        public class Node : IPoolable
        {
            const int MAX_ITEMS = 16;

            public Rectangle Bounds { get; internal set; }

            public bool IsEmpty => _items.Count == 0 && _nodes.Count == 0;
            public int AllCount
            {
                get
                {
                    int c = _items.Count;
                    foreach (var n in _nodes)
                        c += n.AllCount;
                    return c;
                }
            }
            public HashSet<T> AllItems
            {
                get
                {
                    _items2.Clear();
                    _items2.UnionWith(_items);
                    foreach (var n in _nodes)
                        _items2.UnionWith(n.AllItems);
                    return _items2;
                }
            }

            readonly HashSet<T> _items = new HashSet<T>(),
                _items2 = new HashSet<T>();
            readonly HashSet<Node> _nodes = new HashSet<Node>();

            internal Node _parent;


            public Node Add(T item)
            {
                Node n2;
                if (_items.Count >= MAX_ITEMS && _nodes.Count == 0 && Bounds.Width * Bounds.Height > 1024)
                {
                    CreateSubNodes();
                    _items2.Clear();
                    foreach (var i in _items)
                        foreach (var n in _nodes)
                            if (n.Bounds.Contains(i.AABB.Center))
                            {
                                if ((n2 = n.Add(i)) != null)
                                {
                                    _items2.Add(i);
                                    _stored[i] = n2;
                                }
                                break;
                            }
                    _items.ExceptWith(_items2);
                }
                foreach (var n in _nodes)
                    if (n.Bounds.Contains(item.AABB.Center))
                        if ((n2 = n.Add(item)) != null)
                            return n2;
                _items.Add(item);
                return this;
            }
            public void Remove(T item)
            {
                _items.Remove(item);
                if (_parent != null && _parent.AllCount < MAX_ITEMS)
                {
                    var items = _parent.AllItems;
                    foreach (var i in items)
                    {
                        _parent._items.Add(i);
                        _stored[i] = _parent;
                    }
                    foreach (var n in _parent._nodes)
                        Pool<Node>.Free(n);
                    _parent._nodes.Clear();
                }
            }
            public HashSet<T> Query(Rectangle area)
            {
                _items2.Clear();
                foreach (T i in _items)
                    if (area.Intersects(i.AABB))
                        _items2.Add(i);
                foreach (var n in _nodes)
                {
                    if (n.Bounds.Contains(area))
                    {
                        _items2.UnionWith(n.Query(area));
                        break;
                    }
                    if (area.Contains(n.Bounds))
                        _items2.UnionWith(n.AllItems);
                    else if (n.Bounds.Intersects(area))
                        _items2.UnionWith(n.Query(area));
                }
                return _items2;
            }

            void CreateSubNodes()
            {
                if (Bounds.Height * Bounds.Width <= 1024)
                    return;
                int halfWidth = Bounds.Width / 2,
                    halfHeight = Bounds.Height / 2;
                var topLeft = Pool<Node>.Spawn();
                topLeft.Bounds = new Rectangle(Bounds.Left, Bounds.Top, halfWidth, halfHeight);
                topLeft._parent = this;
                _nodes.Add(topLeft);
                var bottomLeft = Pool<Node>.Spawn();
                int midY = Bounds.Top + halfHeight,
                    height = Bounds.Bottom - midY;
                bottomLeft.Bounds = new Rectangle(Bounds.Left, midY, halfWidth, height);
                bottomLeft._parent = this;
                _nodes.Add(bottomLeft);
                var topRight = Pool<Node>.Spawn();
                int midX = Bounds.Left + halfWidth,
                    width = Bounds.Right - midX;
                topRight.Bounds = new Rectangle(midX, Bounds.Top, width, halfHeight);
                topRight._parent = this;
                _nodes.Add(topRight);
                var bottomRight = Pool<Node>.Spawn();
                bottomRight.Bounds = new Rectangle(midX, midY, width, height);
                bottomRight._parent = this;
                _nodes.Add(bottomRight);
            }

            public void Reset()
            {
                _items.Clear();
                _nodes.Clear();
            }
        }
    }
}