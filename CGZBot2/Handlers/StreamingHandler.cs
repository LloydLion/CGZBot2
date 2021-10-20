using CGZBot2.Attributes;
using CGZBot2.Entities;
using CGZBot2.Tools;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
			HandlerState.Get(typeof(StreamingHandler), nameof(announcedStreams), (guild) => new List<AnnouncedStream>());

		private readonly UIS uis;


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

			uis = new UIS(this);
		}


		[Command("announce")]
		[Aliases("astream")]
		[Description("Аннонсирует стрим")]
		public Task Announce(CommandContext ctx)
		{
			if (announcedStreams[ctx.Guild].Count(s => s.Creator == ctx.Member) >= 2) // до 5 (вывод до 5 кнопок)
				ctx.RespondAsync("Вы уже запустили 2 стрима. Это лимит").TryDeleteAfter(8000);
			else uis.AnnounceDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("edit-stream")]
		[Aliases("estream")]
		[Description("Изменяет параметр стрима")]
		public Task EditStream(CommandContext ctx)
		{
			if(!announcedStreams[ctx.Guild].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не запустили ни одного стрима").TryDeleteAfter(8000);
			else uis.EditDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("cancel-stream")]
		[Aliases("cstream")]
		[Description("Отменяет стрим")]
		public Task CancelStream(CommandContext ctx)
		{
			if (!announcedStreams[ctx.Guild].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не запустили ни одного стрима").TryDeleteAfter(8000);
			else uis.DeleteDialog.Start(ctx.Channel, ctx.Member);

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


		private class UIS
		{
			private readonly StreamingHandler owner;


			public UIS(StreamingHandler owner)
			{
				this.owner = owner;

				#region AnnounceDialog
				AnnounceDialog = new MessagesDialogSource();

				AnnounceDialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowText("Введите название стрима", "msg"), DialogUtils.DeleteMessage("msg")));
				AnnounceDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Введите дату и время провидения", "msg"), DialogUtils.DeleteMessage("msg")));
				AnnounceDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Введите место провидения ($ссылка, &название Дискорд канала)", "msg"), DialogUtils.DeleteMessage("msg")));
				AnnounceDialog.AddMessage(new DialogMessage((MessageUID)3, DialogUtils.ShowText("Стрим успешно создан", "msg"), DialogUtils.DeleteMessage("msg")));

				AnnounceDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					if (string.IsNullOrWhiteSpace(msg.Content)) return false; //Аварийный случай, у дискорда есть встройная проверка

					dctx.DynamicParameters.Add("name", msg.Content);
					return true;
				}), MessageUID.StartMessage, (MessageUID)1);

				AnnounceDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					if (DateTime.TryParse(msg.Content, out var val))
					{
						dctx.DynamicParameters.Add("start", val);
						return true;
					}
					else
					{
						dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000);
						return false;
					}
				}), (MessageUID)1, (MessageUID)2);

				AnnounceDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					AnnouncedStream.StreamingPlaceType placeType;
					switch (msg.Content[0])
					{
						case '&': placeType = AnnouncedStream.StreamingPlaceType.Discord; break;
						case '$': placeType = AnnouncedStream.StreamingPlaceType.Internet; break;
						default: dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000); return false;
					}

					dctx.DynamicParameters.Add("placeType", placeType);
					dctx.DynamicParameters.Add("place", msg.Content[1..]);
					return true;
				}), (MessageUID)2, (MessageUID)3);

				AnnounceDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(8000)), true), (MessageUID)3, MessageUID.EndDialog);

				AnnounceDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				AnnounceDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);
				AnnounceDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)2, MessageUID.EndDialog);

				AnnounceDialog.OnMessageChangedTo((dctx) =>
				{
					var streamName = (string)dctx.DynamicParameters["name"];
					var date = (DateTime)dctx.DynamicParameters["start"];
					var place = (string)dctx.DynamicParameters["place"];
					var placeType = (AnnouncedStream.StreamingPlaceType)dctx.DynamicParameters["placeType"];

					var stream = new AnnouncedStream(streamName, dctx.Caller, date, place, placeType);

					lock (stream.SyncRoot)
					{
						owner.InitStream(stream);

						StreamCreated?.Invoke(stream);

						owner.announcedStreams[dctx.Caller.Guild].Add(stream);
						owner.UpdateReport(stream);

						HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), owner.announcedStreams);

						stream.Run();
					}
				}, (MessageUID)3);

				AnnounceDialog.OnMessageChangedTo((dctx) => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region EditDialog
				EditDialog = new MessagesDialogSource();

				EditDialog.AddMessage(new DialogMessage(MessageUID.StartMessage,
					DialogUtils.ShowButtonList((dctx) => owner.announcedStreams.SelectMany(s => s.Value).ToList(), (c, o) => o.Name, (c, o) => o.Creator == c.Caller, "Выберете стрим", "msg", "stream"), DialogUtils.DeleteMessage("msg")));
				EditDialog.AddMessage(new DialogMessage((MessageUID)1,
					DialogUtils.ShowButtonList((dctx) => new string[] { "Название", "Дата и время начала", "Место провидения" }, (c, o) => o, (c, o) => true, "Выберете параметр", "msg", "param"), DialogUtils.DeleteMessage("msg")));
				EditDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Введите новое значение параметра", "msg"), DialogUtils.DeleteMessage("msg")));
				EditDialog.AddMessage(new DialogMessage((MessageUID)3, DialogUtils.ShowText("Стрим изменён", "msg"), DialogUtils.DeleteMessage("msg")));


				EditDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("stream"), MessageUID.StartMessage, (MessageUID)1);
				EditDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("param"), (MessageUID)1, (MessageUID)2);
				EditDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					var stream = (AnnouncedStream)dctx.DynamicParameters["stream"];

					switch ((string)dctx.DynamicParameters["param"])
					{
						case "Название": stream.Name = msg.Content; break;
						case "Дата и время начала":
							if (DateTime.TryParse(msg.Content, out var val)) { stream.StartDate = val; break; }
							else { dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000); return false; }
						case "Место провидения":
							AnnouncedStream.StreamingPlaceType placeType;

							switch (msg.Content[0])
							{
								case '&': placeType = AnnouncedStream.StreamingPlaceType.Discord; break;
								case '$': placeType = AnnouncedStream.StreamingPlaceType.Internet; break;
								default: dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000); return false;
							}

							stream.PlaceType = placeType;
							stream.Place = msg.Content[1..];
							break;
					}

					stream.RequestReportMessageUpdate();
					owner.UpdateReport(stream);
					HandlerState.Set(typeof(StreamingHandler), nameof(announcedStreams), owner.announcedStreams);

					return true;
				}), (MessageUID)2, (MessageUID)3);

				EditDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>(token => new Task(() => { Thread.Sleep(8000); })), (MessageUID)3, MessageUID.EndDialog);

				EditDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				EditDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);
				EditDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)2, MessageUID.EndDialog);

				EditDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region DeleteDialog
				DeleteDialog = new MessagesDialogSource();

				DeleteDialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowButtonList((dctx) => owner.announcedStreams.SelectMany(s => s.Value).ToList(),
					(dc, o) => o.Name, (dc, o) => o.Creator == dc.Caller, "Выберете стрим", "msg", "stream"), DialogUtils.DeleteMessage("msg")));
				DeleteDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Стрим отменён", "msg"), DialogUtils.DeleteMessage("msg")));


				DeleteDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("stream"), MessageUID.StartMessage, (MessageUID)1);
				DeleteDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>(token => new Task(() => { Thread.Sleep(8000); })), (MessageUID)1, MessageUID.EndDialog);

				DeleteDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);


				DeleteDialog.OnMessageChangedTo(dctx =>
				{
					var stream = (AnnouncedStream)dctx.DynamicParameters["stream"];
					owner.announcedStreams[stream.Guild].Remove(stream);
					stream.Cancel();
				}, (MessageUID)1);
				#endregion
			}


			public MessagesDialogSource AnnounceDialog { get; }

			public MessagesDialogSource EditDialog { get; }

			public MessagesDialogSource DeleteDialog { get; }
		}
	}
}
