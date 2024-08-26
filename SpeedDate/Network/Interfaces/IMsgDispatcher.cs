using SpeedDate.Network.LiteNetLib;

namespace SpeedDate.Network.Interfaces
{
    public interface IMsgDispatcher
    {
        void SendMessage(uint opCode);
        void SendMessage(uint opCode, ResponseCallback responseCallback);

        void SendMessage(uint opCode, ISerializablePacket packet);
        void SendMessage(uint opCode, ISerializablePacket packet, DeliveryMethod method);
        void SendMessage(uint opCode, ISerializablePacket packet, ResponseCallback responseCallback);

        void SendMessage(uint opCode, byte[] data);
        void SendMessage(uint opCode, byte[] data, DeliveryMethod method);
        void SendMessage(uint opCode, byte[] data, ResponseCallback responseCallback);
        void SendMessage(uint opCode, string data);
        void SendMessage(uint opCode, string data, DeliveryMethod method);
        void SendMessage(uint opCode, string data, ResponseCallback responseCallback);
        void SendMessage(uint opCode, int data);
        void SendMessage(uint opCode, int data, DeliveryMethod method);
        void SendMessage(uint opCode, int data, ResponseCallback responseCallback);
        void SendMessage(uint opCode, bool data);
        void SendMessage(uint opCode, bool data, DeliveryMethod method);
        void SendMessage(uint opCode, bool data, ResponseCallback responseCallback);

        void SendMessage(IMessage message, DeliveryMethod method);
    }
}
