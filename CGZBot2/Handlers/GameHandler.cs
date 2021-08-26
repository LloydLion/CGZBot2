﻿using CGZBot2.Entities;
using CGZBot2.Tools;
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
			foreach (var l in startedGames)
			{
				foreach (var game in l.Value)
				{
					lock(game.SyncRoot)
					{
						game.Started += StartedGameHandler;
						game.Finished += FinishedGameHandler;
						game.Canceled += CanceledGameHandler;
						game.WaitingForMembers += WaitingForMembersGameHandler;
						game.WaitingForCreator += WaitingForCreatorGameHandler;
						game.StateMachine.StateChanged += (a) => GameStateChangedHandler(game);

						game.MembersWait = MembersWaitPredicate;
						game.GameEndWait = EndWaitPredicate;
						game.CreatorWait = CreatorWaitPredicate;

						game.Run();
					}
				}

				UpdateReports(l.Key);
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
				game.MembersWait = MembersWaitPredicate;
				game.GameEndWait = EndWaitPredicate;
				game.CreatorWait = CreatorWaitPredicate;

				game.Started += StartedGameHandler;
				game.Finished += FinishedGameHandler;
				game.Canceled += CanceledGameHandler;
				game.WaitingForMembers += WaitingForMembersGameHandler;
				game.WaitingForCreator += WaitingForCreatorGameHandler;
				game.StateMachine.StateChanged += (a) => GameStateChangedHandler(game);

				game.Run();

				GameCreated?.Invoke(game);

				startedGames[ctx].Add(game);
				UpdateReports(ctx.Guild);

			}

			return Task.CompletedTask;
		}

		[Command("cancel-game")]
		[Description("Отменяет игру")]
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

		[Command("edit-game")]
		[Description("Изменяет параметр игры")]
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
			UpdateReports(ctx.Guild);
		}

		[Command("clear-game-invs")]
		[Description("Отменяет все приглашения у указаной игры")]
		public Task ChangeInvited(CommandContext ctx,
			[Description("Название игры")] string name)
		{
			var game = GetGame(ctx, name);
			if (game == null) return Task.CompletedTask;

			game.Invited.Clear();
			game.RequestReportMessageUpdate();
			UpdateReports(game.Guild);
			return Task.CompletedTask;
		}

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
			UpdateReports(game.Guild);
			return Task.CompletedTask;
		}

		[Command("send-game-invs")]
		[Description("Оправляет приглашения всем приглашённым участником в игре")]
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

		[Command("invite-party")]
		[Description("Приглашает всех участников пати в игру")]
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
			UpdateReports(game.Guild);
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

		private void UpdateReports(DiscordGuild guild)
		{
			lock (guild)
			{

				var channel = reportChannel[guild];

				var nonDel = startedGames[guild].Select(s => s.ReportMessage).ToList();
				var dic = startedGames[guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);

				var msgs = channel.GetMessagesAsync(1000).Result;
				var toDel = msgs.Where(s => !nonDel.Contains(s));
				foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }

				foreach (var game in startedGames[guild])
				{
					if (!game.NeedReportUpdate) continue;

					lock (game.SyncRoot)
					{
						var builder = new DiscordEmbedBuilder();

						if (game.State == TeamGame.GameState.Created)
						{
							var invStr = string.Join(", ", game.Invited.Select(s => s.Mention));
							if (string.IsNullOrWhiteSpace(invStr)) invStr = "--";

							builder
								.WithColor(DiscordColor.DarkRed)
								.WithAuthor(game.Creator.DisplayName, game.Creator.AvatarUrl)
								.WithTitle("Игра в " + game.GameName)
								.AddField("Приглашены", invStr)
								.AddField("Запуск при", "присоединении " + game.TargetMembersCount + " участников" + (game.ReqAllInvited ? " и всех приглашённых" : ""))
								.WithTimestamp(game.CreationDate);
							if (!string.IsNullOrWhiteSpace(game.Description)) builder.AddField("Описание", game.Description);
						}
						else if (game.State == TeamGame.GameState.Running)
							builder
								.WithColor(DiscordColor.HotPink)
								.WithAuthor(game.Creator.DisplayName, game.Creator.AvatarUrl)
								.WithTitle("Игра в " + game.GameName + " начата")
								.AddField("Участники", string.Join(", ", game.TeamMembers.Select(s => s.Mention)))
								.WithTimestamp(game.StartDate);
						else continue;

						if (game.ReportMessage == null)
							game.ReportMessage = channel.SendMessageAsync(builder.Build()).Result;
						else
							game.ReportMessage = game.ReportMessage.ModifyAsync(builder.Build()).Result;

						if (game.State.HasFlag(TeamGame.GameState.Created))
						{
							game.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":ok_hand:"));
							game.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":arrow_forward:"));
						}
						else if (game.State == TeamGame.GameState.Running)
							game.ReportMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":x:"));

						game.ReportMessageType = game.State;
						game.ResetReportUpdate();
					}
				}
			}
		}

		private void GameStateChangedHandler(TeamGame game)
		{
			UpdateReports(game.Guild);
			HandlerState.Set(typeof(GameHandler), nameof(startedGames), startedGames);
		}

		private void StartedGameHandler(TeamGame game)
		{
			var members = GetEmoji(game, ":ok_hand:");
			game.TeamMembers.AddRange(members);

			foreach (var member in game.TeamMembers)
				member.SendDicertMessage($"Игра в {game.GameName} от {game.Creator.Mention} началась.");

			var overs = new DiscordOverwriteBuilder[] { new DiscordOverwriteBuilder(game.Creator).Allow(DSharpPlus.Permissions.All) };
			game.CreatedVoice = game.Guild.CreateChannelAsync("Игра в " + game.GameName, DSharpPlus.ChannelType.Voice,
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

		private bool EndWaitPredicate(TeamGame game)
		{
			lock(game.SyncRoot)
			{
				return GetEmoji(game, ":x:").Where(s => s == game.Creator).Any();
			}
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

		private bool CreatorWaitPredicate(TeamGame game)
		{
			lock(game.SyncRoot)
			{
				return GetEmoji(game, ":arrow_forward:").Any(s => s == game.Creator);
			}
		}

		private DiscordMember[] GetEmoji(TeamGame game, string name)
		{
			return game.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, name)).Result
				.Where(s => !s.IsBot).Select(s => game.Guild.GetMemberAsync(s.Id).Result).ToArray();
		}
	}
}
