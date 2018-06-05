﻿using System.Collections.Generic;
using SpeedDate.Logging;
using SpeedDate.Networking.Utils.IO;

namespace SpeedDate.Packets
{
    public class ObservableDictStringInt : ObservableBaseDictionary<string, int>
    {
        public ObservableDictStringInt(short key) 
            : base(key)
        {
        }

        public ObservableDictStringInt(short key, Dictionary<string, int> defaultValues) 
            : base(key, defaultValues)
        {
        }

        public override string SerializeToString()
        {
            var obj = new JSONObject();

            foreach (var pair in UnderlyingDictionary)
            {
                obj.AddField(pair.Key, pair.Value);
            }

            return obj.ToString();
        }

        public override void DeserializeFromString(string value)
        {
            var parsed = new JSONObject(value);
            var keys = parsed.keys;
            var list = parsed.list;

            if (keys == null)
                return;

            for (var i = 0; i < keys.Count; i++)
            {
                UnderlyingDictionary[keys[i]] = (int)list[i].i;
            }
        }

        protected override string ReadKey(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override int ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void WriteKey(string key, EndianBinaryWriter writer)
        {
            writer.Write(key);
        }

        protected override void WriteValue(int value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}