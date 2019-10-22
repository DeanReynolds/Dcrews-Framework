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

        internal static readonly IDictionary<int, Type> _packets = new Dictionary<int, Type>(),
            _initialDataPackets = new Dictionary<int, Type>();
        internal static readonly IDictionary<Type, (int ID, Action<NetBitPackedDataWriter, object>[] Writes, Action<NetBitPackedDataReader, object>[] Reads, NetPacket Instance)> _packetInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket)>(),
            _initialDataPacketInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket)>();
        internal static readonly int _initialPacketBits = Enum.GetValues(typeof(INITIAL_PACKET)).Length - 1,
            _initialDataIDBits = Enum.GetValues(typeof(INITIAL_DATA_ID)).Length - 1;
        internal static readonly IDictionary<string, (NET_FILE_STATE State, byte[] MD5Hash)> _netFileInfo = new Dictionary<string, (NET_FILE_STATE, byte[])>();
        internal static readonly NetPacket[] _netFilePackets = new NetPacket[256];

        internal static int _maxPlayers,
            _maxPlayersIndex;

        internal enum INITIAL_PACKET { INITIAL_DATA, PEER_JOIN, PEER_QUIT }
        internal enum INITIAL_DATA_ID { DATA }
        internal enum NET_FILE_STATE { AWAITING_HASH, IDENTICAL_FILE, FILE_DIFFERENT }

        static readonly EventListener _eventListener = new EventListener();
        static readonly NetManager _manager = new NetManager(_eventListener) { AutoRecycle = true, UpdateTime = 15 };
        static readonly IDictionary<int, NetPeer> _peers1 = new Dictionary<int, NetPeer>(), // doesn't include self in listen server
            _peers2 = new Dictionary<int, NetPeer>(); // does include self in listen server
        static readonly IDictionary<NetPeer, int> _peerIDs = new Dictionary<NetPeer, int>();
        static readonly IDictionary<NetPeer, IDictionary<Type, ISet<int>>> _expectedPacketsWithFiles = new Dictionary<NetPeer, IDictionary<Type, ISet<int>>>();

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
            if (_manager.IsRunning)
                return;
            _maxPlayersIndex = (_maxPlayers = maxPlayers) - 1;
            _manager.Start(port);
            Room.AddUpdate(Tick);
            MGGame.OnRoomOpen += OnRoomOpen;
            MGGame.OnRoomClosed += OnRoomClosed;
            IsListen = listenServer;
        }
        public static void Stop()
        {
            if (!_manager.IsRunning)
                return;
            MGGame.OnRoomClosed -= OnRoomClosed;
            MGGame.OnRoomOpen -= OnRoomOpen;
            Room.RemoveUpdate(Tick);
            _manager.Stop(true);
            _peerIDs.Clear();
            _peers2.Clear();
            _peers1.Clear();
            _expectedPacketsWithFiles.Clear();
            _netFileInfo.Clear();
            for (var i = 0; i < _netFilePackets.Length; i++)
                _netFilePackets[i] = null;
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
            var t = packet.GetType();
            if (NetClient._packetsWithFilesInfo.ContainsKey(t))
            {
                var (packetID, _, _, _) = _packetInfo[t];
                var (_, writes, _, _) = NetClient._packetsWithFilesInfo[t];
                writer.WriteRangedInt(0, _packets.Count, packetID);
                int index;
                for (index = 0; index < _netFilePackets.Length; index++)
                    if (_netFilePackets[index] == null)
                        break;
                writer.WriteRangedInt(0, _netFilePackets.Length - 1, index);
                _netFilePackets[index] = packet;
                writer.Write(true);
                for (var i = 0; i < writes.Length; i++)
                    writes[i](writer, packet);
                foreach (var peer in _peers1.Values)
                    if (peer != excludePeer)
                    {
                        if (!_expectedPacketsWithFiles[peer].ContainsKey(t))
                            _expectedPacketsWithFiles[peer].Add(t, new HashSet<int> { index });
                        else
                            _expectedPacketsWithFiles[peer][t].Add(index);
                        peer.Send(writer.Data, options);
                    }
            }
            else
            {
                var (packetID, writes, _, _) = _packetInfo[t];
                writer.WriteRangedInt(0, _packets.Count, packetID);
                for (var i = 0; i < writes.Length; i++)
                    writes[i](writer, packet);
                foreach (var peer in _peers1.Values)
                    if (peer != excludePeer)
                        peer.Send(writer.Data, options);
            }
        }
        public static void SendTo(NetPacket packet, int peerID, DeliveryMethod options) => SendTo(packet, _peers1[peerID], options);

        internal static void SendTo(NetPacket packet, NetPeer peer, DeliveryMethod options)
        {
            var writer = new NetBitPackedDataWriter();
            var t = packet.GetType();
            if (NetClient._packetsWithFilesInfo.ContainsKey(t))
            {
                var (packetID, _, _, _) = _packetInfo[t];
                var (_, writes, _, _) = NetClient._packetsWithFilesInfo[t];
                writer.WriteRangedInt(0, _packets.Count, packetID);
                int index;
                for (index = 0; index < _netFilePackets.Length; index++)
                    if (_netFilePackets[index] == null)
                        break;
                writer.WriteRangedInt(0, _netFilePackets.Length - 1, index);
                _netFilePackets[index] = packet;
                writer.Write(true);
                for (var i = 0; i < writes.Length; i++)
                    writes[i](writer, packet);
                if (!_expectedPacketsWithFiles[peer].ContainsKey(t))
                    _expectedPacketsWithFiles[peer].Add(t, new HashSet<int> { index });
                else
                    _expectedPacketsWithFiles[peer][t].Add(index);
            }
            else
            {
                var (packetID, writes, _, _) = _packetInfo[t];
                writer.WriteRangedInt(0, _packets.Count, packetID);
                for (var i = 0; i < writes.Length; i++)
                    writes[i](writer, packet);
            }
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
                    writer.WriteRangedInt(0, _initialDataPackets.Count, 0);
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
                            if (NetClient._packetsWithFilesInfo.ContainsKey(packet.GetType()))
                            {
                                var (packetID, writes, _, _) = NetClient._packetsWithFilesInfo[packet.GetType()];
                                writer.WriteRangedInt(0, _initialDataPackets.Count, packetID);
                                writer.Write(i == InitialDataCount - 1);
                                writer.Write(i);
                                writer.Write(true);
                                for (var j = 0; j < writes.Length; j++)
                                    writes[j](writer, packet);
                            }
                            else
                            {
                                var (packetID, writes, _, _) = _initialDataPacketInfo[packet.GetType()];
                                writer.WriteRangedInt(0, _initialDataPackets.Count, packetID);
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
                _expectedPacketsWithFiles.Remove(peer);
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
                    var packetID2 = data.ReadRangedInt(0, _initialDataPackets.Count);
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
                        _expectedPacketsWithFiles.Add(peer, new Dictionary<Type, ISet<int>>());
                    }
                    else
                    {
                        var (_, _, reads, _) = NetClient._packetsWithFilesInfo[NetClient._initialDataPackets[packetID2]];
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
                            writer.WriteRangedInt(0, _initialDataPackets.Count, packetID2);
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
                    if (_expectedPacketsWithFiles[peer].ContainsKey(t))
                    {
                        var (_, _, reads2, _) = NetClient._packetsWithFilesInfo[t];
                        var (_, writes2, _, _) = _packetInfo[t];
                        var i = data.ReadInt();
                        var packet = _netFilePackets[i];
                        for (var j = 0; j < reads2.Length; j++)
                            reads2[j](data, packet);
                        var writer = new NetBitPackedDataWriter();
                        {
                            writer.WriteRangedInt(0, _packets.Count, packetID);
                            writer.WriteRangedInt(0, _netFilePackets.Length - 1, i);
                            writer.Write(false);
                            for (var j = 0; j < writes2.Length; j++)
                                writes2[j](writer, packet);
                            peer.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                        }
                        _netFilePackets[i] = null;
                        _expectedPacketsWithFiles[peer][t].Remove(i);
                        if (_expectedPacketsWithFiles[peer][t].Count == 0)
                            _expectedPacketsWithFiles[peer].Remove(t);
                    }
                    else
                    {
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
        internal static readonly IDictionary<Type, (int ID, Action<NetBitPackedDataWriter, object>[] Writes, Action<NetBitPackedDataReader, object>[] Reads, NetPacket Instance)> _packetsWithFilesInfo = new Dictionary<Type, (int, Action<NetBitPackedDataWriter, object>[], Action<NetBitPackedDataReader, object>[], NetPacket)>();
        internal static IDictionary<Type, int> _packetStates = new Dictionary<Type, int>();

        static readonly EventListener _eventListener = new EventListener();
        static readonly NetManager _manager = new NetManager(_eventListener);

        static NetClient()
        {
            var id = 1;
            foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(NetPacket)) && x.GetCustomAttribute(typeof(InitialServerData)) == null && x.GetCustomAttribute(typeof(ServerOnlySend)) == null))
            {
                _packets.Add(id, t);
                var (writes, reads) = GetProcessors(t.GetFields().Where(x => x.IsPublic && !x.IsStatic && x.GetCustomAttribute(typeof(ServerOnlySend)) == null && x.FieldType != typeof(FileStream)).ToArray());
                _packetInfo.Add(t, (id++, writes, reads, (NetPacket)Activator.CreateInstance(t), t.GetCustomAttribute(typeof(ServerNoRelay)) == null));
            }
            id = 1;
            foreach (var t in Assembly.GetEntryAssembly().GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.IsSubclassOf(typeof(NetPacket))))
            {
                var (writes, reads) = GetProcessors(t.GetFields().Where(x => x.IsPublic && !x.IsStatic && x.FieldType == typeof(FileStream)).ToArray());
                if (writes.Length > 0)
                {
                    _initialDataPackets.Add(id, t);
                    _packetsWithFilesInfo.Add(t, (id++, writes, reads, (NetPacket)Activator.CreateInstance(t)));
                }
            }
        }

        public static void Start(string ip, int port)
        {
            if (_manager.IsRunning)
                return;
            _manager.Start();
            Room.AddUpdate(Tick);
            MGGame.OnRoomOpen += OnRoomOpen;
            MGGame.OnRoomClosed += OnRoomClosed;
            _manager.Connect(ip, port, new NetDataWriter());
        }
        public static void Stop()
        {
            if (!_manager.IsRunning)
                return;
            MGGame.OnRoomClosed -= OnRoomClosed;
            MGGame.OnRoomOpen -= OnRoomOpen;
            Room.RemoveUpdate(Tick);
            _manager.Stop(true);
            _selfID = -1;
        }

        public static void Send(NetPacket packet, DeliveryMethod options)
        {
            var writer = new NetBitPackedDataWriter();
            var t = packet.GetType();
#if DEBUG
            if (!_packetInfo.ContainsKey(t))
                throw new Exception("Client can't send this packet! Maybe it's a server-only-send packet?");
#endif
            var (packetID, writes, _, _, _) = _packetInfo[t];
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
                            var (packetID2, isLastPacket) = (inData.ReadRangedInt(0, NetServer._initialDataPackets.Count), inData.ReadBool());
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
                                if (_packetsWithFilesInfo.ContainsKey(t2))
                                {
                                    var (index, isInit) = (inData.ReadInt(), inData.ReadBool());
                                    Console.WriteLine($"Client received initial data: ID: {packetID2}, IsLast: {isLastPacket}, i: {index}, isInit: {isInit}");
                                    var (_, writes2, reads3, _) = _packetsWithFilesInfo[t2];
                                    if (isInit)
                                    {
                                        for (var i = 0; i < reads3.Length; i++)
                                            reads3[i](inData, instance2);
                                        var writer = new NetBitPackedDataWriter();
                                        {
                                            writer.WriteRangedInt(0, _packets.Count, 0);
                                            writer.WriteRangedInt(0, NetServer._initialDataPackets.Count, packetID2);
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
                                    writer.WriteRangedInt(0, _packetsWithFilesInfo.Count, 0);
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
                if (_packetsWithFilesInfo.ContainsKey(t))
                {
                    var (index, isInit) = (inData.ReadRangedInt(0, NetServer._netFilePackets.Length - 1), inData.ReadBool());
                    if (isInit)
                    {
                        var (_, writes, reads2, _) = _packetsWithFilesInfo[t];
                        for (var i = 0; i < reads2.Length; i++)
                            reads2[i](inData, instance);
                        var writer = new NetBitPackedDataWriter();
                        {
                            writer.WriteRangedInt(0, _packets.Count, packetID);
                            writer.Write(index);
                            for (var i = 0; i < writes.Length; i++)
                                writes[i](writer, instance);
                            _server.Send(writer.Data, DeliveryMethod.ReliableOrdered);
                        }
                        return;
                    }
                }
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