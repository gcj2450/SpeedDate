﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SpeedDate.Network;
using SpeedDate.Network.Utils.Conversion;
using SpeedDate.Network.Utils.IO;
using SpeedDate.Packets;

namespace SpeedDate.ClientPlugins.GameServer
{
    public class ProfilesPlugin : SpeedDateClientPlugin
    {
        /// <summary>
        /// Time, after which game server will try sending profile 
        /// updates to master server
        /// </summary>
        public float ProfileUpdatesInterval = 0.1f;

        private readonly Dictionary<string, ObservableServerProfile> _profiles;

        private readonly HashSet<ObservableServerProfile> _modifiedProfiles;

        private Task _updateTask;

        public ProfilesPlugin()
        {
            _profiles = new Dictionary<string, ObservableServerProfile>();
            _modifiedProfiles = new HashSet<ObservableServerProfile>();
        }

        /// <summary>
        /// Sends a request to server, retrieves all profile values, and applies them to a provided
        /// profile
        /// </summary>
        public void FillProfileValues(ObservableServerProfile profile, SuccessCallback successCallback, ErrorCallback errorCallback)
        {
            if (!Client.IsConnected)
            {
                errorCallback.Invoke("Not connected");
                return;
            }

            Client.SendMessage((uint) OpCodes.ServerProfileRequest, profile.Username, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    errorCallback.Invoke(response.AsString("Unknown error"));
                    return;
                }

                // Use the bytes received, to replicate the profile
                profile.FromBytes(response.AsBytes());

                profile.ClearUpdates();

                _profiles[profile.Username] = profile;

                profile.ModifiedInServer += serverProfile =>
                {
                    OnProfileModified(profile);
                };

                profile.Disposed += OnProfileDisposed;

                successCallback.Invoke();
            });
        }

        private void OnProfileModified(ObservableServerProfile profile)
        {
            _modifiedProfiles.Add(profile);

            if (_updateTask != null)
                return;

            _updateTask = KeepSendingUpdates();
            
        }

        private void OnProfileDisposed(ObservableServerProfile profile)
        {
            profile.Disposed -= OnProfileDisposed;

            _profiles.Remove(profile.Username);
        }

        private async Task KeepSendingUpdates()
        {
            await Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(ProfileUpdatesInterval));

                    if (_modifiedProfiles.Count == 0)
                        continue;

                    using (var ms = new MemoryStream())
                    using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                    {
                        // Write profiles count
                        writer.Write(_modifiedProfiles.Count);

                        foreach (var profile in _modifiedProfiles)
                        {
                            // Write username
                            writer.Write(profile.Username);

                            var updates = profile.GetUpdates();

                            // Write updates length
                            writer.Write(updates.Length);

                            // Write updates
                            writer.Write(updates);

                            profile.ClearUpdates();
                        }

                        Client.SendMessage((uint)OpCodes.UpdateServerProfile, ms.ToArray());
                    }

                    _modifiedProfiles.Clear();
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
