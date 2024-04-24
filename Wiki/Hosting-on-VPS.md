To run SpeedDate on Linux, .NET Core SDK has to be installed:

1. Follow these [instructions](https://www.microsoft.com/net/download/linux-package-manager/ubuntu18-04/sdk-current)
1. To disable Microsoft spyware (optional): `export DOTNET_CLI_TELEMETRY_OPTOUT=1`

Copy & run the server:
1. Copy the directory `SpeedDate.Server.Console` to the server (for example with WinSCP)
1. Open a remote shell and `cd` to the directory
1. Enter `dotnet SpeedDate.Server.Console.dll`
1. Congratulations, you are now running the fully-featured SpeedDate Masterserver on your VPS