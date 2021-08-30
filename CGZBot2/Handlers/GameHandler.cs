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


		private static readonly GuildDictionary<List<TeamGame>> startedGames =
			HandlerState.Get(typeof(GameHandler), nameof(startedGames), () => new List<TeamGame>());

		private static readonly GuildDictionary<List<MembersParty>> parties =
			HandlerState.Get(typeof(GameHandler), nameof(parties), () => new List<MembersParty>());


		public static event Action<TeamGame> GameCreated;
		public static event Action<MembersParty> PartyCreated;


		public GameHandler()
		{
			Program.Client.MessageDeleted += OnMessageDeleted;

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
		}

		[Command("team-game")]
		[Description("Начинает коммандную игру")]
		public Task StartTeamGame(CommandContext ctx,
			[Description("Имя игры")] string name,
			[Description("Целевое кол-во участников")] int targetMemberCount,
			[Description("Описание игры")] string description,
			[Description("Требовать ли присоединения всех приглашённых участников")] bool reqAllInvited = false,
			[Description("Приглашения для участников")] params DiscordMember[] invites)
		{
			if (startedGames[ctx].Any(s => s.GameName == name && s.Creator == ctx.Member))
			{
				ctx.RespondAsync("Игра с таким названием и от вас уже была создана").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			if (startedGames[ctx].Count(s => s.Creator == ctx.Member) >= 2)
			{
				ctx.RespondAsync("Вы превысили лимит. Нельзя создать больше 2 игр").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			if (targetMemberCount <= 0)
			{
				ctx.RespondAsync("Ошибка в параметре " + nameof(targetMemberCount) + " - число должно быть больше 0").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			var game = new TeamGame(ctx.Member, name, description, targetMemberCount) { Invited = invites, ReqAllInvited = reqAllInvited };

			lock (game.SyncRoot)
			{
				InitGame(game);

				GameCreated?.Invoke(game);

				startedGames[ctx].Add(game);
				UpdateReport(game);

				game.Run();
			}

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("cancel-game")]
		[Description("Отменяет игру (Только создатель)")]
		public Task CancelGame(CommandContext ctx,
			[Description("Имя отменяймой игры")] string name)
		{
			var game = GetGame(ctx, name);
			if (game == null) return Task.CompletedTask;

			game.Cancel();

			startedGames[ctx].Remove(game);
			ctx.RespondAsync("Игра успешно отменёна").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("edit-game")]
		[Description("Изменяет параметр игры (Только создатель)")]
		public async Task EditGame(CommandContext ctx,
			[Description("Название изменяймой игры")] string name,
			[Description("Имя изменяймого параметра\r\nДопустимые значения:\r\n" +
				"name - название, targetMemCount - целевое кол-во участников,\r\n" +
				"reqAllInv - требование всех приглашённых участников, description - описание")] string paramName,
			[Description("Новое значение")] string newValue)
		{
			var game = GetGame(ctx, name);
			if (game == null) return;

			switch (paramName)
			{
				case "name":
					game.GameName = newValue;
					break;
				case "targetMemCount":
					if (game.State == TeamGame.GameState.Created)
						game.TargetMembersCount = (int)await ctx.CommandsNext.ConvertArgument<int>(newValue, ctx);
					else
					{
						ctx.RespondAsync("Невозможно изменить дату начала стрима после его старта.\r\nПересоздайте стрим с новой датой начала").TryDeleteAfter(8000);
						return;
					}
					break;
				case "reqAllInv":
					if (game.State == TeamGame.GameState.Created)
						game.ReqAllInvited = (bool)await ctx.CommandsNext.ConvertArgument<bool>(newValue, ctx);
					else
					{
						ctx.RespondAsync("Невозможно изменить дату начала стрима после его старта.\r\nПересоздайте стрим с новой датой начала").TryDeleteAfter(8000);
						return;
					}
					break;
				case "description":
					game.Description = newValue;
					break;
				default:
					ctx.RespondAsync($"Параметра {paramName} не сущетвует").TryDeleteAfter(8000);
					return;
			}

			game.RequestReportMessageUpdate();
			UpdateReport(game);
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("clear-game-invs")]
		[Description("Отменяет все приглашения у указаной игры (Только создатель)")]
		public Task ChangeInvited(CommandContext ctx,
			[Description("Название игры")] string name)
		{
			var game = GetGame(ctx, name);
			if (game == null) return Task.CompletedTask;

			game.Invited.Clear();
			game.RequestReportMessageUpdate();
			UpdateReport(game);
			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("invite-togame")]
		[Description("Приглашает людей в игру")]
		public Task AddInvited(CommandContext ctx,
			[Description("Название игры")] string name,
			[Description("Приглашения для участников")] params DiscordMember[] invited)
		{
			var game = GetGame(ctx, name);
			if (game == null) return Task.CompletedTask;

			game.Invited.AddRange(invited);
			game.RequestReportMessageUpdate();
			UpdateReport(game);
			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("send-game-invs")]
		[Description("Оправляет приглашения всем приглашённым участником в игре (Только создатель)")]
		public Task SendInvites(CommandContext ctx,
			[Description("Название игры")] string gameName)
		{
			var game = GetGame(ctx, gameName);
			if (game == null) return Task.CompletedTask;

			foreach (var member in game.Invited)
			{
				member.SendDicertMessage($"Вы были приглашены на игру в {game.GameName} на сервере {game.Guild.Name} от {game.Creator.Mention}\r\n" + game.ReportMessage.JumpLink);
			}

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("invite-party")]
		[Description("Приглашает всех участников пати в игру (Только создатель)")]
		public Task AddPatryToInvited(CommandContext ctx,
			[Description("Название игры")] string gameName,
			[Description("Название игры")] string partyName)
		{
			var game = GetGame(ctx, gameName);
			if (game == null) return Task.CompletedTask;

			var party = GetParty(ctx, partyName);
			if (party == null) return Task.CompletedTask;

			game.Invited.AddRange(party.Members);
			game.RequestReportMessageUpdate();
			UpdateReport(game);
			return Task.CompletedTask;
		}

		[Command("party")]
		[Description("Создаёт пати с указанным списком участноков")]
		public Task CreateParty(CommandContext ctx,
			[Description("Название игры")] string name,
			[Description("Участники")] params DiscordMember[] members)
		{
			if(parties[ctx].Any(s => s.Name == name))
			{
				ctx.RespondAsync("Пати с таким именем уже сушествует").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			var party = new MembersParty(ctx.Member, name);
			party.Members.AddRange(members);
			party.Members.Add(party.Creator);

			PartyCreated?.Invoke(party);

			parties[ctx].Add(party);
			ctx.RespondAsync("Пати успешно создано").TryDeleteAfter(8000);
			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("delete-party")]
		[Description("Удаляет пати с сервера (только создатель)")]
		public Task DeleteParty(CommandContext ctx,
			[Description("Название пати")] string name)
		{
			var party = GetPartyPrivate(ctx, name);
			if (party == null) return Task.CompletedTask;

			parties[ctx].Remove(party);
			ctx.RespondAsync("Пати успешно удалён").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[Command("list-parties")]
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
				builder.AddField("Вы не состоите не в одном пати", "Вы можете просоединится к одному из пати ниже или создать своё");

			if (string.IsNullOrWhiteSpace(fieldVal2) == false)
				builder.AddField("Доступные пати", fieldVal2);
			else
				builder.AddField("Доступные пати", "На этом сервере нет доступных пати");

			ctx.RespondAsync(builder).TryDeleteAfter(20000);

			return Task.CompletedTask;
		}

		[Command("list-party")]
		[Description("Выводит информацию об указанном пати")]
		public Task ListParty(CommandContext ctx,
			[Description("Название пати")] string partyName)
		{
			var party = GetParty(ctx, partyName);
			if (party == null) return Task.CompletedTask;

			var fieldVal = string.Join(", ", party.Members.Select(s => s.Mention));

			var builder = new DiscordEmbedBuilder();

			builder
				.WithTitle("Информация о пати " + partyName)
				.WithColor(DiscordColor.IndianRed)
				.AddField("Создатель", party.Creator.Mention)
				.AddField("Участники", fieldVal);

			ctx.RespondAsync(builder).TryDeleteAfter(20000);
			return Task.CompletedTask;
		}

		[Command("join-party-req")]
		[Description("Отправляет запрос на присоединение к пати его создателю")]
		public Task SendJoinRequest(CommandContext ctx,
			[Description("Название пати")] string partyName)
		{
			var party = GetParty(ctx, partyName);
			if (party == null) return Task.CompletedTask;

			party.Creator.SendDicertMessage($"Отправлен запрос на присоединение к пати {party.Name} на сервере {party.Creator.Guild.Name} от {ctx.Member.Mention}");
			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Private)]
		[Command("kick-party")]
		[Description("Кикает участника из пати (только владелец)")]
		public Task KickPartyMember(CommandContext ctx,
			[Description("Название пати")] string partyName,
			[Description("Участник")] DiscordMember member)
		{
			var party = GetPartyPrivate(ctx, partyName);
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
		[Description("Присоединяет участника к пати (только владелец)")]
		public Task JoinMember(CommandContext ctx,
			[Description("Название пати")] string partyName,
			[Description("Участник")] DiscordMember member)
		{
			var party = GetPartyPrivate(ctx, partyName);
			if (party == null) return Task.CompletedTask;

			if (party.Members.Contains(member))
			{
				ctx.RespondAsync("Участник уже состоит в этом пати").TryDeleteAfter(8000);
			}
			else
			{
				party.Members.Add(member);
				ctx.RespondAsync("Участник успешно добавлен в пати").TryDeleteAfter(8000);
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

			Func<Task> waitButton(string btnid)
			{
				return () =>
				{
					return new Task(() =>
					{
					restart:
						var args = Utils.WaitForButton(() => game.ReportMessage, btnid).Result;

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
					});
				};
			}
		}

		private TeamGame GetGame(CommandContext ctx, string gameName)
		{
			var games = startedGames[ctx].Where(s => s.Creator == ctx.Member && s.GameName == gameName).ToArray();

			if (games.Length == 0)
			{
				ctx.RespondAsync("Такой игры не существует.\r\nПоиск шёл только среди **ваших** игр").TryDeleteAfter(8000);
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

		private MembersParty GetPartyPrivate(CommandContext ctx, string partyName)
		{
			var partiesSel = parties[ctx].Where(s => ctx.Member == s.Creator && s.Name == partyName).ToArray();

			if (partiesSel.Length == 0)
			{
				ctx.RespondAsync("Такого пати не существует.\r\nПоиск шёл только среди **ваших** пати").TryDeleteAfter(8000);
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

			var overs = new DiscordOverwriteBuilder[] { new DiscordOverwriteBuilder(game.Creator).Allow(Permissions.All) };
			game.CreatedVoice = game.Guild.CreateChannelAsync("Игра в " + game.GameName, ChannelType.Voice,
				voiceCreationCategory[game.Guild], overwrites: overs).Result;
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
			game.CreatedVoice.DeleteAsync();
			startedGames[game.Guild].Remove(game);
		}

		private void CanceledGameHandler(TeamGame game)
		{
			startedGames[game.Guild].Remove(game);
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
	}
}
