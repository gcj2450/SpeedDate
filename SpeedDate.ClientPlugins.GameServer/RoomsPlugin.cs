﻿using System;
using System.Collections.Generic;
using SpeedDate.Network;
using SpeedDate.Network.Interfaces;
using SpeedDate.Packets.Rooms;
using SpeedDate.Packets.Spawner;

namespace SpeedDate.ClientPlugins.GameServer
{
    public delegate void RoomCreationCallback(RoomController controller);
    public delegate void RoomAccessValidateCallback(UsernameAndPeerIdPacket usernameAndPeerId);

    public delegate void RegisterSpawnedProcessCallback(SpawnTaskController taskController);
    public delegate void CompleteSpawnedProcessCallback();

    public sealed class RoomsPlugin : SpeedDateClientPlugin
    {
        private readonly Dictionary<int, RoomController> _localCreatedRooms = new Dictionary<int, RoomController>();

        /// <summary>
        /// Maximum time the master server can wait for a response from game server
        /// to see if it can give access to a peer
        /// </summary>
        public float AccessProviderTimeout = 3;

        /// <summary>
        /// Event, invoked when a room is registered
        /// </summary>
        public event Action<RoomController> RoomRegistered; 

        /// <summary>
        /// Event, invoked when a room is destroyed
        /// </summary>
        public event Action<RoomController> RoomDestroyed;

        public override void Loaded()
        { 
        }

        /// <summary>
        /// Sends a request to register a room to master server
        /// </summary>
        public void RegisterRoom(RoomOptions options, RoomCreationCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint) OpCodes.RegisterRoom, options, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    // Failed to register room
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                    return;
                }

                var roomId = response.AsInt();

                var controller = new RoomController(this, roomId, Client, options);

                // Save the reference
                _localCreatedRooms[roomId] = controller;

                callback.Invoke(controller);

                // Invoke event
                RoomRegistered?.Invoke(controller);
            });
        }

        /// <summary>
        /// Sends a request to destroy a room of a given room id
        /// </summary>
        public void DestroyRoom(int roomId, SuccessCallback callback, ErrorCallback errorCallback)
        {
            DestroyRoom(roomId, callback, errorCallback, Client);
        }

        /// <summary>
        /// Sends a request to destroy a room of a given room id
        /// </summary>
        public void DestroyRoom(int roomId, SuccessCallback successCallback, ErrorCallback errorCallback, IClient client)
        {
            if (!client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            client.SendMessage((uint)OpCodes.DestroyRoom, roomId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                    return;
                }

                _localCreatedRooms.TryGetValue(roomId, out var destroyedRoom);
                _localCreatedRooms.Remove(roomId);
                
                successCallback.Invoke();

                // Invoke event
                if (destroyedRoom != null)
                    RoomDestroyed?.Invoke(destroyedRoom);
            });
        }

        /// <summary>
        /// Sends a request to master server, to see if a given token is valid
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="token"></param>
        /// <param name="callback"></param>
        /// <param name="errorCallback"></param>
        public void ValidateAccess(int roomId, string token, RoomAccessValidateCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            var packet = new RoomAccessValidatePacket
            {
                RoomId = roomId,
                Token = token
            };

            Client.SendMessage((uint)OpCodes.ValidateRoomAccess, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                    return;
                }

                callback.Invoke(response.Deserialize<UsernameAndPeerIdPacket>());
            });
        }

        /// <summary>
        /// Updates the options of the registered room
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="options"></param>
        /// <param name="successCallback"></param>
        public void SaveOptions(int roomId, RoomOptions options, SuccessCallback successCallback, ErrorCallback errorCallback)
        {
            SaveOptions(roomId, options, successCallback, errorCallback, Client);
        }

        /// <summary>
        /// Updates the options of the registered room
        /// </summary>
        public void SaveOptions(int roomId, RoomOptions options, SuccessCallback successCallback, ErrorCallback errorCallback, IClient client)
        {
            if (!client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            var changePacket = new SaveRoomOptionsPacket
            {
                Options = options,
                RoomId =  roomId
            };

            client.SendMessage((uint) OpCodes.SaveRoomOptions, changePacket, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                    return;
                }

                successCallback.Invoke();
            });
        }

        /// <summary>
        /// Notifies master server that a user with a given peer id has left the room
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void NotifyPlayerLeft(int roomId, int peerId, SuccessCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("NotConnected");
                return;
            }

            var packet = new PlayerLeftRoomPacket
            {
                PeerId = peerId,
                RoomId = roomId
            };

            Client.SendMessage((uint) OpCodes.PlayerLeftRoom, packet, (status, response) =>
            {
                if (status == ResponseStatus.Success)
                {
                    callback.Invoke();
                }
                else
                {
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                }
            });
        }

        /// <summary>
        /// Get's a room controller (of a registered room, which was registered in current process)
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        public RoomController GetRoomController(int roomId)
        {
            _localCreatedRooms.TryGetValue(roomId, out var controller);
            return controller;
        }

        /// <summary>
        /// Retrieves all of the locally created rooms (their controllers)
        /// </summary>
        /// <returns></returns>
        public IEnumerable<RoomController> GetLocallyCreatedRooms()
        {
            return _localCreatedRooms.Values;
        }

        /// <summary>
        /// This should be called from a process which is spawned.
        /// For example, it can be called from a game server, which is started by the spawner
        /// On successfull registration, callback contains <see cref="SpawnTaskController"/>, which 
        /// has a dictionary of properties, that were given when requesting a process to be spawned
        /// </summary>
        public void RegisterSpawnedProcess(int spawnId, string spawnCode, RegisterSpawnedProcessCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            var packet = new RegisterSpawnedProcessPacket()
            {
                SpawnCode = spawnCode,
                SpawnId = spawnId
            };

            Client.SendMessage((uint)OpCodes.RegisterSpawnedProcess, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                    return;
                }

                var properties = new Dictionary<string, string>().FromBytes(response.AsBytes());

                var process = new SpawnTaskController(this, spawnId, properties);

                callback.Invoke(process);
            });
        }

        /// <summary>
        /// This method should be called, when spawn process is finalized (finished spawning).
        /// For example, when spawned game server fully starts
        /// </summary>
        public void FinalizeSpawnedProcess(int spawnId, CompleteSpawnedProcessCallback callback, ErrorCallback errorCallback, Dictionary<string, string> finalizationData = null)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            var packet = new SpawnFinalizationPacket
            {
                SpawnId = spawnId,
                FinalizationData = finalizationData ?? new Dictionary<string, string>()
            };

            Client.SendMessage((uint)OpCodes.CompleteSpawnProcess, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown Error"));
                    return;
                }

                callback.Invoke();
            });
        }
    }
}
