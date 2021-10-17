using CGZBot2.Handlers;
using CGZBot2.Processors;
using CGZBot2.Processors.Discord;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using LloydLion.Serialization.Common;
using LloydLion.Serialization.Json;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2
{
	class Program
	{
		public static DiscordClient Client { get; private set; }

		public static bool Connected { get; private set; }


		static void Main(string[] args)
		{
			var json = new JsonSerializator();
			SerializatorProvider.AddSerializator(json);
			SerializatorProvider.AddDeserializator(json);

			foreach (var type in Assembly.GetExecutingAssembly().DefinedTypes
				.Where(s => typeof(ITypeSerializationProcessor).IsAssignableFrom(s)))
			{
				SerializatorProvider.AddProcessor(
					 (ITypeSerializationProcessor)type.GetConstructor(Array.Empty<Type>())
					 .Invoke(Array.Empty<object>()));
			}

			SerializatorProvider.SetDefaultFormat("json");


			Client = new DiscordClient(new DiscordConfiguration()
			{
				AutoReconnect = true,
				TokenType = TokenType.Bot,
				Token = File.ReadAllText("token.ini"),
			});

			Client.UseCommandsNext(new CommandsNextConfiguration()
			{
#if DEBUG
				StringPrefixes = new string[] { "\\" },
#else
				StringPrefixes = new string[] { "/" },
#endif
				EnableDefaultHelp = false,
			});

			Client.UseInteractivity(new InteractivityConfiguration()
			{
				
			});

			var types = Assembly.GetExecutingAssembly().DefinedTypes.Where(s => typeof(BaseCommandModule).IsAssignableFrom(s) && s != typeof(CustomHelpHandler));
			foreach (var type in types)	Client.GetCommandsNext().RegisterCommands(type);
			Client.GetCommandsNext().RegisterCommands(typeof(CustomHelpHandler)); //NEEEEEED register last

			Client.GetCommandsNext().CommandErrored += (sender, args) =>
			{
				Client.Logger.Log(LogLevel.Error, args.Exception, "Exception in command /{0}", args.Command?.Name ?? "--");
				args.Handled = true;
				return Task.CompletedTask;
			};

			Client.GetCommandsNext().CommandExecuted += (sender, args) =>
			{
				args?.Context?.Message?.TryDeleteAfter(20000);
				HandlerState.SaveAll();
				return Task.CompletedTask;
			};

			Client.ConnectAsync().Wait();

			Thread.Sleep(10000);
			Connected = true;

			Thread.Sleep(-1);
		}
	}
}
