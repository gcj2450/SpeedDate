﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpeedDate.Network.Utils.Conversion;
using SpeedDate.Network.Utils.IO;

namespace SpeedDate.Packets
{
    public abstract class ObservableBaseDictionary<TKey, TValue> : ObservableBase
    {
        private const int SetOperation = 0;
        private const int RemoveOperation = 1;
        private readonly Queue<UpdateEntry> _updates;

        public ObservableBaseDictionary(short key, Dictionary<TKey, TValue> defaultValues = null) : base(key)
        {
            _updates = new Queue<UpdateEntry>();

            UnderlyingDictionary = defaultValues == null ? new Dictionary<TKey, TValue>() :
                defaultValues.ToDictionary(k => k.Key, k => k.Value);
        }

        /// <summary>
        /// Returns an immutable list of values
        /// </summary>
        public IEnumerable<TValue> Values => UnderlyingDictionary.Values;

        /// <summary>
        /// Returns an immutable list of key-value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TValue>> Pairs => UnderlyingDictionary.ToList();

        /// <summary>
        /// Returns a mutable dictionary
        /// </summary>
        public Dictionary<TKey, TValue> UnderlyingDictionary { get; }

        public void SetValue(TKey key, TValue value)
        {
            if (value == null)
            {
                Remove(key);
                return;
            }

            if (UnderlyingDictionary.ContainsKey(key))
            {
                UnderlyingDictionary[key] = value;
            }
            else
            {
                UnderlyingDictionary.Add(key, value);
            }

            MarkDirty();
            _updates.Enqueue(new UpdateEntry()
            {
                Key = key,
                Operation = SetOperation,
                Value = value
            });
        }

        public void Remove(TKey key)
        {
            UnderlyingDictionary.Remove(key);

            MarkDirty();
            _updates.Enqueue(new UpdateEntry()
            {
                Key = key,
                Operation = RemoveOperation,
            });
        }

        public TValue GetValue(TKey key)
        {
            UnderlyingDictionary.TryGetValue(key, out var result);
            return result;
        }

        protected abstract void WriteValue(TValue value, EndianBinaryWriter writer);

        protected abstract TValue ReadValue(EndianBinaryReader reader);

        protected abstract void WriteKey(TKey key, EndianBinaryWriter writer);

        protected abstract TKey ReadKey(EndianBinaryReader reader);

        public override byte[] ToBytes()
        {
            byte[] b;
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.Write(UnderlyingDictionary.Count);

                    foreach (var item in UnderlyingDictionary)
                    {
                        WriteKey(item.Key, writer);
                        WriteValue(item.Value, writer);
                    }
                }

                b = ms.ToArray();
            }
            return b;
        }

        public override void FromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        var key = ReadKey(reader);
                        var value = ReadValue(reader);
                        if (UnderlyingDictionary.ContainsKey(key))
                        {
                            UnderlyingDictionary[key] = value;
                        }
                        else
                        {
                            UnderlyingDictionary.Add(key, value);
                        }
                    }
                }
            }
        }

        public override byte[] GetUpdates()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.Write(_updates.Count);

                    foreach (var update in _updates)
                    {
                        writer.Write(update.Operation);
                        WriteKey(update.Key, writer);

                        if (update.Operation != RemoveOperation)
                        {
                            WriteValue(update.Value, writer);
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        public override void ApplyUpdates(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big,ms))
                {
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        var operation = reader.ReadByte();
                        var key = ReadKey(reader);
                        
                        if (operation == RemoveOperation)
                        {
                            
                            UnderlyingDictionary.Remove(key);
                            continue;
                        }

                        var value = ReadValue(reader);
                        if (UnderlyingDictionary.ContainsKey(key))
                        {
                            UnderlyingDictionary[key] = value;
                        }
                        else
                        {
                            UnderlyingDictionary.Add(key, value);
                        }
                    }
                }
            }
            MarkDirty();
        }

        public override void ClearUpdates()
        {
            _updates.Clear();
        }

        private struct UpdateEntry
        {
            public byte Operation;
            public TKey Key;
            public TValue Value;
        }
    }
}