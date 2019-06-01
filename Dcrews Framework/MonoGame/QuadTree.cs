using Dcrew.Framework.MonoGame.Interfaces;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Dcrew.Framework.MonoGame
{
    public class QuadTree<T> where T : IAABB
    {
        readonly Node _mainNode;
        readonly Dictionary<T, Node> _stored;

        public QuadTree(Rectangle bounds)
        {
            _mainNode = new Node(bounds);
            _stored = new Dictionary<T, Node>();
        }

        public bool Add(T item)
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

        public bool Remove(T item)
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

        public HashSet<T> Query(Rectangle area) => _mainNode.Query(area);

        public class Node
        {
            public readonly Rectangle Bounds;

            public bool IsEmpty => (_items.Count == 0) && (_nodes.Count == 0);
            public int AllCount
            {
                get
                {
                    int count = 0;
                    count += _items.Count;
                    foreach (Node node in _nodes)
                        count += node.AllCount;
                    return count;
                }
            }
            public List<T> AllItems
            {
                get
                {
                    List<T> results = new List<T>();
                    results.AddRange(_items);
                    foreach (Node node in _nodes)
                        results.AddRange(node.AllItems);
                    return results;
                }
            }

            readonly HashSet<T> _items;
            readonly List<Node> _nodes;

            public Node(Rectangle bounds)
            {
                Bounds = bounds;
                _items = new HashSet<T>();
                _nodes = new List<Node>();
            }

            public Node Add(T item)
            {
                if (_nodes.Count == 0)
                    CreateSubNodes();
                foreach (var node in _nodes)
                    if (node.Bounds.Contains(item.AABB))
                    {
                        Node node2;
                        if ((node2 = node.Add(item)) != null)
                            return node2;
                        break;
                    }
                _items.Add(item);
                return this;
            }

            public bool Remove(T item)
            {
                if (_items.Contains(item))
                {
                    _items.Remove(item);
                    return true;
                }
                return false;
            }

            public HashSet<T> Query(Rectangle area)
            {
                var items = new HashSet<T>();
                foreach (T item in _items)
                    if (area.Intersects(item.AABB))
                        items.Add(item);
                foreach (Node node in _nodes)
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
                if ((Bounds.Height * Bounds.Width) <= 1024)
                    return;
                var halfWidth = Bounds.Width / 2;
                var halfHeight = Bounds.Height / 2;
                _nodes.Add(new Node(new Rectangle(Bounds.Location.X, Bounds.Location.Y, halfWidth, halfHeight)));
                _nodes.Add(new Node(new Rectangle(Bounds.Left, Bounds.Top + halfHeight, halfWidth, halfHeight)));
                _nodes.Add(new Node(new Rectangle(Bounds.Left + halfWidth, Bounds.Top, halfWidth, halfHeight)));
                _nodes.Add(new Node(new Rectangle(Bounds.Left + halfWidth, Bounds.Top + halfHeight, halfWidth, halfHeight)));
            }
        }
    }
}