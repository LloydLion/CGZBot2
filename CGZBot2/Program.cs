using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using LloydLion.Serialization.Common;
using LloydLion.Serialization.Json;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2
{
	class Program
	{
		public static DiscordClient Client { get; private set; }


		static void Main(string[] args)
		{
			var json = new JsonSerializator();
			SerializatorProvider.AddSerializator(json);
			SerializatorProvider.AddDeserializator(json);

			SerializatorProvider.SetDefaultFormat("json");


			Client = new DiscordClient(new DiscordConfiguration()
			{
				AutoReconnect = true,
				TokenType = TokenType.Bot,
				Token = File.ReadAllText("token.ini"),
			});

			Client.UseCommandsNext(new CommandsNextConfiguration()
			{
				StringPrefixes = new string[] { "/" },
			});

			Client.UseInteractivity(new InteractivityConfiguration()
			{
				
			});

			Client.GetCommandsNext().RegisterCommands(Assembly.GetExecutingAssembly());
			Client.GetCommandsNext().CommandErrored += (sender, args) => 
			{
				Client.Logger.Log(LogLevel.Error, args.Exception, "Exception in command {0}", args.Command.Name);
				args.Handled = true;
				return Task.CompletedTask;
			};

			Client.ConnectAsync();
			Thread.Sleep(-1);
		}
	}
}
