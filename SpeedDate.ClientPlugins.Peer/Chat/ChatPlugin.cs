﻿using System.Collections.Generic;
using SpeedDate.Network;
using SpeedDate.Network.Interfaces;
using SpeedDate.Packets.Chat;
using SpeedDate.Plugin.Interfaces;

namespace SpeedDate.ClientPlugins.Peer.Chat
{
    public class ChatPlugin : SpeedDateClientPlugin
    {
        public delegate void ChatChannelsCallback(List<string> channels);
        public delegate void ChatUsersCallback(List<string> users);

        public delegate void ChatUserHandler(string channel, string user);
        public delegate void ChatMessageHandler(ChatMessagePacket message);

        /// <summary>
        /// Invoked, when user leaves a channel
        /// </summary>
        public event ChatUserHandler UserLeftChannel;

        /// <summary>
        /// Invoked, when user joins a channel
        /// </summary>
        public event ChatUserHandler UserJoinedChannel;

        /// <summary>
        /// Invoked, when a new message is received
        /// </summary>
        public event ChatMessageHandler MessageReceived;


        public override void Loaded()
        {
            Client.SetHandler((uint)OpCodes.UserJoinedChannel, HandleUserJoinedChannel);
            Client.SetHandler((uint)OpCodes.UserLeftChannel, HandleUserLeftChannel);
            Client.SetHandler((uint)OpCodes.ChatMessage, HandleChatMessage);
        }

        /// <summary>
        /// Sends a request to set chat username
        /// </summary>
        public void PickUsername(string username, SuccessCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint)OpCodes.PickUsername, username, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke();
            });
        }

        /// <summary>
        /// Sends a request to join a specified channel
        /// </summary>
        public void JoinChannel(string channel, SuccessCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint) OpCodes.JoinChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke();
            });
        }


        /// <summary>
        /// Sends a request to leave a specified channel
        /// </summary>
        public void LeaveChannel(string channel, SuccessCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint)OpCodes.LeaveChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke();
            });
        }

        /// <summary>
        /// Sets a default channel to the specified channel.
        /// Messages, that have no channel, will be sent to default channel
        /// </summary>
        public void SetDefaultChannel(string channel, SuccessCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint)OpCodes.SetDefaultChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke();
            });
        }

        /// <summary>
        /// Retrieves a list of channels, which user has joined
        /// </summary>
        public void GetJoinedChannels(ChatChannelsCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint)OpCodes.GetCurrentChannels, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                var list = new List<string>().FromBytes(response.AsBytes());

                callback.Invoke(list);
            });
        }
        
        /// <summary>
        /// Retrieves a list of users in a channel
        /// </summary>
        public void GetUsersInChannel(string channel, ChatUsersCallback callback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint)OpCodes.GetUsersInChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                var list = new List<string>().FromBytes(response.AsBytes());

                callback.Invoke(list);
            });
        }

        /// <summary>
        /// Sends a message to default channel
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="errorCallback"></param>
        public void SendToDefaultChannel(string message, SuccessCallback callback, ErrorCallback errorCallback)
        {
            SendMessage(new ChatMessagePacket()
            {
                Receiver = "",
                Message = message,
                Type = ChatMessagePacket.ChannelMessage
            }, callback, errorCallback);
        }

        /// <summary>
        /// Sends a message to specified channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="errorCallback"></param>
        public void SendChannelMessage(string channel, string message, SuccessCallback callback, ErrorCallback errorCallback)
        {
            SendMessage(new ChatMessagePacket()
            {
                Receiver = channel,
                Message = message,
                Type = ChatMessagePacket.ChannelMessage
            }, callback, errorCallback);
        }

        /// <summary>
        /// Sends a private message to specified user
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        /// <param name="errorCallback"></param>
        public void SendPrivateMessage(string receiver, string message, SuccessCallback callback, ErrorCallback errorCallback)
        {
            SendMessage(new ChatMessagePacket()
            {
                Receiver = receiver,
                Message = message,
                Type = ChatMessagePacket.PrivateMessage
            }, callback, errorCallback);
        }

        /// <summary>
        /// Sends a generic message packet to server
        /// </summary>
        public void SendMessage(ChatMessagePacket packet, SuccessCallback callback, ErrorCallback errorCallback)
        {
            Client.SendMessage((uint)OpCodes.ChatMessage, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke();
            });
        }

        #region Message handlers

        private void HandleChatMessage(IIncommingMessage message)
        {
            var packet = message.Deserialize<ChatMessagePacket>();

            MessageReceived?.Invoke(packet);
        }

        private void HandleUserLeftChannel(IIncommingMessage message)
        {
            var data = new List<string>().FromBytes(message.AsBytes());
            UserLeftChannel?.Invoke(data[0], data[1]);
        }

        private void HandleUserJoinedChannel(IIncommingMessage message)
        {
            var data = new List<string>().FromBytes(message.AsBytes());
            UserJoinedChannel?.Invoke(data[0], data[1]);
        }

        #endregion
    }
}
