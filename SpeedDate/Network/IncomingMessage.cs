﻿using System;
using System.Collections.Generic;
using System.Text;
using SpeedDate.Network.Interfaces;
using SpeedDate.Network.LiteNetLib;
using SpeedDate.Network.Utils.Conversion;

namespace SpeedDate.Network
{
    /// <summary>
    ///     Default implementation of incomming message
    /// </summary>
    public class IncommingMessage : IIncommingMessage
    {
        private readonly byte[] _data;

        public IncommingMessage(uint opCode, byte flags, byte[] data, DeliveryMethod deliveryMethod, IPeer peer)
        {
            OpCode = opCode;
            Peer = peer;
            _data = data;
        
        }

        public IncommingMessage(OpCodes opCode, byte flags, byte[] data, DeliveryMethod deliveryMethod, IPeer peer)
        {
            OpCode = (uint) opCode;
            Peer = peer;
            _data = data;

        }

        /// <summary>
        ///     Message flags
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        ///     Operation code (message type)
        /// </summary>
        public uint OpCode { get; private set; }

        /// <summary>
        ///     Sender
        /// </summary>
        public IPeer Peer { get; private set; }

        /// <summary>
        ///     Ack id the message is responding to
        /// </summary>
        public int? AckResponseId { get; set; }

        /// <summary>
        ///     We add this to a packet to so that receiver knows
        ///     what he responds to
        /// </summary>
        public int? AckRequestId { get; set; }

        /// <summary>
        ///     Returns true, if sender expects a response to this message
        /// </summary>
        public bool IsExpectingResponse
        {
            get { return AckResponseId.HasValue; }
        }

        /// <summary>
        ///     For ordering
        /// </summary>
        public int SequenceChannel { get; set; }

        /// <summary>
        ///     Message status code
        /// </summary>
        public ResponseStatus Status { get; set; }

        /// <summary>
        ///     Respond with a message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="statusCode"></param>
        public void Respond(IMessage message, ResponseStatus statusCode = ResponseStatus.Default)
        {
            message.Status = statusCode;

            if (AckResponseId.HasValue)
                message.AckResponseId = AckResponseId.Value;

            Peer.SendMessage(message, DeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        ///     Respond with data (message is created internally)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="statusCode"></param>
        public void Respond(byte[] data, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, data), statusCode);
        }

        /// <summary>
        ///     Respond with data (message is created internally)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="statusCode"></param>
        public void Respond(ISerializablePacket packet, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, packet.ToBytes()), statusCode);
        }


        /// <summary>
        ///     Respond with empty message and status code
        /// </summary>
        /// <param name="statusCode"></param>
        public void Respond(ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode), statusCode);
        }

        public void Respond(string message, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(message.ToBytes(), statusCode);
        }

        public void Respond(int response, ResponseStatus statusCode = ResponseStatus.Default)
        {
            Respond(MessageHelper.Create(OpCode, response), statusCode);
        }

        /// <summary>
        ///     Returns true if message contains any data
        /// </summary>
        public bool HasData => _data.Length > 0;

        /// <summary>
        ///     Returns contents of this message. Mutable
        /// </summary>
        /// <returns></returns>
        public byte[] AsBytes()
        {
            return _data;
        }

        /// <summary>
        ///     Decodes content into a string
        /// </summary>
        /// <returns></returns>
        public string AsString()
        {
            return Encoding.UTF8.GetString(_data);
        }

        /// <summary>
        ///     Decodes content into a string. If there's no content,
        ///     returns the <see cref="defaultValue"/>
        /// </summary>
        /// <returns></returns>
        public string AsString(string defaultValue)
        {
            return HasData ? AsString() : defaultValue;
        }

        /// <summary>
        ///     Decodes content into an integer
        /// </summary>
        /// <returns></returns>
        public int AsInt()
        {
            return EndianBitConverter.Big.ToInt32(_data, 0);
        }

        /// <summary>
        ///     Writes content of the message into a packet
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packetToBeFilled"></param>
        /// <returns></returns>
        public T Deserialize<T>() where T : ISerializablePacket, new()
        {
            var packetToBeFilled = new T();
            return MessageHelper.Deserialize(_data, packetToBeFilled);
        }

        /// <summary>
        ///     Uses content of the message to regenerate list of packets
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packetCreator"></param>
        /// <returns></returns>
        public IEnumerable<T> DeserializeList<T>() where T : ISerializablePacket, new()
        {
            return MessageHelper.DeserializeList<T>(_data);
        }

        public override string ToString()
        {
            return AsString(base.ToString());
        }
    }
}