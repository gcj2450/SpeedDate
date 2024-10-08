﻿using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using NUnit.Framework;
using Shouldly;
using SpeedDate.Client;
using SpeedDate.ClientPlugins.GameServer;
using SpeedDate.ClientPlugins.Peer.Auth;
using SpeedDate.ClientPlugins.Peer.Room;
using SpeedDate.ClientPlugins.Peer.SpawnRequest;
using SpeedDate.ClientPlugins.Spawner;
using SpeedDate.Configuration;
using SpeedDate.Network;
using SpeedDate.Network.Interfaces;
using SpeedDate.Packets.Rooms;
using SpeedDate.Packets.Spawner;

namespace SpeedDate.Test
{
    [TestFixture]
    public class TestSpawner
    {
        ///-------------------------------------------------------------------------------------------------
        /// <summary>
        ///     (Unit Test Method) This test resembles the complete workflow: begins with registering a spawner, requesting a spawn,
        ///     starting up a gameserver and granting access for the client who requested the spawn
        /// </summary>
        ///
        /// <exception cref="Exception">    Thrown when an error occured. </exception>
        ///-------------------------------------------------------------------------------------------------
        [Test]
        public void ShouldRegisterRoomBeforeFinalizingSpawnTask_AndThen_ShouldReceiveAccessToRoomAsClient()
        {
            var done = new AutoResetEvent(false);

            var spawnId = -1;
            var spawnCode = string.Empty;
            var spawnerRegionName = TestContext.CurrentContext.Test.Name;

            //------------------------------------------------------
            // 
            // Test-Setup
            // 
            // -----------------------------------------------------

            //Fakes spawning a process after receiving a SpawnRequest
            var spawnerDelegateMock = new Mock<ISpawnerRequestsDelegate>();
            spawnerDelegateMock.Setup(mock => mock.HandleSpawnRequest(
                    It.IsAny<IIncommingMessage>(),
                    It.Is<SpawnRequestPacket>(packet =>
                        packet.SpawnId >= 0 && !string.IsNullOrEmpty(packet.SpawnCode))))
                .Callback((IIncommingMessage message, SpawnRequestPacket data) =>
                {
                    //By default, the spawn-data is passed via commandline-arguments
                    spawnId = data.SpawnId;
                    spawnCode = data.SpawnCode;

                    message.Respond(ResponseStatus.Success);
                    message.Peer.SendMessage((uint) OpCodes.ProcessStarted, data.SpawnId);
                });

            //------------------------------------------------------
            // 
            // On Spawner:
            // 
            // Register itself
            // 
            // -----------------------------------------------------
            var spawner = new SpeedDateClient();
            spawner.Started += () =>
            {
                spawner.GetPlugin<SpawnerPlugin>().SetSpawnerRequestsDelegate(spawnerDelegateMock.Object);
                spawner.GetPlugin<SpawnerPlugin>().Register(
                    callback: spawnerId =>
                    {
                        spawnerId.ShouldBeGreaterThanOrEqualTo(0);
                        done.Set();
                    },
                    errorCallback: error => throw new Exception(error));
            };

            spawner.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultSpawnerPlugins, //Load spawner-plugins only
                new IConfig[]
                {
                    new SpawnerConfig
                    {
                        Region = spawnerRegionName
                    }
                }));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The spawner has been registered to master

            //------------------------------------------------------
            // 
            // On Client:
            // 
            // Connect to the master and request to spawn a new process
            // 
            // -----------------------------------------------------
            var client = new SpeedDateClient();
            client.Started += () =>
            {
                client.GetPlugin<AuthPlugin>().LogInAsGuest(info =>
                {
                    client.GetPlugin<SpawnRequestPlugin>().RequestSpawn(new Dictionary<string, string>(),
                        spawnerRegionName,
                        controller =>
                        {
                            controller.ShouldNotBeNull();
                            controller.SpawnId.ShouldBeGreaterThanOrEqualTo(0);
                            controller.Status.ShouldBe(SpawnStatus.None);
                            controller.StatusChanged += status =>
                            {
                                switch (status)
                                {
                                    case SpawnStatus.WaitingForProcess:
                                    case SpawnStatus.Finalized:
                                        done.Set();
                                        break;
                                }
                            };
                            spawnId = controller.SpawnId;
                        }, error => throw new Exception(error));
                }, error => throw new Exception(error));
            };

            client.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultPeerPlugins)); //Load peer-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30))
                .ShouldBeTrue(); //The SpawnRequest has been handled and is now waiting for the process to start

            //------------------------------------------------------
            // 
            // On Gameserver (which - by default - is started by the spawner):
            // 
            // The spawned process now registers itself at the master, starts a new server and then creates a new room
            // 
            // -----------------------------------------------------

            var gameserver = new SpeedDateClient();
            gameserver.Started += () =>
            {
                //By default, the spawn-data is passed via commandline-arguments
                gameserver.GetPlugin<RoomsPlugin>().RegisterSpawnedProcess(spawnId, spawnCode,
                    controller =>
                    {
                        //By default, these values are passed via commandline-arguments
                        var roomOptions = new RoomOptions
                        {
                            RoomIp = "127.0.0.1",
                            RoomPort = 20000
                        };
                        gameserver.GetPlugin<RoomsPlugin>().RegisterRoom(roomOptions, roomController =>
                        {
                            controller.FinalizeTask(new Dictionary<string, string>
                            {
                                {OptionKeys.RoomId, roomController.RoomId.ToString()},
                                {OptionKeys.RoomPassword, roomController.Options.Password}
                            }, () =>
                            {
                                //StatusChanged => SpawnStatus.Finalized will signal done
                            });
                        }, error => throw new Exception(error));

                    }, error => throw new Exception(error));
            };

            gameserver.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultGameServerPlugins)); //Load gameserver-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30))
                .ShouldBeTrue(); //The SpawnRequest has been finalized ('done' is set by StatusChanged.Finalized (see above))

            //------------------------------------------------------
            // 
            // On Client:
            // 
            // Receive an access-token and connect to the gameserver
            // 
            // -----------------------------------------------------
            RoomAccessPacket roomAccess = null;
            client.GetPlugin<SpawnRequestPlugin>().GetRequestController(spawnId).ShouldNotBeNull();
            client.GetPlugin<SpawnRequestPlugin>().GetRequestController(spawnId).GetFinalizationData(data =>
                {
                    data.ShouldNotBeNull();
                    data.ShouldContainKey(OptionKeys.RoomId);
                    data.ShouldContainKey(OptionKeys.RoomPassword);

                    client.GetPlugin<RoomPlugin>().GetAccess(
                        roomId: Convert.ToInt32(data[OptionKeys.RoomId]),
                        password: data[OptionKeys.RoomPassword],
                        properties: new Dictionary<string, string>(),
                        callback: access =>
                        {
                            roomAccess = access;
                            done.Set();
                        },
                        errorCallback: error => throw new Exception(error));
                },
                error => throw new Exception(error));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //Client received RoomAccess

            //------------------------------------------------------
            // 
            // Now the client has to connect to the gameserver and transmit the token.
            // How this is done is out of SpeedDate's scope. You may use UNET, LiteNetLib, TcpListener, another SpeedDate server or any other custom solution.
            // 
            // This test simply uses the same access-object on the server and the client
            // 
            // -----------------------------------------------------

            gameserver.GetPlugin<RoomsPlugin>().ValidateAccess(roomAccess.RoomId, roomAccess.Token, id =>
            {
                id.PeerId.ShouldBe(client.PeerId);
                done.Set();
            }, error => throw new Exception(error));
            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //Gameserver validated access
        }

        [Test]
        public void RegisterSpawner_ShouldGenerateSpawnerId()
        {
            var done = new AutoResetEvent(false);

            var spawner = new SpeedDateClient();
            spawner.Started += () =>
            {
                spawner.GetPlugin<SpawnerPlugin>().Register(
                    spawnerId =>
                    {
                        spawnerId.ShouldBeGreaterThanOrEqualTo(0);
                        done.Set();
                    },
                    error => throw new Exception(error));
            };

            spawner.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultSpawnerPlugins)); //Load spawner-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue();
        }

        [Test]
        public void RequestSpawnWithInvalidSpawnerSettings_ShouldAbort()
        {
            //The default spawnerRequestdelegate would start a new process. Since the executable cannot be found (in this test-context), the request will fail
            //  and the spawn-task will be killed
            var spawnerRegionName = TestContext.CurrentContext.Test.Name;
            var done = new AutoResetEvent(false);

            //Register a spawner
            var spawner = new SpeedDateClient();
            spawner.Started += () =>
            {
                spawner.GetPlugin<SpawnerPlugin>().Register(
                    spawnerId =>
                    {
                        spawnerId.ShouldBeGreaterThanOrEqualTo(0);
                        done.Set();
                    },
                    error => throw new Exception(error));
            };

            spawner.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultSpawnerPlugins, //Load spawner-plugins only
                new IConfig[]
                {
                    new SpawnerConfig
                    {
                        Region = spawnerRegionName
                    }
                }));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //Spawner is registered

            var client = new SpeedDateClient();
            client.Started += () =>
            {
                client.GetPlugin<AuthPlugin>().LogInAsGuest(info =>
                {
                    client.GetPlugin<SpawnRequestPlugin>().RequestSpawn(new Dictionary<string, string>(),
                        spawnerRegionName,
                        controller =>
                        {
                            controller.ShouldNotBeNull();
                            controller.Status.ShouldBe(SpawnStatus.None);
                            controller.StatusChanged += status =>
                            {
                                if (status == SpawnStatus.Killed)
                                {
                                    done.Set();
                                }
                            };
                        }, error => throw new Exception(error));
                }, error => throw new Exception(error));
            };

            client.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultPeerPlugins)); //Load peer-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue();
        }

        [Test]
        public void SpawnRequestWithoutLogin_ShouldNotBeAuthorized()
        {
            var spawnerRegionName = TestContext.CurrentContext.Test.Name;
            var done = new AutoResetEvent(false);
            
            var client = new SpeedDateClient();
            client.Started += () =>
            {
                client.GetPlugin<SpawnRequestPlugin>().RequestSpawn(new Dictionary<string, string>(),
                    spawnerRegionName,
                    controller => throw new Exception("Should not be authorized"), error => done.Set());
            };

            client.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultPeerPlugins)); //Load peer-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue();
        }

        [Test]
        public void ShouldRegisterSpawnedProcess()
        {
            var done = new AutoResetEvent(false);

            var spawnId = -1;
            var spawnCode = string.Empty;
            var spawnerRegionName = TestContext.CurrentContext.Test.Name;

            //Fakes spawning a process after receiving a SpawnRequest
            var spawnerDelegateMock = new Mock<ISpawnerRequestsDelegate>();
            spawnerDelegateMock.Setup(mock => mock.HandleSpawnRequest(
                    It.IsAny<IIncommingMessage>(),
                    It.Is<SpawnRequestPacket>(packet =>
                        packet.SpawnId >= 0 && !string.IsNullOrEmpty(packet.SpawnCode))))
                .Callback((IIncommingMessage message, SpawnRequestPacket data) =>
                {
                    //By default, the spawn-data is passed via commandline-arguments
                    spawnId = data.SpawnId;
                    spawnCode = data.SpawnCode;

                    message.Respond(ResponseStatus.Success);
                    message.Peer.SendMessage((uint) OpCodes.ProcessStarted, data.SpawnId);
                });

            //Register a spawner
            var spawner = new SpeedDateClient();
            spawner.Started += () =>
            {
                spawner.GetPlugin<SpawnerPlugin>().SetSpawnerRequestsDelegate(spawnerDelegateMock.Object);
                spawner.GetPlugin<SpawnerPlugin>().Register(
                    spawnerId =>
                    {
                        spawnerId.ShouldBeGreaterThanOrEqualTo(0);
                        done.Set();
                    },
                    error => throw new Exception(error));
            };

            spawner.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultSpawnerPlugins, //Load spawner-plugins only
                new IConfig[]
                {
                    new SpawnerConfig
                    {
                        Region = spawnerRegionName
                    }
                }));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The spawner has been registered to master

            var client = new SpeedDateClient();
            client.Started += () =>
            {
                client.GetPlugin<AuthPlugin>().LogInAsGuest(info =>
                {
                    client.GetPlugin<SpawnRequestPlugin>().RequestSpawn(
                        new Dictionary<string, string>(), spawnerRegionName,
                        controller =>
                        {
                            controller.ShouldNotBeNull();
                            controller.SpawnId.ShouldBeGreaterThanOrEqualTo(0);
                            controller.Status.ShouldBe(SpawnStatus.None);
                            controller.StatusChanged += status =>
                            {
                                switch (status)
                                {
                                    case SpawnStatus.WaitingForProcess:
                                    case SpawnStatus.ProcessRegistered:
                                    case SpawnStatus.Finalized:
                                        done.Set();
                                        break;
                                }
                            };

                            spawnId = controller.SpawnId;
                        }, error => throw new Exception(error));
                }, error => throw new Exception(error));
            };

            client.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultPeerPlugins)); //Load peer-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30))
                .ShouldBeTrue(); //The SpawnRequest has been handled and is now waiting for the process to start

            //Start the gameserver - by default this is done by the spawner-handler
            var gameserver = new SpeedDateClient();
            gameserver.Started += () =>
            {
                //By default, the spawn-data is passed via commandline-arguments
                gameserver.GetPlugin<RoomsPlugin>().RegisterSpawnedProcess(spawnId, spawnCode, controller =>
                {
                    //StatusChanged => SpawnStatus.ProcessRegistered will signal done
                    controller.FinalizeTask(new Dictionary<string, string>(), () =>
                    {
                        //StatusChanged => SpawnStatus.Finalized will signal done
                    });
                }, error => throw new Exception(error));
            };

            gameserver.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultGameServerPlugins)); //Load gameserver-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The Process has been registered

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The SpawnRequest has been finalized
        }

        [Test]
        public void ShouldPassFinalizationData()
        {
            var done = new AutoResetEvent(false);

            var spawnId = -1;
            var spawnCode = string.Empty;
            var spawnerRegionName = TestContext.CurrentContext.Test.Name;
            var testData = new KeyValuePair<string, string>("Hello", "World");

            //Fakes spawning a process after receiving a SpawnRequest
            var spawnerDelegateMock = new Mock<ISpawnerRequestsDelegate>();
            spawnerDelegateMock.Setup(mock => mock.HandleSpawnRequest(
                    It.IsAny<IIncommingMessage>(),
                    It.Is<SpawnRequestPacket>(packet =>
                        packet.SpawnId >= 0 && !string.IsNullOrEmpty(packet.SpawnCode))))
                .Callback((IIncommingMessage message, SpawnRequestPacket data) =>
                {
                    //By default, the spawn-data is passed via commandline-arguments
                    spawnId = data.SpawnId;
                    spawnCode = data.SpawnCode;

                    message.Respond(ResponseStatus.Success);
                    message.Peer.SendMessage((uint) OpCodes.ProcessStarted, data.SpawnId);
                });

            //Register a spawner
            var spawner = new SpeedDateClient();
            spawner.Started += () =>
            {
                spawner.GetPlugin<SpawnerPlugin>().SetSpawnerRequestsDelegate(spawnerDelegateMock.Object);
                spawner.GetPlugin<SpawnerPlugin>().Register(
                    callback: spawnerId =>
                    {
                        spawnerId.ShouldBeGreaterThanOrEqualTo(0);
                        done.Set();
                    },
                    errorCallback: error => throw new Exception(error));
            };

            spawner.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultSpawnerPlugins, //Load spawner-plugins only
                new IConfig[]
                {
                    new SpawnerConfig
                    {
                        Region = spawnerRegionName
                    }
                }));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The spawner has been registered to master

            var client = new SpeedDateClient();
            client.Started += () =>
            {
                client.GetPlugin<AuthPlugin>().LogInAsGuest(info =>
                {
                    client.GetPlugin<SpawnRequestPlugin>().RequestSpawn(new Dictionary<string, string>(),
                        spawnerRegionName,
                        controller =>
                        {
                            controller.ShouldNotBeNull();
                            controller.SpawnId.ShouldBeGreaterThanOrEqualTo(0);
                            controller.Status.ShouldBe(SpawnStatus.None);
                            controller.StatusChanged += status =>
                            {
                                switch (status)
                                {
                                    case SpawnStatus.WaitingForProcess:
                                    case SpawnStatus.ProcessRegistered:
                                    case SpawnStatus.Finalized:
                                        done.Set();
                                        break;
                                }
                            };

                            spawnId = controller.SpawnId;
                        }, error => throw new Exception(error));
                }, error => throw new Exception(error));
            };

            client.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultPeerPlugins)); //Load peer-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30))
                .ShouldBeTrue(); //The SpawnRequest has been handled and is now waiting for the process to start

            //Start the gameserver - by default this is done by the spawner-handler
            var gameserver = new SpeedDateClient();
            gameserver.Started += () =>
            {
                //By default, the spawn-data is passed via commandline-arguments
                gameserver.GetPlugin<RoomsPlugin>().RegisterSpawnedProcess(spawnId, spawnCode, controller =>
                {
                    //StatusChanged => SpawnStatus.ProcessRegistered will signal done
                    controller.FinalizeTask(new Dictionary<string, string> {{testData.Key, testData.Value}}, () =>
                    {
                        //StatusChanged => SpawnStatus.Finalized will signal done
                    });
                }, error => throw new Exception(error));
            };

            gameserver.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultGameServerPlugins)); //Load gameserver-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The Process has been registered

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The SpawnRequest has been finalized

            client.GetPlugin<SpawnRequestPlugin>().GetRequestController(spawnId).ShouldNotBeNull();
            client.GetPlugin<SpawnRequestPlugin>().GetRequestController(spawnId).GetFinalizationData(data =>
                {
                    data.ShouldNotBeNull();
                    data.ShouldContainKeyAndValue(testData.Key, testData.Value);
                    done.Set();
                },
                error => throw new Exception(error));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //FinalizationData was correct
        }

        [Test]
        public void RegisterRandomSpawnedProcess_ShouldError()
        {
            var done = new AutoResetEvent(false);

            //Start the gameserver - by default this is done by the spawner-handler
            var evilClient = new SpeedDateClient();
            evilClient.Started += () =>
            {
                //By default, the spawn-data is passed via commandline-arguments
                evilClient.GetPlugin<RoomsPlugin>().RegisterSpawnedProcess(
                    spawnId: Util.CreateRandomInt(0, 100),
                    spawnCode: Util.CreateRandomString(10),
                    callback: controller => { },
                    errorCallback: error => { done.Set(); }
                );
            };

            evilClient.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultGameServerPlugins)); //Load gameserver-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //Error occured
        }

        [Test]
        public void WrongPeerWithCorrectSpawnIdRegisterSpawnedProcess_ShouldNotBeAuthorized()
        {
            var done = new AutoResetEvent(false);

            var spawnId = -1;
            var spawnerRegionName = TestContext.CurrentContext.Test.Name;

            //Fakes spawning a process after receiving a SpawnRequest
            var spawnerDelegateMock = new Mock<ISpawnerRequestsDelegate>();
            spawnerDelegateMock.Setup(mock => mock.HandleSpawnRequest(
                    It.IsAny<IIncommingMessage>(),
                    It.Is<SpawnRequestPacket>(packet =>
                        packet.SpawnId >= 0 && !string.IsNullOrEmpty(packet.SpawnCode))))
                .Callback((IIncommingMessage message, SpawnRequestPacket data) =>
                {
                    //By default, the spawn-data is passed via commandline-arguments
                    spawnId = data.SpawnId;
                    message.Respond(ResponseStatus.Success);
                    message.Peer.SendMessage((uint) OpCodes.ProcessStarted, data.SpawnId);
                });

            //Register a spawner
            var spawner = new SpeedDateClient();
            spawner.Started += () =>
            {
                spawner.GetPlugin<SpawnerPlugin>().SetSpawnerRequestsDelegate(spawnerDelegateMock.Object);
                spawner.GetPlugin<SpawnerPlugin>().Register(
                    spawnerId =>
                    {
                        spawnerId.ShouldBeGreaterThanOrEqualTo(0);
                        done.Set();
                    },
                    error => throw new Exception(error));
            };

            spawner.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultSpawnerPlugins, //Load spawner-plugins only
                new IConfig[]
                {
                    new SpawnerConfig
                    {
                        Region = spawnerRegionName
                    }
                }));

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //The spawner has been registered to master

            var client = new SpeedDateClient();
            client.Started += () =>
            {
                client.GetPlugin<AuthPlugin>().LogInAsGuest(info =>
                {
                    client.GetPlugin<SpawnRequestPlugin>().RequestSpawn(new Dictionary<string, string>(),
                        spawnerRegionName,
                        controller =>
                        {
                            controller.StatusChanged += status =>
                            {
                                switch (status)
                                {
                                    case SpawnStatus.WaitingForProcess:
                                    case SpawnStatus.ProcessRegistered:
                                        done.Set();
                                        break;
                                }
                            };

                            spawnId = controller.SpawnId;
                        }, error => throw new Exception(error));
                }, error => throw new Exception(error));
            };

            client.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultPeerPlugins)); //Load peer-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30))
                .ShouldBeTrue(); //The SpawnRequest has been handled and is now waiting for the process to start

            var evilClient = new SpeedDateClient();
            evilClient.Started += () =>
            {
                evilClient.GetPlugin<RoomsPlugin>()
                    .FinalizeSpawnedProcess(spawnId, () => { },
                        error => done.Set()); //Not authorized without registering with correct SpawnCode first
            };

            evilClient.Start(new DefaultConfigProvider(
                new NetworkConfig(SetUp.MasterServerIp, SetUp.MasterServerPort),
                PluginsConfig.DefaultGameServerPlugins)); //Load gameserver-plugins only

            done.WaitOne(TimeSpan.FromSeconds(30)).ShouldBeTrue(); //Finalize returned an error
        }
    }
}
