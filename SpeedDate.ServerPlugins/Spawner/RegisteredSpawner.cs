﻿using System.Collections.Generic;
using System.Linq;
using SpeedDate.Logging;
using SpeedDate.Network;
using SpeedDate.Network.Interfaces;
using SpeedDate.Network.LiteNetLib;
using SpeedDate.Packets.Spawner;

namespace SpeedDate.ServerPlugins.Spawner
{
    public class RegisteredSpawner
    {
        public delegate void KillRequestCallback(bool isKilled);

        private const int MaxConcurrentRequests = 8;

        public int SpawnerId { get; }
        public IPeer Peer { get; }
        public SpawnerOptions Options { get; }

        private readonly Queue<SpawnTask> _queue;

        public int ProcessesRunning { get; private set; }

        private readonly HashSet<SpawnTask> _beingSpawned;

        public RegisteredSpawner(int spawnerId, IPeer peer, SpawnerOptions options)
        {
            SpawnerId = spawnerId;
            Peer = peer;
            Options = options;

            _queue = new Queue<SpawnTask>();
            _beingSpawned = new HashSet<SpawnTask>();
        }

        public int CalculateFreeSlotsCount()
        {
            return Options.MaxProcesses - _queue.Count - ProcessesRunning;
        }

        public bool CanSpawnAnotherProcess()
        {
            return Options.MaxProcesses == 0 || _queue.Count + ProcessesRunning < Options.MaxProcesses;
        }

        public void AddTaskToQueue(SpawnTask task)
        {
            _beingSpawned.Add(task);

            _queue.Enqueue(task);
        }

        public void UpdateQueue()
        {
            // Ignore if there's no connection with the peer
            if (Peer.ConnectionState != ConnectionState.Connected)
                return;

            // Ignore if nothing's in the queue
            if (_queue.Count == 0)
                return;

            if (_beingSpawned.Count >= MaxConcurrentRequests)
            {
                // If we're currently at the maximum available concurrent spawn count
                var finishedSpawns = _beingSpawned.Where(s => s.IsDoneStartingProcess);

                // Remove finished spawns
                foreach (var finishedSpawn in finishedSpawns)
                    _beingSpawned.Remove(finishedSpawn);
            }

            // If we're still at the maximum concurrent requests
            if (_beingSpawned.Count >= MaxConcurrentRequests)
                return;

            var task = _queue.Dequeue();

            var data = new SpawnRequestPacket
            {
                SpawnerId = SpawnerId,
                CustomArgs = task.CustomArgs,
                Properties = task.Properties,
                SpawnId = task.SpawnId,
                SpawnCode = task.UniqueCode
            };

            Peer.SendMessage((uint)OpCodes.SpawnRequest, data, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    task.Kill();
                    Logs.Error("Spawn request was not handled. Status: " + status + " | " + response.AsString("Unknown Error"));
                }
            });
        }

        public void SendKillRequest(int spawnId, KillRequestCallback callback)
        {
            var packet = new KillSpawnedProcessPacket
            {
                SpawnerId = SpawnerId,
                SpawnId = spawnId
            };

            Peer.SendMessage((uint) OpCodes.KillSpawnedProcess, packet, (status, response) =>
            {
                callback.Invoke(status == ResponseStatus.Success);
            });
        }

        public void UpdateProcessesCount(int value)
        {
            ProcessesRunning = value;
        }

        public void OnProcessKilled()
        {
            ProcessesRunning -= 1;
        }

        public void OnProcessStarted()
        {
            ProcessesRunning += 1;
        }
    }
}