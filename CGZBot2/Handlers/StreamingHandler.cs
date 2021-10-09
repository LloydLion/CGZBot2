using CGZBot2.Attributes;
using CGZBot2.Entities;
using CGZBot2.Tools;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	[Description("Аннонсы и стримы")]
	class StreamingHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<DiscordChannel> announceChannel =
			BotSettings.Load<DiscordChannel>(typeof(StreamingHandler), nameof(announceChannel));


		private readonly GuildDictionary<List<AnnouncedStream>> announcedStreams =
			HandlerState.Get(typeof(StreamingHandler), nameof(announcedStreams), () => new List<AnnouncedStream>());


		public static event Action<AnnouncedStream> StreamCreated;


		public StreamingHandler()
		{
			Program.Client.MessageDeleted += OnMessageDeleted;

			foreach (var l in announcedStreams)
			{	
				foreach (var stream in l.Value)
				{
					lock (stream.SyncRoot)
					{
						InitStream(stream);
					}
				}

				UpdateReports(l.Key);

				foreach (var stream in l.Value)
				{
					lock (stream.SyncRoot)
					{
						stream.Run();
					}
				}
			}
		}


		[Command("announce")]
		[Description("Аннонсирует стрим")]
		public Task Announce(CommandContext ctx
			//[Description("Название стрима")] string streamName,
			//[Description("Время провидения")] DateTime date,
			//[Description("Место провидения (В дискорд канале - &НАЗВАНИЕ или в интернете - $ССЫЛКА)")] string place)
			)
		{
			var dialog = new MessagesDialogSource();

			dialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowText("Введите название стрима", "msg"), DialogUtils.DeleteMessage("msg")));
			dialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Введите дату и время провидения", "msg"), DialogUtils.DeleteMessage("msg")));
			dialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Введите место провидения ($ссылка, &название Дискорд канала)", "msg"), DialogUtils.DeleteMessage("msg")));
			dialog.AddMessage(new DialogMessage((MessageUID)3, DialogUtils.ShowText("Стрим успешно создан", "msg"), DialogUtils.DeleteMessage("msg")));

			dialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) =>
			{
				return new Task(() =>
				{
					var task = Utils.WaitForMessage(ctx.Member, ctx.Channel, token).StartAndWait();
					if (token.IsCancellationRequested) return;
					dctx.DynamicParameters.Add("name", task.Result.Message.Content);
				});
			}, true), MessageUID.StartMessage, (MessageUID)1);
			
			dialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) =>
			{
				return new Task(() =>
				{
				retry:
					var task = Utils.WaitForMessage(ctx.Member, ctx.Channel, token).StartAndWait();
					if (token.IsCancellationRequested) return;

					if (DateTime.TryParse(task.Result.Message.Content, out var val))
					{
						dctx.DynamicParameters.Add("start", val);
						return;
					}
					else
					{
						dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000);
						goto retry;
					}
				});
			}, true), (MessageUID)1, (MessageUID)2);
			
			dialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) =>
			{
				return new Task(() =>
				{
				retry:
					var task = Utils.WaitForMessage(ctx.Member, ctx.Channel, token).StartAndWait();
					if (token.IsCancellationRequested) return;

					var content = task.Result.Message.Content;

					AnnouncedStream.StreamingPlaceType placeType;
					switch (content[0])
					{
						case '&': placeType = AnnouncedStream.StreamingPlaceType.Discord; break;
						case '$': placeType = AnnouncedStream.StreamingPlaceType.Internet; break;
						default: dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000); goto retry;
					}

					dctx.DynamicParameters.Add("placeType", placeType);
					dctx.DynamicParameters.Add("place", content[1..]);
				});
			}, true), (MessageUID)2, (MessageUID)3);

			dialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(8000)), true), (MessageUID)3, MessageUID.EndDialog);

			ITransitWorker<MessageUID> factory(DialogContext dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => { Thread.Sleep(20000); if(!token.IsCancellationRequested) dctx.DynamicParameters.Add("bad", new object()); }), true);
			dialog.AddTransit(factory, MessageUID.StartMessage, MessageUID.EndDialog);
			dialog.AddTransit(factory, (MessageUID)1, MessageUID.EndDialog);
			dialog.AddTransit(factory, (MessageUID)2, MessageUID.EndDialog);

			dialog.OnMessageChangedTo((dctx) =>
			{
				var streamName = (string)dctx.DynamicParameters["name"];
				var date = (DateTime)dctx.DynamicParameters["start"];
				var place = (string)dctx.DynamicParameters["place"];
				var placeType = (AnnouncedStream.StreamingPlaceType)dctx.DynamicParameters["placeType"];

				var stream = new AnnouncedStream(streamName, ctx.Member, date, place, placeType);

				lock (stream.SyncRoot)
				{
					InitStream(stream);

					StreamCreated?.Invoke(stream);

					announcedStreams[ctx].Add(stream);
					UpdateReport(stream);

					stream.Run();
				}
			}, (MessageUID)3);

			dialog.OnMessageChangedTo((dctx) => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);

			var actual = dialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
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
			UpdateReport(stream);
		}

		[HelpUseLimits(CommandUseLimit.Private)]
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

		private void UpdateReport(AnnouncedStream stream, bool clear = true)
		{
			try
			{
				var channel = announceChannel[stream.Guild];

				if (clear)
				{
					var nonDel = announcedStreams[stream.Guild].Select(s => s.ReportMessage).ToList();

					var msgs = channel.GetMessagesAsync(1000).Result;
					var toDel = msgs.Where(s => !nonDel.Contains(s));
					foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }
				}

				if (!stream.NeedReportUpdate) return;

				lock (stream.SyncRoot)
				{
					var builder = new DiscordEmbedBuilder();
					var msgbuilder = new DiscordMessageBuilder();

					if (stream.State.HasFlag(AnnouncedStream.StreamState.Announced))
					{
						builder
							.WithColor(DiscordColor.Cyan)
							.WithTimestamp(stream.CreationDate)
							.WithAuthor(stream.Creator.DisplayName, iconUrl: stream.Creator.AvatarUrl)
							.WithTitle("Аннонс стрима")
							.AddField("Название", stream.Name)
							.AddField("Время провидения", stream.StartDate.ToString())
							.AddField("Место", stream.PlaceType == AnnouncedStream.StreamingPlaceType.Discord ?
								$"В дискорд канале {stream.Place}" : stream.Place);

						if (stream.State == AnnouncedStream.StreamState.WaitingForStreamer)
							msgbuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, "start", "Начать стрим",
								emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"))));
					}
					else if (stream.State == AnnouncedStream.StreamState.Running)
					{
						builder
							.WithColor(DiscordColor.Blurple)
							.WithTimestamp(stream.RealStartDate)
							.WithAuthor(stream.Creator.DisplayName, iconUrl: stream.Creator.AvatarUrl)
							.WithTitle("Стрим начат")
							.AddField("Название", stream.Name)
							.AddField("Время провидения", stream.StartDate.ToString())
							.AddField("Место", stream.PlaceType == AnnouncedStream.StreamingPlaceType.Discord ?
								$"В дискорд канале {stream.Place}" : stream.Place);

						msgbuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, "stop", "Завершить стрим",
							emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":x:"))));

						if (stream.PlaceType == AnnouncedStream.StreamingPlaceType.Internet)
						{
							msgbuilder.AddComponents(new DiscordLinkButtonComponent(stream.Place, "Смотреть стрим",
								emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":earth_africa:"))));
						}
					}
					else return;

					msgbuilder.AddEmbed(builder.Build());

					if (stream.ReportMessage == null || !stream.ReportMessage.IsExist())
						stream.ReportMessage = channel.SendMessageAsync(msgbuilder).Result;
					else
						stream.ReportMessage = stream.ReportMessage.ModifyAsync(msgbuilder).Result;
				}
			}
			catch (Exception ex)
			{
				Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while updating report in UpdateReport for StreamingHandler");
			}
			finally
			{
				stream.ReportMessageType = stream.State;
				stream.ResetReportUpdate();
			}
		}

		private void UpdateReports(DiscordGuild guild)
		{
			try
			{
				if (Monitor.IsEntered(guild))
					Program.Client.Logger.Log(LogLevel.Information, "UpdateReports in StreamingHandler called twice");
				else Monitor.Enter(guild);

				foreach (var stream in announcedStreams[guild])
				{
					UpdateReport(stream);
				}
			}
			catch (Exception ex)
			{
				Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while updating reportS in UpdateReportS for StreamingHandler");
			}
			finally
			{
				if (Monitor.IsEntered(guild)) Monitor.Exit(guild);
			}
		}

		private void InitStream(AnnouncedStream stream)
		{
			stream.Started += StartedStreamHandler;
			stream.WaitingForStreamer += WaitingForStreamerHandler;
			stream.Finished += FinishedStreamHandler;
			stream.Canceled += CanceledStreamHandler;

			stream.StartWorker = new PredicateTransitWorker<AnnouncedStream.StreamState>(s => StartWaitPredicate(stream));
			stream.StreamerWaitWorker = new TaskTransitWorker<AnnouncedStream.StreamState>(waitButton("start"), true);
			stream.StreamEndWorker = new TaskTransitWorker<AnnouncedStream.StreamState>(waitButton("stop"), true);

			Func<CancellationToken, Task> waitButton(string btnid)
			{
				return (token) =>
				{
					return new Task(() =>
					{
					restart:
						var args = Utils.WaitForButton(() => stream.ReportMessage, btnid).StartAndWait().Result;

						var builder = new DiscordInteractionResponseBuilder().AsEphemeral(true);

						var member = stream.Guild.GetMemberAsync(args.User.Id).Result;
						if (member != stream.Creator)
						{
							builder.WithContent("Вы не создатель стрима");
							try { args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder).Wait(); } catch(Exception ex)
							{
								Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while sending interaction responce");
							}
							goto restart;
						}
						else
						{
							builder.WithContent("Успешно");
							try { args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder).Wait(); } catch(Exception ex)
							{
								Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while sending interaction responce");
							}
						}
					});
				};
			}
		}

		private bool StartWaitPredicate(AnnouncedStream stream)
		{
			return stream.StartDate < DateTime.Now;
		}

		private void WaitingForStreamerHandler(AnnouncedStream stream)
		{
			stream.Creator.SendDicertMessage($"Напоминание о трансляции \"{stream.Name}\", мы ждём только вас");

			UpdateReport(stream);

			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private void StartedStreamHandler(AnnouncedStream stream)
		{
			UpdateReport(stream);
			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private void FinishedStreamHandler(AnnouncedStream stream)
		{
			announcedStreams[stream.Guild].Remove(stream);
			UpdateReport(stream);
			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private void CanceledStreamHandler(AnnouncedStream stream)
		{
			UpdateReport(stream);
			HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), announcedStreams);
		}

		private Task OnMessageDeleted(DiscordClient _, MessageDeleteEventArgs args)
		{
			var streams = announcedStreams[args.Guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);
			if (streams.ContainsKey(args.Message))
			{
				streams[args.Message].RequestReportMessageUpdate();
				UpdateReport(streams[args.Message]);
			}

			return Task.CompletedTask;
		}
	}
}
