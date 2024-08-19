using System;
using SpeedDate;

namespace ConsoleGameServer.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            //这里发过来的参数是没有前缀的，下面的解析可能出错了，规律是最后一个为SpawnCode, 所以下面的解析用^1
            //====Starting game with arguments: 127.0.0.1, 60125, 60125, -1, 0, -1, 10000, 127.0.0.1, 7f080d...
            Console.WriteLine($"=====Starting game with arguments: {string.Join(", ", args)}...");

            var server = new GameServer();
            server.ConnectedToMaster += () =>
            {
                Console.WriteLine($"Connected to Master,SpawnCode: {CommandLineArgs.SpawnCode} ,SpawnId: {CommandLineArgs.SpawnId}");
                if (CommandLineArgs.SpawnCode == null)
                {
                    Console.WriteLine($"Connected to Master,SpawnCode==null get from last");
                    CommandLineArgs.SpawnCode = args[^1];
                }
                else
                {
                    Console.WriteLine($"Connected to Master,SpawnCode not null");
                }
                if (CommandLineArgs.SpawnId < 0)
                {
                    CommandLineArgs.SpawnId = 0;
                }
                server.Rooms.RegisterSpawnedProcess(
                    CommandLineArgs.SpawnId,
                    CommandLineArgs.SpawnCode,
                    (controller) =>
                    {
                        Console.WriteLine("Registered to Master");
                    }, Console.WriteLine);
            };

            server.Start("GameServerConfig.xml");

            Console.ReadLine();
        }
    }
}
