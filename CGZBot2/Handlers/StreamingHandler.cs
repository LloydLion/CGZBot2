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
			HandlerState.Get(typeof(StreamingHandler), nameof(announcedStreams), () => new List<AnnouncedStream>());


		public static event Action<AnnouncedStream> StreamCreated;


		public StreamingHandler()
		{
			foreach (var l in announcedStreams)
			{	
				foreach (var stream in l.Value)
				{
					lock (stream.SyncRoot)
					{
						stream.Started += StartedStreamHandler;
						stream.WaitingForStreamer += WaitingForStreamerHandler;
						stream.Finished += FinishedStreamHandler;
						stream.Canceled += CanceledStreamHandler;

						stream.StartWait = StartWaitPredicate;
						stream.StreamerWait = StreamerWaitPredicate;
						stream.StreamEndWait = StreamEndPredicate;

						stream.Run();
					}
				}

				UpdateReports(l.Key);
			}
		}


		[Command("announce")]
		[Description("Аннонсирует стрим")]
		public Task Announce(CommandContext ctx,
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
					ctx.RespondAsync("Неожиданный токен в 1 символе параметра place\r\nИспользуйте /help announce").TryDeleteAfter(8000);
					return Task.CompletedTask;
			}

			if (announcedStreams[ctx].Any(s => s.Name == streamName && s.Creator == ctx.Member))
			{
				ctx.RespondAsync("Стрим с таким названием и от вас уже был аннонсирован").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			if (announcedStreams[ctx].Count(s => s.Creator == ctx.Member) >= 2)
			{
				ctx.RespondAsync("Вы превысили лимит. Нельзя аннонсировать больше 2 стримов").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			var stream = new AnnouncedStream(streamName, ctx.Member, date, place[1..], placeType);

			lock (stream.SyncRoot)
			{
				stream.Started += StartedStreamHandler;
				stream.WaitingForStreamer += WaitingForStreamerHandler;
				stream.Finished += FinishedStreamHandler;
				stream.Canceled += CanceledStreamHandler;

				stream.StartWait = StartWaitPredicate;
				stream.StreamerWait = StreamerWaitPredicate;
				stream.StreamEndWait = StreamEndPredicate;

				stream.Run();

				StreamCreated?.Invoke(stream);

				announcedStreams[ctx].Add(stream);
				UpdateReports(ctx.Guild);
			}

			return Task.CompletedTask;
		}

		[Command("edit-stream")]
		[Description("Изменяет параметр стрима")]
		public async Task EditStream(CommandContext ctx,
			[Description("Название изменяймого стрима")] string streamName,
			[Description("Имя изменяймого параметра\r\nДопустимые значения:\r\n" +
				"name - название, startDate - дата начала, place - место провидения")] string paramName,
			[Description("Новое значение параметра")] string newValue)
		{
			var streams = announcedStreams[ctx].Where(s => s.Creator == ctx.Member && s.Name == streamName).ToArray();

			if (streams.Length == 0)
			{
				ctx.RespondAsync("Такого стрима не существует.\r\nПоиск шёл только среди **ваших** стримов").TryDeleteAfter(8000);
				return;
			}

			if (streams.Length > 1) throw new Exception("Ce Pi**ec");

			var stream = streams.Single();

			switch (paramName)
			{
				case "name":
					stream.Name = newValue;
					break;
				case "startDate":
					if (stream.State == AnnouncedStream.StreamState.Announced)
						stream.StartDate = (DateTime)await ctx.CommandsNext.ConvertArgument<DateTime>(newValue, ctx);
					else
					{
						ctx.RespondAsync("Невозможно изменить дату начала стрима после его старта.\r\nПересоздайте стрим с новой датой начала").TryDeleteAfter(8000);
						return;
					}
					break;
				case "place":
					switch (newValue[0])
					{
						case '&':
							stream.PlaceType = AnnouncedStream.StreamingPlaceType.Discord; break;
						case '$':
							stream.PlaceType = AnnouncedStream.StreamingPlaceType.Internet; break;
						default:
							ctx.RespondAsync("Неожиданный токен в 1 символе параметра newValue").TryDeleteAfter(8000);
							return;
					}

					stream.Place = newValue[1..];
					break;
				default:
					ctx.RespondAsync($"Параметра {paramName} не сущетвует").TryDeleteAfter(8000);
					return;
			}

			stream.RequestReportMessageUpdate();
			UpdateReports(ctx.Guild);
		}

		[Command("cancel-stream")]
		[Description("Отменяет стрим")]
		public Task CancelStream(CommandContext ctx,
			[Description("Название отменяймого стрима")] string streamName)
		{
			var streams = announcedStreams[ctx].Where(s => s.Creator == ctx.Member && s.Name == streamName).ToArray();

			if(streams.Length == 0)
			{
				ctx.RespondAsync("Такого стрима не существует.\r\nПоиск шёл только среди **ваших** стримов").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}
			
			if(streams.Length > 1) throw new Exception("Ce Pi**ec");

			streams.Single().Cancel();
			announcedStreams[ctx].Remove(streams.Single());
			ctx.RespondAsync("Стрим успешно отменён").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		private void UpdateReports(DiscordGuild guild)
		{
			lock (guild)
			{
				var channel = announceChannel[guild];

				var nonDel = announcedStreams[guild].Select(s => s.ReportMessage).ToList();

				var msgs = channel.GetMessagesAsync(1000).Result;
				var toDel = msgs.Where(s => !nonDel.Contains(s));
				foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }

				foreach (var stream in announcedStreams[guild])
				{
					lock (stream.SyncRoot)
					{
						if (!stream.NeedReportUpdate) continue;

						stream.ReportMessage?.TryDelete();
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

						if (stream.State == AnnouncedStream.StreamState.Running)
							stream.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":x:"));
						else if (stream.State == AnnouncedStream.StreamState.WaitingForStreamer)
							stream.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"));

						stream.ReportMessageType = stream.State;
						stream.ResetReportUpdate();
					}
				}
			}
		}

		private bool StartWaitPredicate(AnnouncedStream stream)
		{
			return stream.StartDate < DateTime.Now;
		}

		private bool StreamerWaitPredicate(AnnouncedStream stream)
		{
			lock (stream.SyncRoot)
			{
				return stream.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"))
					.Result.Where(s => s == stream.Creator).Any();
			}
		}

		private bool StreamEndPredicate(AnnouncedStream stream)
		{
			lock (stream.SyncRoot)
			{
				return stream.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":x:"))
					.Result.Where(s => s == stream.Creator).Any();
			}
		}

		private void WaitingForStreamerHandler(AnnouncedStream stream)
		{
			stream.Creator.SendDicertMessage($"Напоминание о трансляции \"{stream.Name}\", мы ждём только вас");

			UpdateReports(stream.Guild);

			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private void StartedStreamHandler(AnnouncedStream stream)
		{
			UpdateReports(stream.Guild);
			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private void FinishedStreamHandler(AnnouncedStream stream)
		{
			announcedStreams[stream.Guild].Remove(stream);
			UpdateReports(stream.Guild);
			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private void CanceledStreamHandler(AnnouncedStream stream)
		{
			UpdateReports(stream.Guild);
			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}
	}
}
