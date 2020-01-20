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

        public static void Add(T item) => _stored.Add(item, _mainNode.Add(item));
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
        public static IEnumerable<T> Query(Rectangle area)
        {
            foreach (var i in _mainNode.Query(area))
                yield return i;
        }
        public static IEnumerable<T> Query(Vector2 pos)
        {
            foreach (var i in _mainNode.Query(new Rectangle(MGMath.Round(pos.X), MGMath.Round(pos.Y), 1, 1)))
                yield return i;
        }

        public class Node : IPoolable
        {
            const int CAPACITY = 8;

            public Rectangle Bounds { get; internal set; }

            public int AllCount
            {
                get
                {
                    int c = _items.Count;
                    if (_nw != null)
                    {
                        c += _ne.AllCount;
                        c += _se.AllCount;
                        c += _sw.AllCount;
                        c += _nw.AllCount;
                    }
                    return c;
                }
            }
            public IEnumerable<T> AllItems
            {
                get
                {
                    foreach (var i in _items)
                        yield return i;
                    if (_nw != null)
                    {
                        foreach (var i in _ne.AllItems)
                            yield return i;
                        foreach (var i in _se.AllItems)
                            yield return i;
                        foreach (var i in _sw.AllItems)
                            yield return i;
                        foreach (var i in _nw.AllItems)
                            yield return i;
                    }
                }
            }
            public IEnumerable<T> AllSubItems
            {
                get
                {
                    foreach (var i in _ne.AllItems)
                        yield return i;
                    foreach (var i in _se.AllItems)
                        yield return i;
                    foreach (var i in _sw.AllItems)
                        yield return i;
                    foreach (var i in _nw.AllItems)
                        yield return i;
                }
            }

            readonly HashSet<T> _items = new HashSet<T>();

            internal Node _parent;

            Node _ne, _se, _sw, _nw;

            public Node Add(T item)
            {
                Node n2;
                Node MoveTo(T i, Node n)
                {
                    n2 = null;
                    if (n.Bounds.Contains(i.AABB.Center))
                        return n2 = n.Add(i);
                    return n2;
                }
                if (_nw == null)
                    if (_items.Count >= CAPACITY && Bounds.Width * Bounds.Height > 1024)
                    {
                        int halfWidth = Bounds.Width / 2,
                            halfHeight = Bounds.Height / 2;
                        _nw = Pool<Node>.Spawn();
                        _nw.Bounds = new Rectangle(Bounds.Left, Bounds.Top, halfWidth, halfHeight);
                        _nw._parent = this;
                        _sw = Pool<Node>.Spawn();
                        int midY = Bounds.Top + halfHeight,
                            height = Bounds.Bottom - midY;
                        _sw.Bounds = new Rectangle(Bounds.Left, midY, halfWidth, height);
                        _sw._parent = this;
                        _ne = Pool<Node>.Spawn();
                        int midX = Bounds.Left + halfWidth,
                            width = Bounds.Right - midX;
                        _ne.Bounds = new Rectangle(midX, Bounds.Top, width, halfHeight);
                        _ne._parent = this;
                        _se = Pool<Node>.Spawn();
                        _se.Bounds = new Rectangle(midX, midY, width, height);
                        _se._parent = this;
                        foreach (var i in _items)
                            if (MoveTo(i, _ne) != null || MoveTo(i, _se) != null || MoveTo(i, _sw) != null || MoveTo(i, _nw) != null)
                                _stored[i] = n2;
                        _items.Clear();
                    }
                    else
                        goto add;
                if (MoveTo(item, _ne) != null || MoveTo(item, _se) != null || MoveTo(item, _sw) != null || MoveTo(item, _nw) != null)
                    return n2;
                add:
                _items.Add(item);
                return this;
            }
            public void Remove(T item)
            {
                _items.Remove(item);
                if (_parent?._nw != null && _parent.AllCount < CAPACITY)
                {
                    foreach (var i in _parent.AllSubItems)
                    {
                        _parent._items.Add(i);
                        _stored[i] = _parent;
                    }
                    Pool<Node>.Free(_parent._ne);
                    Pool<Node>.Free(_parent._se);
                    Pool<Node>.Free(_parent._sw);
                    Pool<Node>.Free(_parent._nw);
                    _parent._ne = null;
                    _parent._se = null;
                    _parent._sw = null;
                    _parent._nw = null;
                }
            }
            public IEnumerable<T> Query(Rectangle area)
            {
                foreach (T i in _items)
                    if (area.Intersects(i.AABB))
                        yield return i;
                if (_nw == null)
                    yield break;
                if (_ne.Bounds.Contains(area))
                {
                    foreach (var i in _ne.Query(area))
                        yield return i;
                    yield break;
                }
                if (area.Contains(_ne.Bounds))
                    foreach (var i in _ne.AllItems)
                        yield return i;
                else if (_ne.Bounds.Intersects(area))
                    foreach (var i in _ne.Query(area))
                        yield return i;
                if (_se.Bounds.Contains(area))
                {
                    foreach (var i in _se.Query(area))
                        yield return i;
                    yield break;
                }
                if (area.Contains(_se.Bounds))
                    foreach (var i in _se.AllItems)
                        yield return i;
                else if (_se.Bounds.Intersects(area))
                    foreach (var i in _se.Query(area))
                        yield return i;
                if (_sw.Bounds.Contains(area))
                {
                    foreach (var i in _sw.Query(area))
                        yield return i;
                    yield break;
                }
                if (area.Contains(_sw.Bounds))
                    foreach (var i in _sw.AllItems)
                        yield return i;
                else if (_sw.Bounds.Intersects(area))
                    foreach (var i in _sw.Query(area))
                        yield return i;
                if (_nw.Bounds.Contains(area))
                {
                    foreach (var i in _nw.Query(area))
                        yield return i;
                    yield break;
                }
                if (area.Contains(_nw.Bounds))
                    foreach (var i in _nw.AllItems)
                        yield return i;
                else if (_nw.Bounds.Intersects(area))
                    foreach (var i in _nw.Query(area))
                        yield return i;
            }

            public void Reset() => _items.Clear();
        }
    }
}