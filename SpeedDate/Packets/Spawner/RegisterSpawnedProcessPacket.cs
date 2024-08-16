using SpeedDate.Network;
using SpeedDate.Network.Utils.IO;
using System;

namespace SpeedDate.Packets.Spawner
{
    public class RegisterSpawnedProcessPacket : SerializablePacket
    {
        public int SpawnId;
        public string SpawnCode;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Console.WriteLine($"AAAAAA:========{(writer == null)},{SpawnId == null},{SpawnCode == null}");
            writer.Write(SpawnId);
            writer.Write(SpawnCode);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnId = reader.ReadInt32();
            SpawnCode = reader.ReadString();
        }
    }
}