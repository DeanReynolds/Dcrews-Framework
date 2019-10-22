using Dcrew.Framework;
using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    public static class NetPool
    {
        static IDictionary<Type, ObjectPool<object>> _pools = new Dictionary<Type, ObjectPool<object>>();
        static Stack<(object Packet, ObjectPool<object> Pool)> _spawnedPackets = new Stack<(object, ObjectPool<object>)>();

        public static T Spawn<T>() where T : NetPacket
        {
            var type = typeof(T);
            if (!_pools.ContainsKey(type))
                _pools.Add(type, new ObjectPool<object>(() => Activator.CreateInstance(type)));
            var packet = _pools[type].Spawn();
            _spawnedPackets.Push((packet, _pools[type]));
            return (T)packet;
        }
        
        internal static void DespawnSpawned()
        {
            foreach (var (packet, pool) in _spawnedPackets)
                pool.Despawn(packet);
            _spawnedPackets.Clear();
        }
    }
}