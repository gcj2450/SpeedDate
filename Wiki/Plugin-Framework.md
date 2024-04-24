# Preface
SpeedDate looks for every class that implements the interface **IPlugin** in every dll-file that is inside the executing directory. That means: if you make a separate solution for your plugins, you have to copy the resulting dll-file into the SpeedDate-directory.

## Writing a custom Plugin

Plugins extend the functionality of the server. For every server-plugin there should exist a corresponding client-plugin to access the server-functionality. **It is good practice to place server- and client-plugin-classes into separate projects.**

## Create a Server-Plugin

Create a new class that inherits from **SpeedDateServerPlugin**:

    class ExampleServerPlugin : SpeedDateServerPlugin
    {
    }

## Create a Client-Plugin

Create a new class that inherits from **SpeedDateClientPlugin**:

    class ExampleClientPlugin: SpeedDateClientPlugin
    {
    }

## Overwrite "Loaded"-method

ServerPlugins have access to the field **Server** and ClientPlugins have access to the field **Client** to register message-handlers or subscribe to their events. Any fields that are injected by the [Inject]-attribute will not be initialized before **Loaded** was called, so do **NOT** use them in the constructor. **It is good practice to avoid any constructor-logic for plugins.**

Example for a ServerPlugin:

    public override void Loaded()
    {
        Server.PeerConnected += ServerOnPeerConnected;
    }

### Reference to other plugins

If the plugin requires to call methods from other plugins (for example: AuthenticationPlugin requires methods from the DatabasePlugin), you can simply add the field with the [Inject]-Attribute:

    [Inject] private readonly AuthPlugin _auth;

## Additional dependencies

If, for example, the plugin should write log-entries, simply add the following line to the plugin and SpeedDate will handle the rest for you:

    [Inject] private ILogger logger;

Please note that properties are injected **after** the constructor was called.