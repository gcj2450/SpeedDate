Welcome to the SpeedDate wiki! This solution consists of the following projects:

### Common
* **SpeedDate** - Common functionality for Server and Client, like IoC-kernel and configuration-manager.

### Server
* SpeedDate.**Server** - Handles peer-connections and dispatches messages to the server-plugins
* SpeedDate.Server.**ServerPlugins** - All serverplugins, e.g.: Auth, Profiles, Matchmaking, Lobbies...
* SpeedDate.Server.**Console** - The actual Server-Console-Application
* SpeedDate.**Database**.*CockroachDb* - A concrete implementation of 'IDbAccess' to access a CockroachDb-Database. Acts as a resource for the servers' Database-Plugin

### Client
* SpeedDate.**Client** - Connects to the server and dispatches messages to the client-plugins
* SpeedDate.Client.**ClientPlugins** - Common types for client-plugins
* SpeedDate.Client.ClientPlugins.**Peer** - All client-plugins for a peer. Your game-client should make use of these
* SpeedDate.Client.ClientPlugins.**GameServer** - All plugins for the game-server, e.g. to register itself to the master
* SpeedDate.Client.ClientPlugins.**Spawner** - All plugins for the spawner
* SpeedDate.Client.Spawner.**Console** - The actual Spawner-Console-Application

### Examples
* SpeedDate.Client.Console.**Example** - Example game-client
* **ConsoleGameServer.Example** - Example game-server
