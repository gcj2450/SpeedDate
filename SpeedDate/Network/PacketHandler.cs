﻿using SpeedDate.Network.Interfaces;

namespace SpeedDate.Network
{
    /// <summary>
    ///     Generic packet handler
    /// </summary>
    public class PacketHandler : IPacketHandler
    {
        private readonly IncommingMessageHandler _handler;

        public PacketHandler(uint opCode, IncommingMessageHandler handler)
        {
            OpCode = opCode;
            _handler = handler;
        }

        public uint OpCode { get; }

        public void Handle(IIncommingMessage message)
        {
            _handler.Invoke(message);
        }
    }
}