using CGZBot2.Entities;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	class StreamingHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<DiscordChannel> announceChannel =
			BotSettings.Load<DiscordChannel>(typeof(StreamingHandler), nameof(announceChannel));


		private readonly GuildDictionary<List<AnnouncedStream>> announcedStreams =
			//new() { DefaultValueFactory = () => new List<AnnouncedStream>() };
			HandlerState.Get(typeof(StreamingHandler), nameof(announcedStreams), () => new List<AnnouncedStream>());


		public StreamingHandler()
		{
			foreach (var l in announcedStreams)
				foreach (var stream in l.Value)
				{
					stream.Started += StartedStreamHandler;
					stream.WaitingForStreamer += WaitingForStreamerHandler;
					stream.Finished += FinishedStreamHandler;

					stream.StartWait = StartWaitPredicate;
					stream.StreamerWait = StreamerWaitPredicate;
					stream.StreamEndWait = StreamerWaitPredicate;

					stream.LaunchWaitTask();
				}
		}


		[Command("announce")]
		[Description("Аннонсирует стрим")]
		public async Task Announce(CommandContext ctx,
			[Description("Название стрима")] string streamName,
			[Description("Время провидения")] DateTime date,
			[Description("Место провидения (В дискорд канале - &НАЗВАНИЕ или в интернете - $ССЫЛКА)")] string place)
		{
			AnnouncedStream.StreamingPlaceType placeType;

			switch(place[0])
			{
				case '&':
					placeType = AnnouncedStream.StreamingPlaceType.Discord; break;
				case '$':
					placeType = AnnouncedStream.StreamingPlaceType.Internet; break;
				default:
					var resp = await ctx.RespondAsync
						("Неожиданный токен в 1 символе параметра place\r\nИспользуйте /help announce");
					await Task.Delay(8000);
					resp.TryDelete();
					ctx.Message.TryDelete();
					return;
			}


			var stream = new AnnouncedStream(streamName, ctx.Member, date, place[1..], placeType);

			stream.Started += StartedStreamHandler;
			stream.WaitingForStreamer += WaitingForStreamerHandler;
			stream.Finished += FinishedStreamHandler;

			stream.StartWait = StartWaitPredicate;
			stream.StreamerWait = StreamerWaitPredicate;
			stream.StreamEndWait = StreamerWaitPredicate;

			announcedStreams[ctx].Add(stream);
			UpdateReports(ctx.Guild);

			stream.StartWaitTask.Start();
		}

		private void UpdateReports(DiscordGuild guild)
		{
			var channel = announceChannel[guild];

			var nonDel = announcedStreams[guild].Select(s => s.ReportMessage).ToList();
			var dic = announcedStreams[guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);

			var msgs = channel.GetMessagesAsync(1000).Result;
			var toDel = msgs.Where(s => !nonDel.Contains(s) || dic[s].State != dic[s].ReportMessageType);
			foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }

			foreach (var stream in announcedStreams[guild])
			{
				if (stream.State == stream.ReportMessageType) continue;

				var builder = new DiscordEmbedBuilder();


				if (stream.State.HasFlag(AnnouncedStream.StreamState.Announced))
					builder
						.WithColor(DiscordColor.Cyan)
						.WithTimestamp(stream.CreationDate)
						.WithAuthor(stream.Creator.DisplayName, iconUrl: stream.Creator.AvatarUrl)
						.WithTitle("Аннонс стрима")
						.AddField("Название", stream.Name)
						.AddField("Время провидения", stream.StartDate.ToString())
						.AddField("Место", stream.PlaceType == AnnouncedStream.StreamingPlaceType.Discord ?
							$"В дискорд канале; {stream.Place}" : stream.Place);
				else if (stream.State == AnnouncedStream.StreamState.Running)
					builder
						.WithColor(DiscordColor.Blurple)
						.WithTimestamp(stream.RealStartDate)
						.WithAuthor(stream.Creator.DisplayName, iconUrl: stream.Creator.AvatarUrl)
						.WithTitle("Стрим начат")
						.AddField("Название", stream.Name)
						.AddField("Время провидения", stream.StartDate.ToString())
						.AddField("Место", stream.PlaceType == AnnouncedStream.StreamingPlaceType.Discord ?
							$"В дискорд канале {stream.Place}" : stream.Place);
				else continue;

				stream.ReportMessage = channel.SendMessageAsync(builder.Build()).Result;
				stream.ReportMessageType = stream.State;
			}
		}

		private bool StartWaitPredicate(AnnouncedStream stream)
		{
			return stream.StartDate < DateTime.Now;
		}

		private bool StreamerWaitPredicate(AnnouncedStream stream)
		{
			return stream.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"))
				.Result.Where(s => s == stream.Creator).Any();
		}

		private bool StreamEndPredicate(AnnouncedStream stream)
		{
			return stream.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":x:"))
				.Result.Where(s => s == stream.Creator).Any();
		}

		private void WaitingForStreamerHandler(AnnouncedStream stream)
		{
			var dmc = stream.Creator.CreateDmChannelAsync().Result;
			dmc.SendMessageAsync($"Напоминание о трансляции \"{stream.Name}\", мы ждём только вас");

			UpdateReports(stream.Guild);
			stream.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"));
		}

		private void StartedStreamHandler(AnnouncedStream stream)
		{
			UpdateReports(stream.Guild);
			stream.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":x:"));
		}

		private void FinishedStreamHandler(AnnouncedStream stream)
		{
			announcedStreams[stream.Guild].Remove(stream);
			stream.ReportMessage.TryDelete();
			UpdateReports(stream.Guild);
		}
	}
}
