﻿using System.Collections.Generic;
using System.Linq;
using SpeedDate.Configuration;
using SpeedDate.Interfaces;
using SpeedDate.Logging;
using SpeedDate.Network;
using SpeedDate.Network.Interfaces;
using SpeedDate.Packets.Chat;
using SpeedDate.Plugin.Interfaces;
using SpeedDate.Server;
using SpeedDate.ServerPlugins.Authentication;

namespace SpeedDate.ServerPlugins.Chat
{
    class ChatPlugin : SpeedDateServerPlugin
    {
        public bool SetFirstChannelAsLocal = true;

        public bool SetLastChannelAsLocal = true;

        public bool AllowUsernamePicking = true;

        public int MinChannelNameLength = 2;
        public int MaxChannelNameLength = 25;
        
        [Inject] private ILogger logger;
        [Inject] private AuthPlugin auth;

        public readonly Dictionary<string, ChatUserExtension> ChatUsers = new Dictionary<string, ChatUserExtension>();
        public readonly Dictionary<string, ChatChannel> Channels = new Dictionary<string, ChatChannel>();

        public override void Loaded()
        {
            auth.LoggedIn += OnUserLoggedIn;
            auth.LoggedOut += OnUserLoggedOut;

            // Set handlers
            Server.SetHandler((uint)OpCodes.PickUsername, HandlePickUsername);
            Server.SetHandler((uint)OpCodes.JoinChannel, HandleJoinChannel);
            Server.SetHandler((uint)OpCodes.LeaveChannel, HandleLeaveChannel);
            Server.SetHandler((uint)OpCodes.GetCurrentChannels, HandeGetCurrentChannels);
            Server.SetHandler((uint)OpCodes.ChatMessage, HandleSendChatMessage);
            Server.SetHandler((uint)OpCodes.GetUsersInChannel, HandleGetUsersInChannel);
            Server.SetHandler((uint)OpCodes.SetDefaultChannel, HandleSetDefaultChannel);
        }

        protected virtual bool AddChatUser(ChatUserExtension user)
        {
            // Add the new user
            ChatUsers[user.Username.ToLower()] = user;

            user.Peer.Disconnected += OnClientDisconnected;

            return true;
        }

        protected virtual void RemoveChatUser(ChatUserExtension user)
        {
            ChatUsers.Remove(user.Username.ToLower());

            var channels = user.CurrentChannels.ToList();

            foreach (var chatChannel in channels)
            {
                chatChannel.RemoveUser(user);
            }

            user.Peer.Disconnected -= OnClientDisconnected;
        }

        protected virtual ChatUserExtension CreateChatUser(IPeer peer, string username)
        {
            return new ChatUserExtension(peer, username);
        }

        protected virtual void OnUserLoggedIn(UserExtension account)
        {
            var chatUser = CreateChatUser(account.Peer, account.Username);

            AddChatUser(chatUser);

            // Add the extension
            account.Peer.AddExtension(chatUser);
        }

        protected virtual void OnUserLoggedOut(UserExtension account)
        {
            var chatExt = account.Peer.GetExtension<ChatUserExtension>();

            if (chatExt != null)
                RemoveChatUser(chatExt);
        }

        protected virtual void OnClientDisconnected(IPeer peer)
        {
            peer.Disconnected -= OnClientDisconnected;

            var chatExt = peer.GetExtension<ChatUserExtension>();

            if (chatExt != null)
            {
                RemoveChatUser(chatExt);
            }
        }

        public virtual ChatChannel GetOrCreateChannel(string channelName)
        {
            return GetOrCreateChannel(channelName, false);
        }

        /// <summary>
        /// Retrieves an existing channel or creates a new one.
        /// If <see cref="ignoreForbidden"/> value is set to false,
        /// before creating a channel, a check will be executed to make sure that
        /// no forbidden words are used in the name
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="ignoreForbidden"></param>
        /// <returns></returns>
        protected virtual ChatChannel GetOrCreateChannel(string channelName, bool ignoreForbidden)
        {
            var lowercaseName = channelName.ToLower();

            if (!Channels.TryGetValue(lowercaseName, out var channel))
            {
                if (channelName.Length < MinChannelNameLength)
                    return null;

                if (channelName.Length > MaxChannelNameLength)
                    return null;

                // There's no such channel, but we might be able to create one
//                if (!ignoreForbidden && ForbiddenWordsInChNames.Any(w => !string.IsNullOrEmpty(w) && channelName.Contains(w.ToLower())))
//                {
//                    // Channel contains a forbidden word
//                    return null;
//                }

                channel = new ChatChannel(channelName);
                Channels.Add(lowercaseName, channel);
            }

            return channel;
        }

        /// <summary>
        /// Removes existing chat user from all the channels, and creates a new 
        /// <see cref="ChatUserExtension"/> with new username. If <see cref="joinSameChannels"/> is true, 
        /// user will be added to same channels
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="newUsername"></param>
        /// <param name="joinSameChannels"></param>
        public void ChangeUsername(IPeer peer, string newUsername, bool joinSameChannels = true)
        {
            var chatExt = peer.GetExtension<ChatUserExtension>();

            if (chatExt == null)
                return;

            var prevChannels = chatExt.CurrentChannels.ToList();
            var defaultChannel = chatExt.DefaultChannel;

            // Remove the user from chat
            RemoveChatUser(chatExt);

            // Create a new chat user
            var newExtension = CreateChatUser(peer, newUsername);
            peer.AddExtension(newExtension);

            if (joinSameChannels)
            {
                foreach (var prevChannel in prevChannels)
                {
                    var channel = GetOrCreateChannel(prevChannel.Name);
                    channel?.AddUser(newExtension);
                }

                if (defaultChannel != null && defaultChannel.Users.Contains(newExtension))
                {
                    // If we were added to the chat, which is now set as our default chat
                    // It's safe to set the default channel
                    newExtension.DefaultChannel = defaultChannel;
                }
            }
        }

        /// <summary>
        /// Handles chat message.
        /// Returns true, if message was handled
        /// If it returns false, message sender will receive a "Not Handled" response.
        /// </summary>
        protected virtual bool OnChatMessageReceived(ChatMessagePacket message, 
            ChatUserExtension sender, IIncommingMessage rawMessage)
        {
            // Set a true sender
            message.Sender = sender.Username;

            switch (message.Type)
            {
                case ChatMessagePacket.ChannelMessage:

                    if (string.IsNullOrEmpty(message.Receiver))
                    {
                        // If this is a local chat message (no receiver is provided)
                        if (sender.DefaultChannel == null)
                        {
                            rawMessage.Respond("No channel is set to be your local channel", ResponseStatus.Failed);
                            return true;
                        }
                        
                        sender.DefaultChannel.BroadcastMessage(message);
                        rawMessage.Respond(ResponseStatus.Success);
                        return true;
                    }

                    // Find the channel
                    Channels.TryGetValue(message.Receiver.ToLower(), out var channel);

                    if (channel == null || !sender.CurrentChannels.Contains(channel))
                    {
                        // Not in this channel
                        rawMessage.Respond($"You're not in the '{message.Receiver}' channel",
                            ResponseStatus.Failed);
                        return true;
                    }

                    channel.BroadcastMessage(message);

                    rawMessage.Respond(ResponseStatus.Success);
                    return true;

                case ChatMessagePacket.PrivateMessage:
                    ChatUsers.TryGetValue(message.Receiver.ToLower(), out var receiver);

                    if (receiver == null)
                    {
                        rawMessage.Respond($"User '{message.Receiver}' is not online", ResponseStatus.Failed);
                        return true;
                    }

                    receiver.Peer.SendMessage((uint)OpCodes.ChatMessage, message);
                    rawMessage.Respond(ResponseStatus.Success);
                    return true;
            }

            return false;
        }

        #region Message Handlers

        protected virtual void HandlePickUsername(IIncommingMessage message)
        {
            if (!AllowUsernamePicking)
            {
                message.Respond("Username picking is disabled", ResponseStatus.Failed);
                return;
            }

            var username = message.AsString();

            if (username.Replace(" ", "") != username)
            {
                message.Respond("Username cannot contain whitespaces", ResponseStatus.Failed);
                return;
            }

            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser != null)
            {
                message.Respond("You're already identified as: " + chatUser);
                return;
            }

            if (ChatUsers.ContainsKey(username.ToLower()))
            {
                message.Respond("There's already a user who has the same username", ResponseStatus.Failed);
                return;
            }

            chatUser = new ChatUserExtension(message.Peer, username);

            if (!AddChatUser(chatUser))
            {
                message.Respond("Failed to add user to chat", ResponseStatus.Failed);
                return;
            }

            // Add the extension
            message.Peer.AddExtension(chatUser);

            message.Respond(ResponseStatus.Success);
        }

        protected virtual void HandleJoinChannel(IIncommingMessage message)
        {
            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser == null)
            {
                message.Respond("Chat cannot identify you", ResponseStatus.Unauthorized);
                return;
            }

            var channelName = message.AsString();

            var channel = GetOrCreateChannel(channelName);

            if (channel == null)
            {
                // There's no such channel
                message.Respond("This channel is forbidden", ResponseStatus.Failed);
                return;
            }

            if (!channel.AddUser(chatUser))
            {
                message.Respond("Failed to join a channel", ResponseStatus.Failed);
                return;
            }

            if (SetFirstChannelAsLocal && chatUser.CurrentChannels.Count == 1)
                chatUser.DefaultChannel = channel;

            message.Respond(ResponseStatus.Success);
        }

        protected virtual void HandleLeaveChannel(IIncommingMessage message)
        {
            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser == null)
            {
                message.Respond("Chat cannot identify you", ResponseStatus.Unauthorized);
                return;
            }

            var channelName = message.AsString().ToLower();

            Channels.TryGetValue(channelName, out var channel);

            if (channel == null)
            {
                message.Respond("This channel does not exist", ResponseStatus.Failed);
                return;
            }

            channel.RemoveUser(chatUser);

            if (SetLastChannelAsLocal && chatUser.CurrentChannels.Count == 1)
            {
                chatUser.DefaultChannel = chatUser.CurrentChannels.First();
            }

            message.Respond(ResponseStatus.Success);
        }

        protected virtual void HandleSetDefaultChannel(IIncommingMessage message)
        {
            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser == null)
            {
                message.Respond("Chat cannot identify you", ResponseStatus.Unauthorized);
                return;
            }

            var channelName = message.AsString();

            var channel = GetOrCreateChannel(channelName);

            if (channel == null)
            {
                // There's no such channel
                message.Respond("This channel is forbidden", ResponseStatus.Failed);
                return;
            }

            // Add user to channel
            channel.AddUser(chatUser);

            // Set the property of default chat channel
            chatUser.DefaultChannel = channel;

            // Respond with a "success" status
            message.Respond(ResponseStatus.Success);
        }

        protected virtual void HandleGetUsersInChannel(IIncommingMessage message)
        {
            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser == null)
            {
                message.Respond("Chat cannot identify you", ResponseStatus.Unauthorized);
                return;
            }

            var channelName = message.AsString();
            var channel = GetOrCreateChannel(channelName);

            if (channel == null)
            {
                // There's no such channel
                message.Respond("This channel is forbidden", ResponseStatus.Failed);
                return;
            }

            var users = channel.Users.Select(u => u.Username);

            message.Respond(users.ToBytes(), ResponseStatus.Success);
        }

        protected virtual void HandleSendChatMessage(IIncommingMessage message)
        {
            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser == null)
            {
                message.Respond("Chat cannot identify you", ResponseStatus.Unauthorized);
                return;
            }

            var packet = message.Deserialize<ChatMessagePacket>();

            if (!OnChatMessageReceived(packet, chatUser, message))
            {
                // If message was not handled
                message.Respond("Invalid message", ResponseStatus.NotHandled);
                return;
            }
        }

        protected virtual void HandeGetCurrentChannels(IIncommingMessage message)
        {
            var chatUser = message.Peer.GetExtension<ChatUserExtension>();

            if (chatUser == null)
            {
                message.Respond("Chat cannot identify you", ResponseStatus.Unauthorized);
                return;
            }

            var channels = chatUser.CurrentChannels.Select(c => c.Name);

            message.Respond(channels.ToBytes(), ResponseStatus.Success);
        }

        #endregion
    }
}
