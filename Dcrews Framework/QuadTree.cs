using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Dcrew.Framework
{
    /// <summary>For fast and accurate spatial partitioning</summary>
    public static class QuadTree<T> where T : IAABB
    {
        public static Rectangle Bounds
        {
            get => _mainNode.Bounds;
            set
            {
                if (_mainNode != null)
                {
                    var items = _mainNode.AllItems;
                    _mainNode = Pool<Node>.Spawn();
                    _mainNode.Bounds = value;
                    foreach (var i in items)
                        _mainNode.Add(i);
                }
                else
                {
                    _mainNode = Pool<Node>.Spawn();
                    _mainNode.Bounds = value;
                }
            }
        }

        public static readonly IDictionary<T, Node> _stored = new Dictionary<T, Node>();

        static Node _mainNode;

        static readonly HashSet<HashSet<T>> _setsToRecycle = new HashSet<HashSet<T>>();

        public static bool Add(T item)
        {
            if (_stored.ContainsKey(item))
                return false;
            Node node;
            if ((node = _mainNode.Add(item)) != null)
            {
                _stored.Add(item, node);
                return true;
            }
            return false;
        }
        public static bool Remove(T item)
        {
            if (!_stored.ContainsKey(item))
                return false;
            if (_stored[item].Remove(item))
            {
                _stored.Remove(item);
                return true;
            }
            return false;
        }
        public static bool Update(T item)
        {
            if (!_stored.ContainsKey(item))
                return false;
            if (_stored[item].Remove(item))
            {
                _stored.Remove(item);
                Add(item);
                return true;
            }
            return false;
        }

        /// <summary>Call <see cref="Recycle(HashSet{T})"/> on the returned set when done with it</summary>
        public static HashSet<T> Query(Rectangle area)
        {
            var result = _mainNode.Query(area);
            _setsToRecycle.Add(result);
            return result;
        }
        /// <summary>Call <see cref="Recycle(HashSet{T})"/> on the returned set when done with it</summary>
        public static HashSet<T> Query(Vector2 pos)
        {
            var result = _mainNode.Query(new Rectangle(MGMath.Round(pos.X), MGMath.Round(pos.Y), 1, 1));
            _setsToRecycle.Add(result);
            return result;
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
            public Rectangle Bounds { get; internal set; }

            public bool IsEmpty => (_items.Count == 0) && (_nodes.Count == 0);
            public int AllCount
            {
                get
                {
                    int count = 0;
                    count += _items.Count;
                    foreach (var node in _nodes)
                        count += node.AllCount;
                    return count;
                }
            }
            public HashSet<T> AllItems
            {
                get
                {
                    var results = Pool<HashSet<T>>.Spawn();
                    results.Clear();
                    results.UnionWith(_items);
                    foreach (var node in _nodes)
                        results.UnionWith(node.AllItems);
                    return results;
                }
            }

            readonly HashSet<T> _items = Pool<HashSet<T>>.Spawn();
            readonly List<Node> _nodes = Pool<List<Node>>.Spawn();

            public Node Add(T item)
            {
                if (_items.Count >= 16 && _nodes.Count == 0 && Bounds.Width * Bounds.Height > 1024)
                {
                    CreateSubNodes();
                    var itemsToRemove = Pool<HashSet<T>>.Spawn();
                    foreach (var i in _items)
                    {
                        var addedToANode = false;
                        foreach (var node in _nodes)
                            if (node.Bounds.Contains(i.AABB.Center))
                            {
                                Node node2;
                                if ((node2 = node.Add(i)) != null)
                                {
                                    addedToANode = true;
                                    _stored[i] = node2;
                                }
                                break;
                            }
                        if (addedToANode)
                            itemsToRemove.Add(i);
                    }
                    foreach (var i in itemsToRemove)
                        _items.Remove(i);
                }
                foreach (var node in _nodes)
                    if (node.Bounds.Contains(item.AABB.Center))
                    {
                        Node node2;
                        if ((node2 = node.Add(item)) != null)
                            return node2;
                    }
                _items.Add(item);
                return this;
            }
            public bool Remove(T item)
            {
                if (_items.Contains(item))
                {
                    _items.Remove(item);
                    if (AllCount == 0)
                        _nodes.Clear();
                    return true;
                }
                return false;
            }
            public HashSet<T> Query(Rectangle area)
            {
                var items = Pool<HashSet<T>>.Spawn();
                items.Clear();
                foreach (T item in _items)
                    if (area.Intersects(item.AABB))
                        items.Add(item);
                foreach (var node in _nodes)
                {
                    if (node.Bounds.Contains(area))
                    {
                        foreach (T item in node.Query(area))
                            items.Add(item);
                        break;
                    }
                    if (area.Contains(node.Bounds))
                        foreach (T item in node.AllItems)
                            items.Add(item);
                    else if (node.Bounds.Intersects(area))
                        foreach (T item in node.Query(area))
                            items.Add(item);
                }
                return items;
            }

            void CreateSubNodes()
            {
                if (Bounds.Height * Bounds.Width <= 1024)
                    return;
                int halfWidth = Bounds.Width / 2,
                    halfHeight = Bounds.Height / 2;
                var topLeft = Pool<Node>.Spawn();
                topLeft.Bounds = new Rectangle(Bounds.Left, Bounds.Top, halfWidth, halfHeight);
                _nodes.Add(topLeft);
                var bottomLeft = Pool<Node>.Spawn();
                int midY = Bounds.Top + halfHeight,
                    height = Bounds.Bottom - midY;
                bottomLeft.Bounds = new Rectangle(Bounds.Left, midY, halfWidth, height);
                _nodes.Add(bottomLeft);
                var topRight = Pool<Node>.Spawn();
                int midX = Bounds.Left + halfWidth,
                    width = Bounds.Right - midX;
                topRight.Bounds = new Rectangle(midX, Bounds.Top, width, halfHeight);
                _nodes.Add(topRight);
                var bottomRight = Pool<Node>.Spawn();
                bottomRight.Bounds = new Rectangle(midX, midY, width, height);
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