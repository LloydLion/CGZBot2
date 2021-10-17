using CGZBot2.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	class CustomHelpHandler : BaseCommandModule
	{
		private readonly DiscordEmbedBuilder builder = new();
		private readonly Dictionary<string, DiscordEmbedBuilder> cdescBuilders = new();


		public CustomHelpHandler()
		{
			builder.WithTitle("Список комманд");
			builder.WithColor(DiscordColor.Chartreuse);
			builder.AddField("Обозначения", "`command` - публичная комманда" +
				"\r\n__`command`__ - комманда с условиями (см описание)" +
				"\r\n***`command`*** - комманда для администрации");
			builder.WithFooter("версия бота " + Assembly.GetEntryAssembly().GetName().Version.ToString());

			var cn = Program.Client.GetCommandsNext();

			var data = new Dictionary<string, StringBuilder>();
			foreach (var command in cn.RegisteredCommands.Values)
			{
				var caterogy = (command.Module.ModuleType.GetCustomAttributes(false)
					.SingleOrDefault(s => typeof(DescriptionAttribute) == s.GetType()) as DescriptionAttribute)?.Description ?? "Нет группы";

				var limits = (command.CustomAttributes.SingleOrDefault(s => s.GetType() ==
					typeof(HelpUseLimitsAttribute)) as HelpUseLimitsAttribute)?.Limit ?? CommandUseLimit.Public;

				if (!data.ContainsKey(caterogy)) data.Add(caterogy, new StringBuilder());

				var builder = data[caterogy];

				switch (limits)
				{
					case CommandUseLimit.Public:
						builder.Append($"`{command.Name}` ");
						break;
					case CommandUseLimit.Private:
						builder.Append($"__`{command.Name}`__ ");
						break;
					case CommandUseLimit.Admins:
						builder.Append($"***`{command.Name}`*** ");
						break;
				}

				//--------------

				var cdesk = new DiscordEmbedBuilder();
				if (cdescBuilders.TryAdd(command.Name, cdesk) == false) continue;

				cdesk.WithTitle("Описание для комманды " + command.Name);
				cdesk.WithColor(DiscordColor.Chartreuse);
				cdesk.WithDescription(command.Description);
				if(command.Overloads.Count == 1)
					cdesk.AddField("Использование", createUsageString(command, command.Overloads[0]) + "\r\n" + createArgumenetsString(command, command.Overloads[0]));
				else
				{
					foreach (var overload in command.Overloads)
					{
						cdesk.AddField("Вариант использования", createUsageString(command, overload) + "\r\n" + createArgumenetsString(command, overload));
					}
				}


				static string createUsageString(Command command, CommandOverload overload)
				{
					return $"`{command.Name}` " + string.Join(" ", overload.Arguments.Select(s => s.IsOptional || s.IsCatchAll ? $"`[{s.Name}]`" : $"`({s.Name})`"));
				}

				static string createArgumenetsString(Command command, CommandOverload overload)
				{
					return string.Join("\r\n", overload.Arguments.Select(s => $"`{s.Name}`(" + (s.IsCatchAll ? "Array of " + s.Type.Name : s.Type.Name) + $") - {s.Description ?? "Нет описания"}"));
				}
			}

			foreach (var pair in data) builder.AddField(pair.Key, pair.Value.ToString());
		}


		[Command("help")]
		public Task Help(CommandContext ctx)
		{
			ctx.RespondAsync(builder).TryDeleteAfter(20000);
			return Task.CompletedTask;
		}

		[Command("help")]
		public Task Help(CommandContext ctx, string cname)
		{
			if (cdescBuilders.ContainsKey(cname) == false) ctx.RespondAsync("Такой комманды нет").TryDeleteAfter(8000);
			else ctx.RespondAsync(cdescBuilders[cname]).TryDeleteAfter(20000);
			return Task.CompletedTask;
		}
	}
}
