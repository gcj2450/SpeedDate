1. Clone this repository to your computer
1. Open "SpeedDate.sln" in Visual Studio
1. Set "SpeedDate.Server.Console" as your startup-project
1. Hit F5
1. Congratulations, you are now running the fully-featured SpeedDate Masterserver

The EchoPlugin provides a simple examples which echoes and incoming string back to the client. A simple usage of the EchoPlugin can be found in the class "TestEcho".

## Instantiate a server with configuration-file

    var server = new SpeedDateServer();
    server.Started += () => { /* server ready, do something */}; 
    server.Start(new FileConfigProvider("ServerConfig.xml")); 


## Instantiate a client with configuration-file

    var client = new SpeedDateClient();
    client.Started += () => { /* client connected, do something */}; 
    client.Start(new FileConfigProvider("ClientConfig.xml"));
 


## Custom configuration

To make a plugin configurable through Xml-Configuration, add a new class that holds the configurable variables and let it implement **IConfig**-interface:

    class MyServerPluginConfig : IConfig
    {
        public bool SettingWhatever { get; set; }
    }

Add a field to the plugin and apply the [Inject]-attribute to it:

    [Inject] private MyServerPluginConfig _config;

To configure the plugin, add the corresponding tag inside the *.xml-configuration:

    <MyServerPluginConfig SettingWhatever="false"/>

You can now use _config in your plugin everywhere (except in the constructor):

    public override void Loaded()
    {
        var configValue = _config.SettingWhatever;
    }

You can also configure your plugins from within the code, see "SpeedDate.Client.Spawner.Console" for an example.