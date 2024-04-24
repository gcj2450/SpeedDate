# Overview

To be able to start a client or server, the directory should at least contain the following assemblies:

## Server

* SpeedDate.dll
* SpeedDate.Server.dll
* SpeedDate.ServerPlugins.dll
* SpeedDate.Server.Console.dll (dotnet executable)

## Client

* SpeedDate.dll
* SpeedDate.Client.dll
* SpeedDate.ClientPlugins.dll
* SpeedDate.Client.Console.Example.dll (dotnet executable, replace this with your game-client)

## 1. Build the MasterServer

Right-click on project "**SpeedDate.Server.Console**" and select "**Publish...**", then click the **Publish**-button

![Publish Server](https://i.imgur.com/qT6zIO8.png)

![Publish button](https://i.imgur.com/ADTC7J1.png)

## 2. Build the Spawner

Right-click on project "**SpeedDate.Client.Spawner.Console**" and select "**Publish...**", then click the **Publish**-button

## 3. Build the Gameserver-binary

Repeat the last step for project "**ConsoleGameServer.Example**", this can be replaced by your own Gameserver later on

Adjust the path to the game-executable inside "**SpawnerConfig.xml**"

![ExecutablePath](https://i.imgur.com/IE7YaHl.png)

## 4. Run the MasterServer

Start a new **PowerShell**, cd to the repositories root folder. Step 1 should've created a new directory named "**Deploy\Server**". Start the server with the **dotnet**-command:

![Path](https://i.imgur.com/bdYFRFY.png)

![StartServer](https://i.imgur.com/Atd2opU.png)

## 5. Start the Spawner

![StartSpawner](https://i.imgur.com/C0XCR7m.png)

## 6. Start the Client

Inside Visual Studio, set "**SpeedDate.Client.Console.Example**" as StartUp Project & hit **run**:

![Fin](https://i.imgur.com/JwwPBtL.png)

The Client connects to the Master and starts a spawn-request. The Spawner will then spawn a new GameServer.