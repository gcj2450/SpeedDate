namespace SpeedDate.Network.Interfaces
{
    public interface IMessageHandlerProvider
    {
        void SetHandler(uint opCode, IncommingMessageHandler handler);
        void SetHandler(OpCodes opCode, IncommingMessageHandler handler);
    }
}