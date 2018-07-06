﻿using SpeedDate.Network.Interfaces;
using SpeedDate.Network.LiteNetLib;
using SpeedDate.Network.LiteNetLib.Utils;

namespace SpeedDate.Network
{
    class NetPeerAdapter : BasePeer
    {
        public override long Id => _socket.ConnectId;
        public override bool IsConnected => _socket.ConnectionState == ConnectionState.Connected;

        internal readonly NetPeer _socket;

        public ConnectionState ConnectionState => _socket.ConnectionState;

        public NetPeerAdapter(NetPeer socket, AppUpdater timer) : base(timer)
        {
            _socket = socket;
        }

        public override void SendMessage(IMessage message, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableUnordered)
        {
            _socket.Send(message.ToBytes(), deliveryMethod);
        }

        public override void Disconnect(string reason)
        {
            _socket.Disconnect(NetDataWriter.FromString(reason));
        }
    }
}