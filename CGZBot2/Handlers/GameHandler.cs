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
	class GameHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<DiscordChannel> reportChannel =
			BotSettings.Load<DiscordChannel>(typeof(GameHandler), nameof(reportChannel));
		private static readonly GuildDictionary<DiscordChannel> voiceCreationCategory =
			BotSettings.Load<DiscordChannel>(typeof(GameHandler), nameof(voiceCreationCategory));


		private static readonly GuildDictionary<List<TeamGame>> startedGames =
			//new() { DefaultValueFactory = () => new List<TeamGame>() };
			HandlerState.Get(typeof(GameHandler), nameof(startedGames), () => new List<TeamGame>());

		private static readonly GuildDictionary<List<MembersParty>> parties =
			new() { DefaultValueFactory = () => new List<MembersParty>() };
			//HandlerState.Get(typeof(GameHandler), nameof(parties), () => new List<TeamGame>());


		public GameHandler()
		{
			foreach (var l in startedGames)
			{
				UpdateReports(l.Key);

				foreach (var game in l.Value)
				{
					game.Started += StartedGameHandler;
					game.Finished += FinishedGameHandler;
					game.Canceled += CanceledGameHandler;

					game.MembersWait = MembersWaitPredicate;
					game.GameEndWait = EndWaitPredicate;

					game.LaunchWaitTask();
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
			startedGames[ctx].Add(game);

			game.MembersWait = MembersWaitPredicate;
			game.GameEndWait = EndWaitPredicate;

			game.Started += StartedGameHandler;
			game.Finished += FinishedGameHandler;
			game.Canceled += CanceledGameHandler;

			UpdateReports(ctx.Guild);

			game.MembersWaitTask.Start();

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

		[Command("change-game-inv")]
		public Task ChangeInvited(CommandContext ctx, string name, params DiscordMember[] invited)
		{
			var game = GetGame(ctx, name);
			if (game == null) return Task.CompletedTask;

			game.Invited = invited;
			game.RequestReportMessageUpdate();
			UpdateReports(game.Guild);
			return Task.CompletedTask;
		}

		[Command("invite-togame")]
		public Task AddInvited(CommandContext ctx, string name, params DiscordMember[] invited)
		{
			var game = GetGame(ctx, name);
			if (game == null) return Task.CompletedTask;

			game.Invited.AddRange(invited);
			game.RequestReportMessageUpdate();
			UpdateReports(game.Guild);
			return Task.CompletedTask;
		}

		[Command("send-game-invs")]
		public Task SendInvites(CommandContext ctx, string gameName)
		{
			var game = GetGame(ctx, gameName);
			if (game == null) return Task.CompletedTask;

			foreach (var member in game.Invited)
			{
				member.CreateDmChannelAsync().Result.SendMessageAsync
					($"Вы были приглашены на игру в {game.GameName} на сервере {game.Guild.Name} от {game.Creator.Mention}");
			}

			return Task.CompletedTask;
		}

		[Command("invite-party")]
		public Task AddPatryToInvited(CommandContext ctx, string gameName, string partyName)
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
		public Task CreateParty(CommandContext ctx, string name, params DiscordMember[] members)
		{
			if(parties[ctx].Any(s => s.Name == name))
			{
				ctx.RespondAsync("Пати с таким именем уже сушествует").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			var party = new MembersParty(ctx.Member, name);
			party.Members.AddRange(members);
			party.Members.Add(party.Creator);

			parties[ctx].Add(party);
			ctx.RespondAsync("Пати успешно создано").TryDeleteAfter(8000);
			return Task.CompletedTask;
		}

		[Command("delete-party")]
		public Task DeleteParty(CommandContext ctx, string name)
		{
			var party = GetPartyPrivate(ctx, name);
			if (party == null) return Task.CompletedTask;

			parties[ctx].Remove(party);
			ctx.RespondAsync("Пати успешно удалён").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[Command("list-parties")]
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
		public Task ListParty(CommandContext ctx, string partyName)
		{
			var party = GetParty(ctx, partyName);
			if (party == null) return Task.CompletedTask;

			var fieldVal = string.Join(", ", party.Members.Select(s => s.Mention));

			var builder = new DiscordEmbedBuilder();

			ctx.RespondAsync(builder).TryDeleteAfter(20000);
			return Task.CompletedTask;
		}

		[Command("join-party-req")]
		public Task SendJoinRequest(CommandContext ctx, string partyName)
		{
			var party = GetParty(ctx, partyName);
			if (party == null) return Task.CompletedTask;

			party.Creator.CreateDmChannelAsync().Result.SendMessageAsync
				($"Отправлен запрос на присоединение к пати {party.Name} на сервере {party.Creator.Guild.Name} от {ctx.Member.Mention}");
			return Task.CompletedTask;
		}

		[Command("kick-party")]
		public Task KickPartyMember(CommandContext ctx, string partyName, DiscordMember member)
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
				ctx.RespondAsync("Участник уже нет в этом пати").TryDeleteAfter(8000);
			}

			return Task.CompletedTask;
		}

		[Command("join-party")]
		public Task JoinMember(CommandContext ctx, string partyName, DiscordMember member)
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
			var channel = reportChannel[guild];

			var nonDel = startedGames[guild].Select(s => s.ReportMessage).ToList();
			var dic = startedGames[guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);

			var msgs = channel.GetMessagesAsync(1000).Result;
			var toDel = msgs.Where(s => !nonDel.Contains(s));
			foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }

			foreach (var game in startedGames[guild])
			{
				if (!game.NeedReportUpdate) continue;

				lock(game.MsgSyncRoot)
				{
					game.ReportMessage?.DeleteAsync().Wait();
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

					game.ReportMessage = channel.SendMessageAsync(builder.Build()).Result;

					if (game.State == TeamGame.GameState.Created)
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

		private void StartedGameHandler(TeamGame game)
		{
			foreach (var member in game.TeamMembers)
				member.CreateDmChannelAsync().Result.SendMessageAsync($"Игра в {game.GameName} от {game.Creator.Mention} началась.");

			var overs = new DiscordOverwriteBuilder[] { new DiscordOverwriteBuilder().For(game.Creator).Allow(DSharpPlus.Permissions.All) };
			game.CreatedVoice = game.Guild.CreateChannelAsync("Игра в " + game.GameName, DSharpPlus.ChannelType.Voice, voiceCreationCategory[game.Guild], overwrites: overs).Result;

			var members = game.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":ok_hand:")).Result.Where(s => !s.IsBot);
			game.TeamMembers.AddRange(members.Select(s => game.Guild.GetMemberAsync(s.Id).Result).ToList());

			UpdateReports(game.Guild);
		}

		private void FinishedGameHandler(TeamGame game)
		{
			game.CreatedVoice.DeleteAsync();
			startedGames[game.Guild].Remove(game);
			UpdateReports(game.Guild);
		}

		private void CanceledGameHandler(TeamGame game)
		{
			startedGames[game.Guild].Remove(game);
			UpdateReports(game.Guild);
		}

		private bool EndWaitPredicate(TeamGame game)
		{
			lock(game.MsgSyncRoot)
			{
				return game.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":x:")).Result.Where(s => s == game.Creator).Any();
			}
		}

		private bool MembersWaitPredicate(TeamGame game)
		{
			lock (game.MsgSyncRoot)
			{
				if (!game.IsWaitingForCreator)
				{
					var members = game.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":ok_hand:")).Result.Where(s => !s.IsBot);

					bool tr = false;
					if (members.Count() >= game.TargetMembersCount)
					{
						if (game.ReqAllInvited) { if (members.Intersect(game.Invited).SequenceEqual(game.Invited)) tr = true; }
						else tr = true;
					}

					if (tr)
					{
						game.IsWaitingForCreator = true;
						game.Creator.CreateDmChannelAsync().Result.SendMessageAsync($"Ваша игра в {game.GameName} готова к запуску");
					}

					return false;
				}
				else
				{
					return game.ReportMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":arrow_forward:")).Result.Where(s => s == game.Creator).Any();
				}
			}
		}
	}
}
