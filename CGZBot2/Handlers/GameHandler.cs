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
	[Description("Коммандные игры и пати")]
	class GameHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<DiscordChannel> reportChannel =
			BotSettings.Load<DiscordChannel>(typeof(GameHandler), nameof(reportChannel));
		private static readonly GuildDictionary<DiscordChannel> voiceCreationCategory =
			BotSettings.Load<DiscordChannel>(typeof(GameHandler), nameof(voiceCreationCategory));


		private readonly GuildDictionary<List<TeamGame>> startedGames =
			HandlerState.Get(typeof(GameHandler), nameof(startedGames), (guild) => new List<TeamGame>());

		private readonly GuildDictionary<List<MembersParty>> parties =
			HandlerState.Get(typeof(GameHandler), nameof(parties), (guild) => new List<MembersParty>());

		private readonly UIS uis;


		public static event Action<TeamGame> GameCreated;
		public static event Action<MembersParty> PartyCreated;


		public GameHandler()
		{
			Program.Client.MessageDeleted += OnMessageDeleted;
			Program.Client.ChannelDeleted += (s, a) => { if (a.Channel.Type == ChannelType.Voice) return OnVoiceChannelDeleted(s, a); else return Task.CompletedTask; };

			foreach (var l in startedGames)
			{
				foreach (var game in l.Value)
				{
					lock (game.SyncRoot)
					{
						InitGame(game);
					}
				}

				UpdateReports(l.Key);

				foreach (var game in l.Value)
				{
					lock (game.SyncRoot)
					{
						game.Run();
					}
				}
			}

			uis = new UIS(this);
		}

		[Command("team-game")]
		[Aliases("tgame")]
		[Description("Начинает коммандную игру")]
		public Task StartTeamGame(CommandContext ctx)
		{
			if (startedGames[ctx.Guild].Count(s => s.Creator == ctx.Member) >= 2) // до 5 (вывод до 5 кнопок)
				ctx.RespondAsync("Вы уже запустили 2 игры. Это лимит").TryDeleteAfter(8000);
			else uis.CreateGameDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("cancel-game")]
		[Aliases("cgame")]
		[Description("Отменяет игру (Только создатель)")]
		public Task CancelGame(CommandContext ctx)
		{
			if (!startedGames[ctx.Guild].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не запустили ни одной игры").TryDeleteAfter(8000);
			else uis.DeleteGameDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Admins)]
		[Command("cancel-game-hard")]
		[Description("Отменяет любую игру")]
		public Task CancelGame(CommandContext ctx,
			[Description("Создатель")] DiscordMember creator,
			[Description("Имя отменяймой игры")] params string[] name)
		{
			if (!ctx.Member.Permissions.HasPermission(Permissions.ManageChannels))
			{
				ctx.RespondAsync("У вас не достаточно прав для этой операции (Управление каналами)").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}


			var game = GetGame(ctx, string.Join(" ", name), creator);
			if (game == null) return Task.CompletedTask;

			game.Cancel();
			startedGames[ctx].Remove(game);

			HandlerState.Set(typeof(GameHandler), nameof(startedGames), startedGames);

			ctx.RespondAsync("Игра успешно отменена").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Aliases("egame")]
		[Command("edit-game")]
		[Description("Изменяет параметр игры (Только создатель)")]
		public Task EditGame(CommandContext ctx)
		{
			if (!startedGames[ctx.Guild].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не запустили ни одной игры").TryDeleteAfter(8000);
			else uis.EditGameDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("control-game")]
		[Aliases("ctrlgame")]
		[Description("Открывает панель управления приглашениями игры (Только создатель)")]
		public Task OpenGameControlPanel(CommandContext ctx)
		{
			MessagesDialog dialog;
			if (!startedGames[ctx.Guild].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не запустили ни одной игры").TryDeleteAfter(8000);
			else dialog = uis.ManageGameInvsDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[Command("party")]
		[Description("Создаёт пати с указанным списком участноков")]
		public Task CreateParty(CommandContext ctx)
		{
			uis.CreatePartyDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("delete-party")]
		[Aliases("dparty")]
		[Description("Удаляет пати с сервера (только создатель)")]
		public Task DeleteParty(CommandContext ctx)
		{
			if (!parties[ctx].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не создали ни одного пати");
			else uis.DeletePartyDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[Command("list-parties")]
		[Aliases("lparties")]
		[Description("Показывает список пати на сервере")]
		public Task ListParties(CommandContext ctx)
		{
			var partiesSel1 = parties[ctx].Where(s => s.Members.Contains(ctx.Member)).ToList();
			var partiesSel2 = parties[ctx].Where(s => !s.Members.Contains(ctx.Member)).ToList();

			var fieldVal1 = string.Join("\n", partiesSel1.Select(s => s.Name + (s.Creator == ctx.Member ? " [Вы владелец]" : " - от " + s.Creator.Mention)));
			var fieldVal2 = string.Join("\n", partiesSel2.Select(s => s.Name + (s.Creator == ctx.Member ? " [Вы владелец]" : " - от " + s.Creator.Mention)));

			var builder = new DiscordEmbedBuilder();

			builder
				.WithAuthor(ctx.Member.DisplayName, iconUrl: ctx.Member.AvatarUrl)
				.WithTimestamp(DateTime.Now)
				.WithColor(DiscordColor.Gold);

			if (string.IsNullOrWhiteSpace(fieldVal1) == false)
				builder.AddField("Ваши пати", fieldVal1);
			else
				builder.AddField("Вы не состоите ни в одном пати", "Вы можете просоединится к одному из пати ниже или создать своё");

			if (string.IsNullOrWhiteSpace(fieldVal2) == false)
				builder.AddField("Доступные пати", fieldVal2);
			else
				builder.AddField("Доступные пати", "На этом сервере нет доступных пати");

			ctx.RespondAsync(builder).TryDeleteAfter(20000);

			return Task.CompletedTask;
		}

		[Command("list-party")]
		[Aliases("lparty")]
		[Description("Выводит информацию об указанном пати")]
		public Task ListParty(CommandContext ctx,
			[Description("Название пати")] params string[] partyName)
		{
			var party = GetParty(ctx, string.Join(" ", partyName.JoinWords()));
			if (party == null) return Task.CompletedTask;

			var fieldVal = string.Join(", ", party.Members.Select(s => s.Mention));

			var builder = new DiscordEmbedBuilder();

			builder
				.WithTitle("Информация о пати " + partyName.JoinWords())
				.WithColor(DiscordColor.IndianRed)
				.AddField("Создатель", party.Creator.Mention)
				.AddField("Участники", fieldVal);

			ctx.RespondAsync(builder).TryDeleteAfter(20000);
			return Task.CompletedTask;
		}

		[Command("req-join")]
		[Aliases("rjp")]
		[Description("Отправляет запрос на присоединение к пати его создателю")]
		public Task SendJoinRequest(CommandContext ctx,
			[Description("Название пати")] params string[] partyName)
		{
			var party = GetParty(ctx, string.Join(" ", partyName));
			if (party == null) return Task.CompletedTask;


			if (party.Members.Contains(ctx.Member))
			{
				ctx.RespondAsync("Вы уже состоите в этом пати").TryDeleteAfter(8000);
			}
			else
			{				
				party.Creator.SendDicertMessage($"Запрос на присоединение к пати {party.Name} на сервере {party.Creator.Guild.Name} от {ctx.Member.Mention}");

				ctx.RespondAsync("Запрос отправлен").TryDeleteAfter(8000);
			}

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("kick-party")]
		[Aliases("kparty")]
		[Description("Кикает участника из пати (только владелец)")]
		public Task KickPartyMember(CommandContext ctx,
			[Description("Участник")] DiscordMember member,
			[Description("Название пати")] params string[] partyName)
		{
			MembersParty party;
			if (ctx.Member.Permissions.HasPermission(Permissions.ManageChannels))
				party = GetParty(ctx, partyName.JoinWords());
			else
			{
				var partiesSel = parties[ctx].Where(s => ctx.Member == s.Creator && s.Name == partyName.JoinWords()).ToArray();

				if (partiesSel.Length == 0)
				{
					ctx.RespondAsync("Такого пати не существует.\r\nПоиск шёл только среди **ваших** пати").TryDeleteAfter(8000);
					return Task.CompletedTask;
				}

				if (partiesSel.Length > 1) throw new Exception("Ce Pi**ec");
				party = partiesSel.Single();
			}

			if (party == null) return Task.CompletedTask;

			if (party.Creator == member)
			{
				ctx.RespondAsync("Вы не можете выгнать себя т.к. вы создатель пати").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			if (party.Members.Remove(member))
			{
				ctx.RespondAsync("Участник успешно кикнут").TryDeleteAfter(8000);
			}
			else
			{
				ctx.RespondAsync("Участника нет в этом пати").TryDeleteAfter(8000);
			}

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("join-party")]
		[Aliases("joinp")]
		[Description("Присоединяет участника к пати (только владелец)")]
		public Task JoinMember(CommandContext ctx)
		{
			if (!parties[ctx].Any(s => s.Creator == ctx.Member))
				ctx.RespondAsync("Вы не создали ни одного пати");
			else uis.JoinPartyDialog.Start(ctx.Channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Description("Удаляет вас из пати")]
		[Command("exit-party")]
		public Task ExitParty(CommandContext ctx,
			[Description("Название пати")] params string[] partyName)
		{
			var party = GetParty(ctx, string.Join(" ", partyName));

			if (party == null) return Task.CompletedTask;

			if (party.Creator == ctx.Member)
			{
				ctx.RespondAsync("Вы не можете уйти из пати т.к. вы создатель пати").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			if (party.Members.Remove(ctx.Member))
			{
				ctx.RespondAsync("Вы вышли из пати").TryDeleteAfter(8000);
			}
			else
			{
				ctx.RespondAsync("Вас нет в этом пати").TryDeleteAfter(8000);
			}

			return Task.CompletedTask;
		}

		private void InitGame(TeamGame game)
		{
			game.MembersWaitWorker = new PredicateTransitWorker<TeamGame.GameState>(s => MembersWaitPredicate(game));
			game.CreatorWaitWorker = new TaskTransitWorker<TeamGame.GameState>(waitButton("start"), true);
			game.GameEndWorker = new TaskTransitWorker<TeamGame.GameState>(waitButton("stop"), true);

			game.Started += StartedGameHandler;
			game.Finished += FinishedGameHandler;
			game.Canceled += CanceledGameHandler;
			game.WaitingForMembers += WaitingForMembersGameHandler;
			game.WaitingForCreator += WaitingForCreatorGameHandler;
			game.StateMachine.StateChanged += (a) => GameStateChangedHandler(game);

			Func<CancellationToken, Task> waitButton(string btnid)
			{
				return (token) =>
				{
					return new Task(() =>
					{
					restart:
						var args = Utils.WaitForButton(() => game.ReportMessage, btnid).StartAndWait().Result;
						if (token.IsCancellationRequested) return;

						var builder = new DiscordInteractionResponseBuilder().AsEphemeral(true);

						var member = game.Guild.GetMemberAsync(args.User.Id).Result;
						if (member != game.Creator)
						{
							builder.WithContent("Вы не создатель игры");
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
					}, token);
				};
			}
		}

		private TeamGame GetGame(CommandContext ctx, string gameName, DiscordMember creator = null)
		{
			bool admin = false;
			if (creator == null) creator = ctx.Member;
			else admin = true;

			var games = startedGames[ctx].Where(s => s.Creator == creator && s.GameName == gameName).ToArray();

			if (games.Length == 0)
			{
				if (!admin)
					ctx.RespondAsync("Такой игры не существует.\r\nПоиск шёл только среди **ваших** игр").TryDeleteAfter(8000);
				else
					ctx.RespondAsync("Такой игры не существует.\r\nПоиск шёл среди **всех** игр").TryDeleteAfter(8000);
				return null;
			}

			if (games.Length > 1) throw new Exception("Ce Pi**ec");
			return games.Single();
		}

		private MembersParty GetParty(CommandContext ctx, string partyName)
		{
			var partiesSel = parties[ctx].Where(s => s.Name == partyName).ToArray();

			if (partiesSel.Length == 0)
			{
				ctx.RespondAsync("Такого пати не существует.\r\nПоиск шёл среди **всех** пати на сервере").TryDeleteAfter(8000);
				return null;
			}

			if (partiesSel.Length > 1) throw new Exception("Ce Pi**ec");
			return partiesSel.Single();
		}

		private void UpdateReport(TeamGame game, bool clear = true)
		{
			try
			{
				var channel = reportChannel[game.Guild];

				if (clear)
				{
					var nonDel = startedGames[game.Guild].Select(s => s.ReportMessage).ToList();

					var msgs = channel.GetMessagesAsync(1000).Result;
					var toDel = msgs.Where(s => !nonDel.Contains(s));
					foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }
				}

				if (!game.NeedReportUpdate) return;

				lock (game.SyncRoot)
				{
					var builder = new DiscordEmbedBuilder();
					var msgbuilder = new DiscordMessageBuilder();

					if (game.State.HasFlag(TeamGame.GameState.Created))
					{
						builder
							.WithColor(DiscordColor.DarkRed)
							.WithAuthor(game.Creator.DisplayName, iconUrl: game.Creator.AvatarUrl)
							.WithTitle("Игра в " + game.GameName)
							.AddField("Запуск при", "присоединении " + game.TargetMembersCount + " участников" + (game.ReqAllInvited ? " и всех приглашённых" : ""))
							.WithTimestamp(game.CreationDate);

						var invStr = string.Join(", ", game.Invited.Select(s => s.Mention));
						if (!string.IsNullOrWhiteSpace(invStr)) builder.AddField("Приглашены", invStr);
						if (!string.IsNullOrWhiteSpace(game.Description)) builder.AddField("Описание", game.Description);

						msgbuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, "start", "Начать игру",
							game.State == TeamGame.GameState.Created, new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"))));
					}
					else if (game.State == TeamGame.GameState.Running)
					{
						builder
							.WithColor(DiscordColor.HotPink)
							.WithAuthor(game.Creator.DisplayName, iconUrl: game.Creator.AvatarUrl)
							.WithTitle("Игра в " + game.GameName + " начата")
							.AddField("Участники", string.Join(", ", game.TeamMembers.Select(s => s.Mention)))
							.WithTimestamp(game.StartDate);

						msgbuilder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, "stop", "Завершить игру", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":x:"))));
					}
					else return;
					msgbuilder.AddEmbed(builder.Build());

					if (game.ReportMessage == null || !game.ReportMessage.IsExist())
						game.ReportMessage = channel.SendMessageAsync(msgbuilder).Result;
					else
						game.ReportMessage = game.ReportMessage.ModifyAsync(msgbuilder).Result;


					if (game.State.HasFlag(TeamGame.GameState.Created))
						game.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":ok_hand:"));
					else if (game.State == TeamGame.GameState.Running)
						game.ReportMessage.DeleteAllReactionsAsync();

				}
			}
			catch(Exception ex)
			{
				Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while updating report in UpdateReport for GameHandler");
			}
			finally
			{
				game.ReportMessageType = game.State;
				game.ResetReportUpdate();
			}
		}

		private void UpdateReports(DiscordGuild guild)
		{
			try
			{
				if (Monitor.IsEntered(guild))
					Program.Client.Logger.Log(LogLevel.Information, "UpdateReports in GameHandler called twice");
				else Monitor.Enter(guild);

				foreach (var game in startedGames[guild])
				{
					UpdateReport(game);
				}
			}
			catch (Exception ex)
			{
				Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while updating reportS in UpdateReportS for GameHandler");
			}
			finally
			{
				if (Monitor.IsEntered(guild)) Monitor.Exit(guild);
			}
		}

		private void CreateVoiceChannel(TeamGame game)
		{
			var overs = new DiscordOverwriteBuilder[] { new DiscordOverwriteBuilder(game.Creator).Allow(Permissions.All) };
			game.CreatedVoice = game.Guild.CreateChannelAsync("Игра в " + game.GameName, ChannelType.Voice,
				voiceCreationCategory[game.Guild], overwrites: overs).Result;
		}

		private void GameStateChangedHandler(TeamGame game)
		{
			UpdateReport(game);
			HandlerState.Set(typeof(GameHandler), nameof(startedGames), startedGames);
		}

		private void StartedGameHandler(TeamGame game)
		{
			var members = GetEmoji(game, ":ok_hand:");
			game.TeamMembers.AddRange(members);

			foreach (var member in game.TeamMembers)
				member.SendDicertMessage($"Игра в {game.GameName} от {game.Creator.Mention} началась.");

			CreateVoiceChannel(game);
		}

		private void WaitingForCreatorGameHandler(TeamGame game)
		{
			game.Creator.SendDicertMessage($"Ваша игра в {game.GameName} готова к запуску.");
		}

		private void WaitingForMembersGameHandler(TeamGame game)
		{
			game.Creator.SendDicertMessage($"Ваша игра в {game.GameName} более не готова к запуску - участник вышел из игры.");
		}

		private void FinishedGameHandler(TeamGame game)
		{
			startedGames[game.Guild].Remove(game);
			game.CreatedVoice.TryDelete();
		}

		private void CanceledGameHandler(TeamGame game)
		{
			startedGames[game.Guild].Remove(game);
			game.CreatedVoice.TryDelete();
		}

		private bool MembersWaitPredicate(TeamGame game)
		{
			lock (game.SyncRoot)
			{
				var members = GetEmoji(game, ":ok_hand:");

				if (members.Length >= game.TargetMembersCount)
				{
					if (game.ReqAllInvited) { if (members.Intersect(game.Invited).SequenceEqual(game.Invited)) return true; }
					else return true;
				}

				return false;
			}
		}

		private DiscordMember[] GetEmoji(TeamGame game, string name)
		{
			if (Program.Connected == false) return Array.Empty<DiscordMember>();

			try
			{
				return game.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, name)).Result
					.Where(s => !s.IsBot).Select(s => game.Guild.GetMemberAsync(s.Id).Result).ToArray();
			}
			catch(Exception ex)
			{
				Program.Client.Logger.Log(LogLevel.Warning, ex, "Exception while getting reactions of report message ({0})", game.ReportMessage?.Id.ToString() ?? "null");
				return Array.Empty<DiscordMember>();
			}
		}

		private Task OnMessageDeleted(DiscordClient _, MessageDeleteEventArgs args)
		{
			var reports = startedGames[args.Guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);
			if (reports.ContainsKey(args.Message))
			{
				reports[args.Message].RequestReportMessageUpdate();
				UpdateReport(reports[args.Message]);
			}

			return Task.CompletedTask;
		}

		private Task OnVoiceChannelDeleted(DiscordClient _, ChannelDeleteEventArgs args)
		{
			var reports = startedGames[args.Guild].Where(s => s.CreatedVoice != null).ToDictionary(s => s.CreatedVoice);
			if (reports.ContainsKey(args.Channel))
			{
				reports[args.Channel].RequestReportMessageUpdate();
				CreateVoiceChannel(reports[args.Channel]);
			}

			return Task.CompletedTask;
		}


		private class UIS
		{
			private readonly GameHandler owner;


			public UIS(GameHandler owner)
			{
				this.owner = owner;

				#region CreateGameDialog
				CreateGameDialog = new MessagesDialogSource();

				CreateGameDialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowText("Введите название игры", "msg"), DialogUtils.DeleteMessage("msg")));
				CreateGameDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Введите целевое кол-во участников", "msg"), DialogUtils.DeleteMessage("msg")));
				CreateGameDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Введите описание игры (. - для пустого)", "msg"), DialogUtils.DeleteMessage("msg")));
				CreateGameDialog.AddMessage(new DialogMessage((MessageUID)3, DialogUtils.ShowText("Вводите @упоминания тех того хотите пригласить, а потом введите точку (отдельно)", "msg"), DialogUtils.DeleteMessage("msg")));
				CreateGameDialog.AddMessage(new DialogMessage((MessageUID)4, DialogUtils.ShowButtonList((dctx) => new bool[] { true, false },
					(dctx, obj) => obj ? "Ждать" : "Не ждать", (dctx, obj) => true, "Ждать ли всех приглашённых?", "msg", "waitInvs"), DialogUtils.DeleteMessage("msg")));
				CreateGameDialog.AddMessage(new DialogMessage((MessageUID)5, DialogUtils.ShowText("Игра создана", "msg"), DialogUtils.DeleteMessage("msg")));


				CreateGameDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					if (string.IsNullOrWhiteSpace(msg.Content)) return false; //Аварийный случай, у дискорда есть встройная проверка

					if (owner.startedGames[dctx.Channel.Guild].Any(s => s.Creator == dctx.Caller && s.GameName == msg.Content))
					{
						dctx.Channel.SendMessageAsync("Игра с таким названием от вас уже существует. Попробуйте другое имя").TryDeleteAfter(8000);
						return false;
					}

					dctx.DynamicParameters.Add("name", msg.Content);
					return true;
				}), MessageUID.StartMessage, (MessageUID)1);

				CreateGameDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					if (int.TryParse(msg.Content, out var val))
					{
						dctx.DynamicParameters.Add("tmc", val);
						return true;
					}
					else
					{
						dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000);
						return false;
					}
				}), (MessageUID)1, (MessageUID)2);

				CreateGameDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					if (string.IsNullOrWhiteSpace(msg.Content)) return false; //Аварийный случай, у дискорда есть встройная проверка

					if (msg.Content.Trim() == ".") dctx.DynamicParameters.Add("desc", "");
					else dctx.DynamicParameters.Add("desc", msg.Content);

					return true;
				}), (MessageUID)2, (MessageUID)3);

				CreateGameDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() =>
				{
					var invited = new List<DiscordMember>();
				
				retry:
					var args = Utils.WaitForMessage(dctx.Caller, dctx.Channel, token).StartAndWait().Result;
					if (token.IsCancellationRequested) return;

					if (args.Message.Content.Trim() == ".")
					{
						dctx.DynamicParameters.Add("invited", invited);
						return;
					}
					else
					{
						invited.AddRange(args.MentionedUsers.Select(s => dctx.Channel.Guild.GetMemberAsync(s.Id).Result).ToList());
						goto retry;
					}
				}, token)), (MessageUID)3, (MessageUID)4);

				CreateGameDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("waitInvs"), (MessageUID)4, (MessageUID)5);

				CreateGameDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(8000)), true), (MessageUID)5, MessageUID.EndDialog);

				CreateGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				CreateGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);
				CreateGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)3, MessageUID.EndDialog);
				CreateGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)4, MessageUID.EndDialog);

				CreateGameDialog.OnMessageChangedTo((dctx) =>
				{
					var name = (string)dctx.DynamicParameters["name"];
					var tmc = (int)dctx.DynamicParameters["tmc"];
					var desc = (string)dctx.DynamicParameters["desc"];
					var invs = (List<DiscordMember>)dctx.DynamicParameters["invited"];
					var waitInvs = (bool)dctx.DynamicParameters["waitInvs"];

					var game = new TeamGame(dctx.Caller, name, desc, tmc) { Invited = invs.Distinct().ToHashSet(), ReqAllInvited = waitInvs };

					lock (game.SyncRoot)
					{
						owner.InitGame(game);

						GameCreated?.Invoke(game);

						owner.startedGames[dctx.Channel.Guild].Add(game);
						owner.UpdateReport(game);

						HandlerState.Set(typeof(GameHandler), nameof(startedGames), owner.startedGames);

						game.Run();
					}
				}, (MessageUID)5);
				#endregion

				#region EditGameDialog
				EditGameDialog = new MessagesDialogSource();

				EditGameDialog.AddMessage(new DialogMessage(MessageUID.StartMessage,
					DialogUtils.ShowButtonList((dctx) => owner.startedGames.SelectMany(s => s.Value).ToList(), (dctx, obj) => obj.GameName, (dctx, obj) => obj.Guild == dctx.Channel.Guild && obj.Creator == dctx.Caller, "Выберите игру", "msg", "game"),
					DialogUtils.DeleteMessage("msg")));
				EditGameDialog.AddMessage(new DialogMessage((MessageUID)1,
					DialogUtils.ShowButtonList((dctx) => new string[] { "Имя игры", "Целевое кол-во участников", "Введите описание игры (. - для пустого)" }, (c, o) => o, (c, o) => true, "Выберете параметр", "msg", "param"), DialogUtils.DeleteMessage("msg")));
				EditGameDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Введите новое значение параметра", "msg"), DialogUtils.DeleteMessage("msg")));
				EditGameDialog.AddMessage(new DialogMessage((MessageUID)3, DialogUtils.ShowText("Игра изменена", "msg"), DialogUtils.DeleteMessage("msg")));


				EditGameDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("game"), MessageUID.StartMessage, (MessageUID)1);
				EditGameDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("param"), (MessageUID)1, (MessageUID)2);
				EditGameDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					var game = (TeamGame)dctx.DynamicParameters["game"];

					switch ((string)dctx.DynamicParameters["param"])
					{
						case "Имя игры": game.GameName = msg.Content; break;
						case "Целевое кол-во участников":
							if (int.TryParse(msg.Content, out var val)) { game.TargetMembersCount = val; break; }
							else { dctx.Channel.SendMessageAsync("Попробуйте ещё раз").TryDeleteAfter(8000); return false; }
						case "Введите описание игры (. - для пустого)":
							if (string.IsNullOrWhiteSpace(msg.Content)) return false; //Аварийный случай, у дискорда есть встройная проверка

							if (msg.Content.Trim() == ".") dctx.DynamicParameters.Add("desc", "");
							else dctx.DynamicParameters.Add("desc", msg.Content);

							break;
					}

					game.RequestReportMessageUpdate();
					owner.UpdateReport(game);
					HandlerState.Set(typeof(GameHandler), nameof(startedGames), owner.startedGames);

					return true;
				}), (MessageUID)2, (MessageUID)3);

				EditGameDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>(token => new Task(() => { Thread.Sleep(8000); })), (MessageUID)3, MessageUID.EndDialog);

				EditGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				EditGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);
				EditGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)2, MessageUID.EndDialog);

				EditGameDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region DeleteGameDialog
				DeleteGameDialog = new MessagesDialogSource();

				DeleteGameDialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowButtonList((dctx) => owner.startedGames.SelectMany(s => s.Value).ToList(),
					(dc, o) => o.GameName, (dc, o) => o.Creator == dc.Caller, "Выберете игру", "msg", "game"), DialogUtils.DeleteMessage("msg")));
				DeleteGameDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Игра отменена", "msg"), DialogUtils.DeleteMessage("msg")));


				DeleteGameDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("game"), MessageUID.StartMessage, (MessageUID)1);
				DeleteGameDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>(token => new Task(() => { Thread.Sleep(8000); })), (MessageUID)1, MessageUID.EndDialog);

				DeleteGameDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);


				DeleteGameDialog.OnMessageChangedTo(dctx =>
				{
					var game = (TeamGame)dctx.DynamicParameters["game"];
					owner.startedGames[game.Guild].Remove(game);
					game.Cancel();
				}, (MessageUID)1);

				DeleteGameDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region CreatePartyDialog
				CreatePartyDialog = new MessagesDialogSource();

				CreatePartyDialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowText("Введите название пати", "msg"), DialogUtils.DeleteMessage("msg")));
				CreatePartyDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Вводите @упоминания тех того хотите пригласить, а потом введите точку (отдельно)", "msg"), DialogUtils.DeleteMessage("msg")));
				CreatePartyDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Пати создано", "msg"), DialogUtils.DeleteMessage("msg")));


				CreatePartyDialog.AddTransit(DialogUtils.WaitForMessageTransitFactory((msg, dctx) =>
				{
					if (string.IsNullOrWhiteSpace(msg.Content)) return false; //Аварийный случай, у дискорда есть встройная проверка

					if (owner.parties[dctx.Channel.Guild].Any(s => s.Name == msg.Content))
					{
						dctx.Channel.SendMessageAsync("Пати с таким названием уже существует. Попробуйте другое имя").TryDeleteAfter(8000);
						return false;
					}

					dctx.DynamicParameters.Add("name", msg.Content);
					return true;
				}), MessageUID.StartMessage, (MessageUID)1);

				CreatePartyDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() =>
				{
					var members = new List<DiscordMember>();

				retry:
					var args = Utils.WaitForMessage(dctx.Caller, dctx.Channel, token).StartAndWait().Result;
					if (token.IsCancellationRequested) return;

					if (args.Message.Content.Trim() == ".")
					{
						dctx.DynamicParameters.Add("members", members);
						return;
					}
					else
					{
						members.AddRange(args.MentionedUsers.Select(s => dctx.Channel.Guild.GetMemberAsync(s.Id).Result).ToList());
						goto retry;
					}
				}, token)), (MessageUID)1, (MessageUID)2);


				CreatePartyDialog.OnMessageChangedTo(dctx =>
				{
					var name = (string)dctx.DynamicParameters["name"];
					var members = (List<DiscordMember>)dctx.DynamicParameters["members"];

					var party = new MembersParty(dctx.Caller, name);
					party.Members.AddRange(members);
					party.Members.Add(party.Creator);

					PartyCreated?.Invoke(party);

					owner.parties[dctx.Channel.Guild].Add(party);

					HandlerState.Set(typeof(GameHandler), nameof(parties), owner.parties);
				}, (MessageUID)2);

				CreatePartyDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region DeletePartyDialog
				DeletePartyDialog = new MessagesDialogSource();

				DeletePartyDialog.AddMessage(new DialogMessage(MessageUID.StartMessage, DialogUtils.ShowButtonList((dctx) => owner.parties.SelectMany(s => s.Value).ToList(),
					(dc, o) => o.Name, (dc, o) => o.Creator == dc.Caller, "Выберете пати", "msg", "party"), DialogUtils.DeleteMessage("msg")));
				DeletePartyDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Пати удалено", "msg"), DialogUtils.DeleteMessage("msg")));


				DeletePartyDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("party"), MessageUID.StartMessage, (MessageUID)1);
				DeletePartyDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>(token => new Task(() => { Thread.Sleep(8000); })), (MessageUID)1, MessageUID.EndDialog);

				DeletePartyDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);


				DeletePartyDialog.OnMessageChangedTo(dctx =>
				{
					var party = (MembersParty)dctx.DynamicParameters["party"];
					owner.parties[dctx.Channel.Guild].Remove(party);

					HandlerState.Set(typeof(GameHandler), nameof(parties), owner.parties);
				}, (MessageUID)1);

				DeletePartyDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region ManageGameInvsDialog
				ManageGameInvsDialog = new MessagesDialogSource();


				ManageGameInvsDialog.AddMessage(new DialogMessage(MessageUID.StartMessage,
					DialogUtils.ShowButtonList((dctx) => owner.startedGames.SelectMany(s => s.Value).ToList(), (dctx, obj) => obj.GameName, (dctx, obj) => obj.Guild == dctx.Channel.Guild && obj.Creator == dctx.Caller, "Выберите игру", "msg", "game"),
					DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)1, (dctx, sctx) =>
				{
					dctx.DynamicParameters["controlPanel"] = sctx.DynamicParameters["msg"] = dctx.Channel.SendMessageAsync((builder) =>
					{
						var game = (TeamGame)dctx.DynamicParameters["game"];

						builder.WithContent("Управление приглашениями");
						builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, "sendInvs", "Отправить приглашения", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":postbox:"))),
							new DiscordButtonComponent(ButtonStyle.Danger, "clearInvs", "Очистить приглашения", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":wastebasket:"))),
							new DiscordButtonComponent(ButtonStyle.Secondary, "toggleWait", (game.ReqAllInvited ? "Не ждать" : "Ждать") + " всех приглашённых", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":cd:"))));
						builder.AddComponents(new DiscordButtonComponent(ButtonStyle.Secondary, "inviteParty", "Пригласить пати", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":newspaper:"))),
							new DiscordButtonComponent(ButtonStyle.Secondary, "inviteMember", "Пригласить участника", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":e_mail:"))),
							new DiscordButtonComponent(ButtonStyle.Danger, "exit", "Выход", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(Program.Client, ":x:"))));
					}).Result;
				}, DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Приглашения отправлены в ЛС", "msg"), DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)3, DialogUtils.ShowText("Приглашения очищены", "msg"), DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)4, DialogUtils.ShowText("Статус ожидания изменён", "msg"), DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)5, DialogUtils.ShowText("Участники приглашены", "msg"), DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)6, DialogUtils.ShowButtonList((dctx) => owner.parties.SelectMany(s => s.Value).ToList(),
					(dc, o) => o.Name, (dc, o) => o.Creator == dc.Caller, "Выберете пати", "msg", "party"), DialogUtils.DeleteMessage("msg")));
				ManageGameInvsDialog.AddMessage(new DialogMessage((MessageUID)7, DialogUtils.ShowText("Вводите @упоминания тех того хотите пригласить, а потом введите точку (отдельно)", "msg"), DialogUtils.DeleteMessage("msg")));


				ManageGameInvsDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("game"), MessageUID.StartMessage, (MessageUID)1);
				ManageGameInvsDialog.AddTransit(DialogUtils.WaitForButtonTransitFactory((dctx) => (DiscordMessage)dctx.DynamicParameters["controlPanel"], (dctx) =>
				{
					var game = (TeamGame)dctx.DynamicParameters["game"];
					foreach (var member in game.Invited)
						member.SendDicertMessage($"Вы были приглашены на игру в {game.GameName} на сервере {game.Guild.Name} от {game.Creator.Mention}\r\n" + game.ReportMessage.JumpLink);
					return true;
				}, "sendInvs"), (MessageUID)1, (MessageUID)2);
				ManageGameInvsDialog.AddTransit(DialogUtils.WaitForButtonTransitFactory((dctx) => (DiscordMessage)dctx.DynamicParameters["controlPanel"], (dctx) =>
				{
					var game = (TeamGame)dctx.DynamicParameters["game"];
					game.Invited.Clear();
					game.RequestReportMessageUpdate();
					owner.UpdateReport(game);
					HandlerState.Set(typeof(GameHandler), nameof(startedGames), owner.startedGames);
					return true;
				}, "clearInvs"), (MessageUID)1, (MessageUID)3);
				ManageGameInvsDialog.AddTransit(DialogUtils.WaitForButtonTransitFactory((dctx) => (DiscordMessage)dctx.DynamicParameters["controlPanel"], (dctx) =>
				{
					var game = (TeamGame)dctx.DynamicParameters["game"];
					game.ReqAllInvited = !game.ReqAllInvited;
					game.RequestReportMessageUpdate();
					owner.UpdateReport(game);
					HandlerState.Set(typeof(GameHandler), nameof(startedGames), owner.startedGames);
					return true;
				}, "toggleWait"), (MessageUID)1, (MessageUID)4);
				ManageGameInvsDialog.AddTransit(DialogUtils.WaitForButtonTransitFactory((dctx) => (DiscordMessage)dctx.DynamicParameters["controlPanel"], (dctx) => true, "inviteParty"), (MessageUID)1, (MessageUID)6);
				ManageGameInvsDialog.AddTransit(DialogUtils.WaitForButtonTransitFactory((dctx) => (DiscordMessage)dctx.DynamicParameters["controlPanel"], (dctx) => true, "inviteMember"), (MessageUID)1, (MessageUID)7);
				ManageGameInvsDialog.AddTransit(DialogUtils.WaitForButtonTransitFactory((dctx) => (DiscordMessage)dctx.DynamicParameters["controlPanel"], (dctx) => true, "exit"), (MessageUID)1, MessageUID.EndDialog);
				ManageGameInvsDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("party"), (MessageUID)6, (MessageUID)5);
				ManageGameInvsDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() =>
				{
					var members = new List<DiscordMember>();

				retry:
					var args = Utils.WaitForMessage(dctx.Caller, dctx.Channel, token).StartAndWait().Result;
					if (token.IsCancellationRequested) return;

					if (args.Message.Content.Trim() == ".")
					{
						dctx.DynamicParameters.Add("members", members);
						return;
					}
					else
					{
						members.AddRange(args.MentionedUsers.Select(s => dctx.Channel.Guild.GetMemberAsync(s.Id).Result).ToList());
						goto retry;
					}
				}, token)), (MessageUID)7, (MessageUID)5);


				ManageGameInvsDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(3000))), (MessageUID)2, (MessageUID)1);
				ManageGameInvsDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(3000))), (MessageUID)3, (MessageUID)1);
				ManageGameInvsDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(3000))), (MessageUID)4, (MessageUID)1);
				ManageGameInvsDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() => Thread.Sleep(3000))), (MessageUID)5, (MessageUID)1);

				ManageGameInvsDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				ManageGameInvsDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);
				ManageGameInvsDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)6, MessageUID.EndDialog);
				ManageGameInvsDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)7, MessageUID.EndDialog);


				ManageGameInvsDialog.OnMessageChangedTo((dctx) =>
				{
					var game = (TeamGame)dctx.DynamicParameters["game"];

					if (dctx.DynamicParameters.ContainsKey("party"))
					{
						var party = (MembersParty)dctx.DynamicParameters["party"];
						dctx.DynamicParameters.Remove("party");
						game.Invited.AddRange(party.Members);
					}
					else
					{
						var members = (List<DiscordMember>)dctx.DynamicParameters["members"];
						dctx.DynamicParameters.Remove("members");
						game.Invited.AddRange(members);
					}

					game.RequestReportMessageUpdate();
					owner.UpdateReport(game);
					HandlerState.Set(typeof(GameHandler), nameof(startedGames), owner.startedGames);
				}, (MessageUID)5);

				ManageGameInvsDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				//TODO: Починить и использовать в kick-party
				#region KickPartyDialog
				KickPartyDialog = new MessagesDialogSource();


				KickPartyDialog.AddMessage(new DialogMessage(MessageUID.StartMessage,
					DialogUtils.ShowButtonList((_) => owner.parties.SelectMany(s => s.Value).ToArray(), (dctx, obj) => obj.Name, (dctx, obj) => obj.Creator == dctx.Caller, "Выберете пати", "msg", "party"),
					DialogUtils.DeleteMessage("msg")));
				KickPartyDialog.AddMessage(new DialogMessage((MessageUID)1,
					DialogUtils.ShowButtonList((dctx) => (IReadOnlyCollection<DiscordMember>)((MembersParty)dctx.DynamicParameters["party"]).Members, (dctx, obj) => obj.Nickname,
						(dctx, obj) => obj != ((MembersParty)dctx.DynamicParameters["party"]).Creator, "Выберете участника", "msg", "member"),
					DialogUtils.DeleteMessage("msg")));
				KickPartyDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Участник кикнут", "msg"), DialogUtils.DeleteMessage("msg")));


				KickPartyDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("party"), MessageUID.StartMessage, (MessageUID)1);
				KickPartyDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("member"), (MessageUID)1, (MessageUID)2);

				KickPartyDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				KickPartyDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);


				DeletePartyDialog.OnMessageChangedTo(dctx =>
				{
					var party = (MembersParty)dctx.DynamicParameters["party"];
					var member = (DiscordMember)dctx.DynamicParameters["member"];

					party.Members.Remove(member);

					HandlerState.Set(typeof(GameHandler), nameof(parties), owner.parties);
				}, (MessageUID)3);
				DeletePartyDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion

				#region JoinPartyDialog
				JoinPartyDialog = new MessagesDialogSource();


				JoinPartyDialog.AddMessage(new DialogMessage(MessageUID.StartMessage,
					DialogUtils.ShowButtonList((_) => owner.parties.SelectMany(s => s.Value).ToArray(), (dctx, obj) => obj.Name, (dctx, obj) => obj.Creator == dctx.Caller, "Выберете пати", "msg", "party"),
					DialogUtils.DeleteMessage("msg")));
				JoinPartyDialog.AddMessage(new DialogMessage((MessageUID)1, DialogUtils.ShowText("Вводите @упоминания тех того хотите пригласить, а потом введите точку (отдельно)", "msg"), DialogUtils.DeleteMessage("msg")));
				JoinPartyDialog.AddMessage(new DialogMessage((MessageUID)2, DialogUtils.ShowText("Участник добавлен", "msg"), DialogUtils.DeleteMessage("msg")));


				JoinPartyDialog.AddTransit(DialogUtils.ButtonSelectorTransitFactory("party"), MessageUID.StartMessage, (MessageUID)1);
				JoinPartyDialog.AddTransit((dctx) => new TaskTransitWorker<MessageUID>((token) => new Task(() =>
				{
					var members = new List<DiscordMember>();

				retry:
					var args = Utils.WaitForMessage(dctx.Caller, dctx.Channel, token).StartAndWait().Result;
					if (token.IsCancellationRequested) return;

					if (args.Message.Content.Trim() == ".")
					{
						dctx.DynamicParameters.Add("members", members);
						return;
					}
					else
					{
						members.AddRange(args.MentionedUsers.Select(s => dctx.Channel.Guild.GetMemberAsync(s.Id).Result).ToList());
						goto retry;
					}
				}, token)), (MessageUID)1, (MessageUID)2);

				JoinPartyDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), MessageUID.StartMessage, MessageUID.EndDialog);
				JoinPartyDialog.AddTransit(DialogUtils.TimeoutTransitFactory(), (MessageUID)1, MessageUID.EndDialog);


				JoinPartyDialog.OnMessageChangedTo(dctx =>
				{
					var party = (MembersParty)dctx.DynamicParameters["party"];
					var members = (List<DiscordMember>)dctx.DynamicParameters["members"];

					party.Members.AddRange(members);

					HandlerState.Set(typeof(GameHandler), nameof(parties), owner.parties);
				}, (MessageUID)2);
				JoinPartyDialog.OnMessageChangedTo(dctx => { if (dctx.DynamicParameters.ContainsKey("bad")) dctx.Channel.SendMessageAsync("Диалог прерван").TryDeleteAfter(8000); }, MessageUID.EndDialog);
				#endregion
			}


			public MessagesDialogSource CreateGameDialog { get; }

			public MessagesDialogSource EditGameDialog { get; }

			public MessagesDialogSource DeleteGameDialog { get; }

			public MessagesDialogSource CreatePartyDialog { get; }

			public MessagesDialogSource DeletePartyDialog { get; }

			public MessagesDialogSource ManageGameInvsDialog { get; }

			public MessagesDialogSource KickPartyDialog { get; }

			public MessagesDialogSource JoinPartyDialog { get; }
		}
	}
}
