using Dcrew.Framework;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using static LiteNetLib.NetPacket;

namespace LiteNetLib
{
    public delegate void PeerJoinRequestEvent(ConnectionRequest request);
    public delegate void PeerUpdateEvent(int id);
    public static class NetServer
    {
        public static event PeerJoinRequestEvent OnPeerJoinRequest;
        public static event PeerUpdateEvent OnPeerJoin,
            OnPeerQuit;

        public static bool IsListen { get; private set; }

        public static readonly IList<NetPacket> InitialData = new List<NetPacket>();

        public static bool IsRunning => (_manager != null) && _manager.IsRunning;

        internal static NetManager _manager;

        internal static readonly IDictionary<int, Type> _packets = new Dictionary<int, Type>(),
            _initialDataPackets = new Dictionary<int, Type>();
        internal static readonly IDictionary<Type, (int ID, Action<NetBitPackedDataWriter, object>[] Writes, Action<NetBitPackedDataReader, object>[] Reads, NetPacket Instance)> _packetInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket)>(),
            _initialDataPacketInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket)>();
        internal static readonly int _initialPacketBits = Enum.GetValues(typeof(INITIAL_PACKET)).Length - 1,
            _initialDataIDBits = Enum.GetValues(typeof(INITIAL_DATA_ID)).Length - 1;
        internal static readonly IDictionary<string, (NET_FILE_STATE State, byte[] MD5Hash)> _netFileInfo = new Dictionary<string, (NET_FILE_STATE, byte[])>();

        internal static int _maxPlayers,
            _maxPlayersIndex;

        internal enum INITIAL_PACKET { INITIAL_DATA, PEER_JOIN, PEER_QUIT }
        internal enum INITIAL_DATA_ID { DATA }
        internal enum NET_FILE_STATE { AWAITING_HASH, IDENTICAL_FILE, FILE_DIFFERENT }

        static IDictionary<int, NetPeer> _peers1; // doesn't include self in listen server
        static IDictionary<int, NetPeer> _peers2; // does include self in listen server
        static IDictionary<NetPeer, int> _peerIDs;

        static readonly EventListener _eventListener = new EventListener();

        static NetServer()
        {
            var id = 1;
            foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(NetPacket)) && x.GetCustomAttribute(typeof(InitialServerData)) == null && x.GetCustomAttribute(typeof(ClientOnlySend)) == null))
            {
                _packets.Add(id, t);
                var (writes, reads) = GetProcessors(t.GetFields().Where(x => x.IsPublic && !x.IsStatic && x.GetCustomAttribute(typeof(ClientOnlySend)) == null).ToArray());
                var newWrites = new Action<NetBitPackedDataWriter, object>[writes.Length + 1];
                Array.Copy(writes, 0, newWrites, 1, writes.Length);
                newWrites[0] = (NetBitPackedDataWriter writer, object packet) => { writer.WriteRangedInt(0, _maxPlayersIndex, ((NetPacket)packet).SenderID); };
                writes = newWrites;
                var newReads = new Action<NetBitPackedDataReader, object>[reads.Length + 1];
                Array.Copy(reads, 0, newReads, 1, reads.Length);
                newReads[0] = (NetBitPackedDataReader reader, object packet) => { ((NetPacket)packet).SenderID = reader.ReadRangedInt(0, _maxPlayersIndex); };
                reads = newReads;
                _packetInfo.Add(t, (id++, writes, reads, (NetPacket)Activator.CreateInstance(t)));
            }
            id = 1;
            foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(NetPacket)) && x.GetCustomAttribute(typeof(InitialServerData)) != null))
            {
                _initialDataPackets.Add(id, t);
                var (writes, reads) = GetProcessors(t.GetFields().Where(x => x.IsPublic && !x.IsStatic).ToArray());
                _initialDataPacketInfo.Add(t, (id++, writes, reads, (NetPacket)Activator.CreateInstance(t)));
            }
        }

        public static void Start(int port, int maxPlayers, bool listenServer)
        {
            if ((_manager == null) || !_manager.IsRunning)
            {
                _manager = new NetManager(_eventListener);
                _maxPlayersIndex = (_maxPlayers = maxPlayers) - 1;
                _manager.AutoRecycle = true;
                _manager.UpdateTime = 15;
                _manager.Start(port);
                _peers1 = new Dictionary<int, NetPeer>(maxPlayers);
                _peers2 = new Dictionary<int, NetPeer>(maxPlayers);
                _peerIDs = new Dictionary<NetPeer, int>(maxPlayers);
                Room.AddUpdate(Tick);
                MGGame.OnRoomOpen += OnRoomOpen;
                MGGame.OnRoomClosed += OnRoomClosed;
                IsListen = listenServer;
            }
        }
        public static void Stop()
        {
            if ((_manager != null) && _manager.IsRunning)
            {
                _manager.Stop(true);
                Room.RemoveUpdate(Tick);
                MGGame.OnRoomOpen -= OnRoomOpen;
                MGGame.OnRoomClosed -= OnRoomClosed;
                _manager = null;
                _peerIDs.Clear();
                _peerIDs = null;
                _peers1.Clear();
                _peers1 = null;
                _peers2.Clear();
                _peers2 = null;
            }
        }

        public static void SyncEachPlayer(Func<int, NetPacket> packet)
        {
            var (packetID, writes, _, _) = _packetInfo[packet(NetClient._selfID).GetType()];
            foreach (var p1 in _peers1)
            {
                var data = new NetBitPackedDataWriter();
                data.WriteRangedInt(0, _packets.Count, packetID);
                foreach (var p2 in _peers2)
                    if (p1.Key != p2.Key)
                    {
                        var inst = packet(p2.Key);
                        inst.SenderID = p2.Key;
                        for (var i = 0; i < writes.Length; i++)
                            writes[i](data, inst);
                    }
                p1.Value.Send(data.Data, DeliveryMethod.Sequenced);
            }
        }

        public static void SendToAll(NetPacket packet, DeliveryMethod options, NetPeer excludePeer = null)
        {
            var writer = new NetBitPackedDataWriter();
            var (packetID, writes, _, _) = _packetInfo[packet.GetType()];
            writer.WriteRangedInt(0, _packets.Count, packetID);
            for (var i = 0; i < writes.Length; i++)
                writes[i](writer, packet);
            foreach (var peer in _peers1.Values)
                if (peer != excludePeer)
                    peer.Send(writer.Data, options);
        }
        public static void SendTo(NetPacket packet, int peerID, DeliveryMethod options) => SendTo(packet, _peers1[peerID], options);

        internal static void SendTo(NetPacket packet, NetPeer peer, DeliveryMethod options)
        {
            var writer = new NetBitPackedDataWriter();
            var (packetID, writes, _, _) = _packetInfo[packet.GetType()];
            writer.WriteRangedInt(0, _packets.Count, packetID);
            for (var i = 0; i < writes.Length; i++)
                writes[i](writer, packet);
            peer.Send(writer.Data, options);
        }

        internal static UpdateState Tick()
        {
            _manager.PollEvents();
            NetPool.DespawnSpawned();
            return UpdateState.Finished;
        }
        internal static void OnRoomOpen() => Room.AddUpdate(Tick);
        internal static void OnRoomClosed() => Room.RemoveUpdate(Tick);

        class EventListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                if (IsListen && _peerIDs.Count == 0)
                {
                    _peerIDs.Add(peer, NetClient._selfID = 0);
                    Console.WriteLine($"Server initialized self with ID: {NetClient._selfID}");
                    NetClient.InvokeOnJoin(NetClient._selfID);
                    _peers2.Add(NetClient._selfID, peer);
                    return;
                }
                var peerID = -1;
                for (int i = 0; i < _maxPlayers; i++)
                    if (!_peerIDs.Values.Contains(i))
                    {
                        peerID = i;
                        break;
                    }
                _peerIDs.Add(peer, peerID);
                var writer = new NetBitPackedDataWriter();
                {
                    writer.WriteRangedInt(0, _packets.Count, 0);
                    writer.WriteRangedInt(0, _initialPacketBits, (int)INITIAL_PACKET.INITIAL_DATA);
                    writer.WriteRangedInt(0, _initialDataIDBits, (int)INITIAL_DATA_ID.DATA);
                    writer.Write(0);
                    writer.Write(InitialData.Count == 0);
                    writer.Write((byte)_maxPlayersIndex);
                    writer.WriteRangedInt(0, _maxPlayersIndex, peerID);
                    for (int j = 0; j < _maxPlayers; j++)
                        if (j != peerID)
                            writer.Write(_peerIDs.Values.Contains(j));
                    peer.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                    for (int i = 0, InitialDataCount = InitialData.Count; i < InitialDataCount; i++)
                    {
                        var packet = InitialData[i];
                        writer = new NetBitPackedDataWriter();
                        {
                            writer.WriteRangedInt(0, _packets.Count, 0);
                            writer.WriteRangedInt(0, _initialPacketBits, (int)INITIAL_PACKET.INITIAL_DATA);
                            writer.WriteRangedInt(0, _initialDataIDBits, (int)INITIAL_DATA_ID.DATA);
                            if (NetClient._initialDataPacketInfo.ContainsKey(packet.GetType()))
                            {
                                var (packetID, writes, _, _) = NetClient._initialDataPacketInfo[packet.GetType()];
                                writer.Write(packetID);
                                writer.Write(i == InitialDataCount - 1);
                                writer.Write(i);
                                writer.Write(true);
                                for (var j = 0; j < writes.Length; j++)
                                    writes[j](writer, packet);
                            }
                            else
                            {
                                var (packetID, writes, _, _) = _initialDataPacketInfo[packet.GetType()];
                                writer.Write(packetID);
                                writer.Write(i == InitialDataCount - 1);
                                for (var j = 0; j < writes.Length; j++)
                                    writes[j](writer, packet);
                            }
                            peer.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                        }
                    }
                }
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                int peerID = _peerIDs[peer];
                var writer = new NetBitPackedDataWriter();
                {
                    writer.WriteRangedInt(0, _packets.Count, 0);
                    writer.WriteRangedInt(0, _initialPacketBits, (int)INITIAL_PACKET.PEER_QUIT);
                    writer.WriteRangedInt(0, _maxPlayersIndex, peerID);
                    _manager.SendToAll(writer.Data, DeliveryMethod.ReliableOrdered, peer);
                }
                OnPeerQuit?.Invoke(peerID);
                _peerIDs.Remove(peer);
                _peers1.Remove(peerID);
                _peers2.Remove(peerID);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {
                var peerID = _peerIDs[peer];
                var data = new NetBitPackedDataReader(reader);
                var packetID = data.ReadRangedInt(0, NetClient._packets.Count);
                if (packetID == 0)
                {
                    Console.WriteLine($"Server received initial data from ID: {peerID}");
                    var packetID2 = data.ReadRangedInt(0, NetClient._initialDataPackets.Count);
                    if (packetID2 == 0)
                    {
                        var writer = new NetBitPackedDataWriter();
                        {
                            writer.WriteRangedInt(0, _packets.Count, 0);
                            writer.WriteRangedInt(0, _initialPacketBits, (int)INITIAL_PACKET.PEER_JOIN);
                            writer.WriteRangedInt(0, _maxPlayersIndex, peerID);
                        }
                        if (IsListen)
                        {
                            NetClient.InvokeOnPeerJoin(peerID);
                            foreach (var kv in _peerIDs)
                                if (kv.Value != NetClient._selfID && kv.Value != peerID)
                                    kv.Key.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                        }
                        else
                        {
                            OnPeerJoin?.Invoke(peerID);
                            _manager.SendToAll(writer.Data, DeliveryMethod.ReliableOrdered, peer);
                        }
                        _peers1.Add(peerID, peer);
                        _peers2.Add(peerID, peer);
                    }
                    else
                    {
                        var (_, _, reads, _) = NetClient._initialDataPacketInfo[NetClient._initialDataPackets[packetID2]];
                        var i = data.ReadInt();
                        var packet = InitialData[i];
                        for (var j = 0; j < reads.Length; j++)
                            reads[j](data, packet);
                        var writer = new NetBitPackedDataWriter();
                        {
                            writer.WriteRangedInt(0, _packets.Count, 0);
                            writer.WriteRangedInt(0, _initialPacketBits, (int)INITIAL_PACKET.INITIAL_DATA);
                            writer.WriteRangedInt(0, _initialDataIDBits, (int)INITIAL_DATA_ID.DATA);
                            var (_, writes2, _, _) = _initialDataPacketInfo[packet.GetType()];
                            writer.Write(packetID2);
                            writer.Write(i == InitialData.Count - 1);
                            writer.Write(i);
                            writer.Write(false);
                            for (var j = 0; j < writes2.Length; j++)
                                writes2[j](writer, packet);
                            peer.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                        }
                    }
                    return;
                }
                //Console.WriteLine($"Server received data: Packet ID: {id}");
                try
                {
                    var t = NetClient._packets[packetID];
                    var (_, _, reads, instance, shouldRelay) = NetClient._packetInfo[t];
                    for (var i = 0; i < reads.Length; i++)
                        reads[i](data, instance);
                    instance.SenderID = peerID;
                    if (!IsListen || (peerID != NetClient._selfID))
                        try { instance.Process(); }
                        catch (Exception e) { throw e; }
                    if (_packets.ContainsKey(packetID) && shouldRelay)
                        SendToAll(instance, deliveryMethod, peer);
                }
                catch { Console.WriteLine($"Server failed to read data: Packet ID: {packetID}"); }
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                if (_manager.PeersCount < _maxPlayers)
                    OnPeerJoinRequest?.Invoke(request);
                else
                    request.Reject();
            }
        }
    }
    public static class NetClient
    {
        public static event PeerUpdateEvent OnJoin,
            OnPeerJoin,
            OnPeerQuit;

        public static bool IsRunning => (_manager != null) && _manager.IsRunning;

        internal static NetPeer _server;
        internal static int _selfID = -1;

        internal static readonly IDictionary<int, Type> _packets = new Dictionary<int, Type>(),
            _initialDataPackets = new Dictionary<int, Type>();
        internal static readonly IDictionary<Type, (int ID, Action<NetBitPackedDataWriter, object>[] Writes, Action<NetBitPackedDataReader, object>[] Reads, NetPacket Instance, bool ServerShouldRelay)> _packetInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket, bool)>();
        internal static readonly IDictionary<Type, (int ID, Action<NetBitPackedDataWriter, object>[] Writes, Action<NetBitPackedDataReader, object>[] Reads, NetPacket Instance)> _initialDataPacketInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket)>();
        internal static IDictionary<Type, int> _packetStates = new Dictionary<Type, int>();

        static NetManager _manager;

        static readonly EventListener _eventListener = new EventListener();

        static NetClient()
        {
            var id = 1;
            foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(NetPacket)) && x.GetCustomAttribute(typeof(InitialServerData)) == null && x.GetCustomAttribute(typeof(ServerOnlySend)) == null))
            {
                _packets.Add(id, t);
                var (writes, reads) = GetProcessors(t.GetFields().Where(x => x.IsPublic && !x.IsStatic && x.GetCustomAttribute(typeof(ServerOnlySend)) == null).ToArray());
                _packetInfo.Add(t, (id++, writes, reads, (NetPacket)Activator.CreateInstance(t), t.GetCustomAttribute(typeof(ServerNoRelay)) == null));
            }
            id = 1;
            foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(NetPacket))))
            {
                var (writes, reads) = GetProcessors(t.GetFields().Where(x => x.IsPublic && !x.IsStatic && x.FieldType == typeof(FileStream)).ToArray());
                if (writes.Length > 0)
                {
                    _initialDataPackets.Add(id, t);
                    _initialDataPacketInfo.Add(t, (id++, writes, reads, (NetPacket)Activator.CreateInstance(t)));
                }
            }
        }

        public static void Start(string ip, int port)
        {
            if ((_manager == null) || !_manager.IsRunning)
            {
                _manager = new NetManager(_eventListener);
                _manager.Start();
                Room.AddUpdate(Tick);
                MGGame.OnRoomOpen += OnRoomOpen;
                MGGame.OnRoomClosed += OnRoomClosed;
                _manager.Connect(ip, port, new NetDataWriter());
            }
        }
        public static void Stop()
        {
            if ((_manager != null) && _manager.IsRunning)
            {
                _manager.Stop(true);
                Room.RemoveUpdate(Tick);
                MGGame.OnRoomOpen -= OnRoomOpen;
                MGGame.OnRoomClosed -= OnRoomClosed;
                _manager = null;
                _selfID = -1;
            }
        }

        public static void Send(NetPacket packet, DeliveryMethod options)
        {
            var writer = new NetBitPackedDataWriter();
            var (packetID, writes, _, _, _) = _packetInfo[packet.GetType()];
            writer.WriteRangedInt(0, _packets.Count, packetID);
            for (var i = 0; i < writes.Length; i++)
                writes[i](writer, packet);
            _server.Send(writer.Data, options);
        }

        internal static UpdateState Tick()
        {
            _manager.PollEvents();
            NetPool.DespawnSpawned();
            return UpdateState.Finished;
        }
        internal static void OnRoomOpen() => Room.AddUpdate(Tick);
        internal static void OnRoomClosed() => Room.RemoveUpdate(Tick);
        internal static void InvokeOnJoin(int selfID) => OnJoin?.Invoke(selfID);
        internal static void InvokeOnPeerJoin(int peerID) => OnPeerJoin?.Invoke(peerID);

        class EventListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                _server = peer;
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {
                var inData = new NetBitPackedDataReader(reader);
                var packetID = inData.ReadRangedInt(0, NetServer._packets.Count);
                if (packetID == 0)
                {
                    var id1 = (NetServer.INITIAL_PACKET)inData.ReadRangedInt(0, NetServer._initialPacketBits);
                    if (id1 == NetServer.INITIAL_PACKET.INITIAL_DATA)
                    {
                        var id2 = (NetServer.INITIAL_DATA_ID)inData.ReadRangedInt(0, NetServer._initialDataIDBits);
                        if (id2 == NetServer.INITIAL_DATA_ID.DATA)
                        {
                            var (packetID2, isLastPacket) = (inData.ReadInt(), inData.ReadBool());
                            if (packetID2 == 0)
                            {
                                NetServer._maxPlayers = (NetServer._maxPlayersIndex = inData.ReadByte()) + 1;
                                _selfID = inData.ReadRangedInt(0, NetServer._maxPlayersIndex);
                                for (var peerID = 0; peerID < NetServer._maxPlayers; peerID++)
                                    if (inData.ReadBool())
                                    {
                                        if (peerID == _selfID)
                                            peerID++;
                                        OnPeerJoin?.Invoke(peerID);
                                    }
                                Console.WriteLine($"Client received initial data: MaxPlayers: {NetServer._maxPlayers}, SelfID: {_selfID}");
                            }
                            else
                            {
                                var t2 = NetServer._initialDataPackets[packetID2];
                                var (_, _, reads2, instance2) = NetServer._initialDataPacketInfo[t2];
                                if (_initialDataPacketInfo.ContainsKey(t2))
                                {
                                    var (index, isInit) = (inData.ReadInt(), inData.ReadBool());
                                    Console.WriteLine($"Client received initial data: ID: {packetID2}, IsLast: {isLastPacket}, i: {index}, isInit: {isInit}");
                                    var (packetID3, writes2, reads3, _) = _initialDataPacketInfo[t2];
                                    if (isInit)
                                    {
                                        for (var i = 0; i < reads3.Length; i++)
                                            reads3[i](inData, instance2);
                                        var writer = new NetBitPackedDataWriter();
                                        {
                                            writer.WriteRangedInt(0, _packets.Count, 0);
                                            writer.WriteRangedInt(0, _initialDataPackets.Count, packetID3);
                                            writer.Write(index);
                                            for (var i = 0; i < writes2.Length; i++)
                                                writes2[i](writer, instance2);
                                            _server.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                                        }
                                        return;
                                    }
                                }
                                Console.WriteLine($"Client received initial data: ID: {packetID2}, IsLast: {isLastPacket}");
                                do
                                {
                                    for (var i = 0; i < reads2.Length; i++)
                                        reads2[i](inData, instance2);
                                    instance2.Process();
                                } while (!inData.EndOfData);
                            }
                            if (isLastPacket)
                            {
                                var writer = new NetBitPackedDataWriter();
                                {
                                    writer.WriteRangedInt(0, _packets.Count, 0);
                                    writer.WriteRangedInt(0, _initialDataPackets.Count, 0);
                                    _server.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                                }
                                OnJoin?.Invoke(_selfID);
                            }
                        }
                    }
                    else if (id1 == NetServer.INITIAL_PACKET.PEER_JOIN)
                        OnPeerJoin?.Invoke(inData.ReadRangedInt(0, NetServer._maxPlayersIndex));
                    else if (id1 == NetServer.INITIAL_PACKET.PEER_QUIT)
                        OnPeerQuit?.Invoke(inData.ReadRangedInt(0, NetServer._maxPlayersIndex));
                    return;
                }
                //Console.WriteLine($"Client received data: Packet ID: {id}");
                var t = NetServer._packets[packetID];
                var (_, _, reads, instance) = NetServer._packetInfo[t];
                do
                {
                    for (var i = 0; i < reads.Length; i++)
                        reads[i](inData, instance);
                    instance.Process();
                } while (!inData.EndOfData);
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
            }
        }
    }
}