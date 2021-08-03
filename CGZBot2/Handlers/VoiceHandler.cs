using CGZBot2.Entities;
using DSharpPlus;
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
	class VoiceHandler : BaseCommandModule
	{
		private static readonly GuildSettings<DiscordChannel> voiceCreationCategory;


		private readonly List<CreatedVoiceChannel> createdVoices = new();


		static VoiceHandler()
		{
			voiceCreationCategory =
				BotSettings.Load<DiscordChannel>(typeof(VoiceHandler), "test");
		}


		[Command("create")]
		[Description("Создает голосовой канал.\r\n\t" +
			"Вы будете администратором своего канала\r\n\t" +
			"Он будет авто-удалён после выхода всех участников")]
		public async Task CreateVoice(CommandContext ctx,
			[Description("Имя создаваемого канала")] string name)
		{
			DiscordChannel category = voiceCreationCategory[ctx];
			var channel = new CreatedVoiceChannel(await ctx.Guild.CreateChannelAsync(name, ChannelType.Voice, category));

			createdVoices.Add(channel);
			await Task.Delay(10000);
			channel.DeleteTask.Start();
		}
	}
}
