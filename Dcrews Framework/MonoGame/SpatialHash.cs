using Dcrew.Framework.MonoGame.Interfaces;
using Microsoft.Xna.Framework;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Dcrew.Framework.MonoGame
{
    public class SpatialHash<T> where T : IAABB
    {
        readonly Hashtable _hash = new Hashtable();
        readonly int _spacing,
            _levelWidth,
            _levelHeight;
        readonly Dictionary<T, int> _objsAdded = new Dictionary<T, int>();

        public SpatialHash(int spacing, int levelWidth, int levelHeight)
        {
            _spacing = spacing;
            _levelWidth = (int)Math.Ceiling(levelWidth / (float)_spacing);
            _levelHeight = (int)Math.Ceiling(levelHeight / (float)_spacing);
            for (int x = 0; x < _levelWidth; x++)
                for (int y = 0; y < _levelHeight; y++)
                    _hash.Add(x + _levelWidth * y, new ArrayList());
        }

        public void Add(T obj)
        {
            var bucket = (int)(obj.AABB.Center.X / (float)_spacing) + _levelWidth * (int)(obj.AABB.Center.Y / (float)_spacing);
            ((ArrayList)_hash[bucket]).Add(obj);
            _objsAdded.Add(obj, bucket);
        }
        public void Remove(T obj) => ((ArrayList)_hash[_objsAdded[obj]]).Remove(obj);

        public void Update(T obj)
        {
            var bucket = (int)(obj.AABB.Center.X / (float)_spacing) + _levelWidth * (int)(obj.AABB.Center.Y / (float)_spacing);
            if (bucket == _objsAdded[obj])
                return;
            ((ArrayList)_hash[_objsAdded[obj]]).Remove(obj);
            ((ArrayList)_hash[bucket]).Add(obj);
            _objsAdded[obj] = bucket;
            return;
        }

        public bool Contains(T obj) => _objsAdded.ContainsKey(obj);

        public ArrayList Query(Vector2 position)
        {
            var objs = new ArrayList();
            var xn1 = (int)(position.X / _spacing);
            var xn2 = xn1 - 1;
            var xn3 = xn1 + 1;
            var yn1 = (int)(position.Y / _spacing);
            var yn2 = yn1 - 1;
            var yn3 = yn1 + 1;
            objs.AddRange((ArrayList)_hash[xn1 + _levelWidth * yn1]);
            objs.AddRange((ArrayList)_hash[xn1 + _levelWidth * yn2]);
            objs.AddRange((ArrayList)_hash[xn1 + _levelWidth * yn3]);
            objs.AddRange((ArrayList)_hash[xn2 + _levelWidth * yn1]);
            objs.AddRange((ArrayList)_hash[xn2 + _levelWidth * yn2]);
            objs.AddRange((ArrayList)_hash[xn2 + _levelWidth * yn3]);
            objs.AddRange((ArrayList)_hash[xn3 + _levelWidth * yn1]);
            objs.AddRange((ArrayList)_hash[xn3 + _levelWidth * yn2]);
            objs.AddRange((ArrayList)_hash[xn3 + _levelWidth * yn3]);
            return objs;
        }

        public void Clear()
        {
            _hash.Clear();
            _objsAdded.Clear();
        }
    }
}