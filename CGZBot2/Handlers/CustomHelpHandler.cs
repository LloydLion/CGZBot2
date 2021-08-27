using CGZBot2.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	class CustomHelpHandler : BaseCommandModule
	{
		private readonly DiscordEmbedBuilder builder = new();


		public CustomHelpHandler()
		{
			builder.WithTitle("Список комманд");
			builder.WithColor(DiscordColor.Chartreuse);
			builder.AddField("Обозначения", "`command` - публичная комманда" +
				"\r\n__`command`__ - комманда с условиями (см описание)" +
				"\r\n***`command`*** - комманда для администрации");
			builder.WithFooter("версия бота 2.0.0.0-dev");

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
			}

			foreach (var pair in data) builder.AddField(pair.Key, pair.Value.ToString());
		}


		[Command("help")]
		public Task Help(CommandContext ctx)
		{
			ctx.RespondAsync(builder).TryDeleteAfter(20000);
			return Task.CompletedTask;
		}
	}
}
