using CGZBot2.Entities;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	[Description("Голосовые каналы")]
	class VoiceHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<DiscordChannel> voiceCreationCategory =
			BotSettings.Load<DiscordChannel>(typeof(VoiceHandler), nameof(voiceCreationCategory));
		private static readonly GuildDictionary<DiscordChannel> voiceCreationReportChannel =
			BotSettings.Load<DiscordChannel>(typeof(VoiceHandler), nameof(voiceCreationReportChannel));


		private readonly GuildDictionary<List<CreatedVoiceChannel>> createdVoices =
			HandlerState.Get(typeof(VoiceHandler), nameof(createdVoices), (guild) => new List<CreatedVoiceChannel>());


		public static event Action<CreatedVoiceChannel> ChannelCreated;


		public VoiceHandler()
		{
			Program.Client.MessageDeleted += OnMessageDeleted;

			foreach (var d in createdVoices)
			{
				UpdateReports(d.Key);

				foreach (var ch in d.Value)
				{
					ch.Deleted += ChannelDeleted;
					ch.DeleteTask.Start();
				}
			}
		}


		[Command("voice")]
		[Description("Создает голосовой канал.\r\n\t" +
			"Вы будете администратором своего канала\r\n\t" +
			"Он будет авто-удалён после выхода всех участников")]
		public async Task CreateVoice(CommandContext ctx,
			[Description("Имя создаваемого канала")] params string[] name)
		{
			DiscordChannel category = voiceCreationCategory[ctx];

			var count = createdVoices[ctx].Where(s => s.Creator == ctx.Member).Count();
			if(count >= 2)
			{
				ctx.RespondAsync("Вы превысили лимит. Участник может создать до 2 каналов").TryDeleteAfter(8000);
				return;
			}

			var overs = new DiscordOverwriteBuilder[] { new DiscordOverwriteBuilder(ctx.Member).Allow(Permissions.All) };

			var channel = new CreatedVoiceChannel(creator: ctx.Member, channel:
				await ctx.Guild.CreateChannelAsync(name.JoinWords(), ChannelType.Voice, category, overwrites: overs));

			ChannelCreated?.Invoke(channel);

			createdVoices[ctx].Add(channel);
			UpdateReports(ctx.Guild);

			await Task.Delay(10000);

			channel.Deleted += ChannelDeleted;

			channel.DeleteTask.Start();
		}

		private void UpdateReports(DiscordGuild guild)
		{
			var channel = voiceCreationReportChannel[guild];

			var nonDel = createdVoices[guild].Select(s => s.ReportMessage).ToList();
			channel.DeleteMessagesAsync(channel.GetMessagesAsync(1000).Result.Where(s => !nonDel.Contains(s)));

			foreach (var voice in createdVoices[guild])
			{
				if (voice.ReportMessage != null) continue;

				var builder = new DiscordEmbedBuilder();

				builder
					.WithColor(DiscordColor.Azure)
					.WithTimestamp(voice.CreationDate)
					.WithAuthor(voice.Creator.DisplayName, iconUrl: voice.Creator.AvatarUrl)
					.WithTitle("Создан голосовой канал")
					.AddField("Имя", voice.Name, inline: true);

				voice.ReportMessage = channel.SendMessageAsync(builder.Build()).Result;
			}
		}

		private void ChannelDeleted(CreatedVoiceChannel channel)
		{
			createdVoices[channel.Channel.Guild].Remove(channel);
			UpdateReports(channel.Channel.Guild);
			HandlerState.Set(typeof(VoiceHandler), nameof(createdVoices), createdVoices);
		}

		//private Task OnVoiceChannelDeleted(DiscordClient _, ChannelDeleteEventArgs args)
		//{
		//	var voices = createdVoices[args.Guild].Where(s => s.Channel != null).ToDictionary(s => s.Channel);
		//	if (voices.ContainsKey(args.Channel))
		//	{
		//		voices[args.Channel].Close();
		//	}

		//	return Task.CompletedTask;
		//}

		private Task OnMessageDeleted(DiscordClient _, MessageDeleteEventArgs args)
		{
			var reports = createdVoices[args.Guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);
			if (reports.ContainsKey(args.Message))
			{
				reports[args.Message].ReportMessage = null;
				UpdateReports(args.Guild);
			}

			return Task.CompletedTask;
		}
	}
}
