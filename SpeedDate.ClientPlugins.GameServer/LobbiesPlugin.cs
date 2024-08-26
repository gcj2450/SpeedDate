﻿using SpeedDate.Network;
using SpeedDate.Packets.Common;
using SpeedDate.Packets.Lobbies;

namespace SpeedDate.ClientPlugins.GameServer
{
    public class LobbiesPlugin : SpeedDateClientPlugin
    {
        public delegate void LobbyMemberDataCallback(LobbyMemberData memberData);

        public delegate void LobbyInfoCallback(LobbyDataPacket info);
        
        /// <summary>
        /// Retrieves lobby member data of user, who has connected to master server with
        /// a specified peerId
        /// </summary>
        public void GetMemberData(int lobbyId, int peerId, LobbyMemberDataCallback callback, ErrorCallback errorCallback)
        {
            var packet = new IntPairPacket
            {
                A = lobbyId,
                B = peerId
            };

            Client.SendMessage((uint)OpCodes.GetLobbyMemberData, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                var memberData = response.Deserialize<LobbyMemberData>();
                callback.Invoke(memberData);
            });
        }
        
        /// <summary>
        /// Retrieves information about the lobby
        /// </summary>
        public void GetLobbyInfo(int lobbyId, LobbyInfoCallback callback, ErrorCallback errorCallback)
        {
            Client.SendMessage((uint)OpCodes.GetLobbyInfo, lobbyId, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                var memberData = response.Deserialize<LobbyDataPacket>();
                callback.Invoke(memberData);
            });
        }
    }
}
