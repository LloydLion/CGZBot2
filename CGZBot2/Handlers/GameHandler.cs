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


		private static readonly GuildDictionary<List<TeamGame>> startedGames =
			new() { DefaultValueFactory = () => new List<TeamGame>() };
			//HandlerState.Get(typeof(GameHandler), nameof(startedGames), () => new List<TeamGame>());


		public GameHandler()
		{

		}


		[Command("team-game")]
		[Description("Начинает коммандную игру")]
		public async Task StartTeamGame(CommandContext ctx,
			[Description("Имя игры")] string name,
			[Description("Целевое кол-во участников")] int targetMemberCount,
			[Description("Описание игры")] string description = null,
			[Description("Требовать ли присоединения всех приглашённых участников")] bool reqAllInvited = false,
			[Description("Приглашения для участников")] params DiscordMember[] invites)
		{
			var game = new TeamGame(ctx.Member, name, description, targetMemberCount) { Invited = invites, ReqAllInvited = reqAllInvited };
			startedGames[ctx].Add(game);

			game.MembersWait = MembersWaitPredicate;
			game.GameEndWait = EndWaitPredicate;

			game.Started += StartedStreamHandler;
			game.Finished += FinishedStreamHandler;
			game.Canceled += CanceledStreamHandler;

			UpdateReports(ctx.Guild);

			game.MembersWaitTask.Start();
		}

		private void UpdateReports(DiscordGuild guild)
		{
			var channel = reportChannel[guild];

			var nonDel = startedGames[guild].Select(s => s.ReportMessage).ToList();
			var dic = startedGames[guild].Where(s => s.ReportMessage != null).ToDictionary(s => s.ReportMessage);

			var msgs = channel.GetMessagesAsync(1000).Result;
			var toDel = msgs.Where(s => !nonDel.Contains(s) || dic[s].NeedReportUpdate);
			foreach (var msg in toDel) { msg.TryDelete(); Thread.Sleep(50); }

			foreach (var game in startedGames[guild])
			{
				if (!game.NeedReportUpdate) continue;

				var builder = new DiscordEmbedBuilder();

				lock(game.MsgSyncRoot)
				{
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

		private void StartedStreamHandler(TeamGame game)
		{
			UpdateReports(game.Guild);
		}

		private void FinishedStreamHandler(TeamGame game)
		{
			UpdateReports(game.Guild);
		}

		private void CanceledStreamHandler(TeamGame game)
		{
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
						game.AddTeamMemberRange(members.Select(s => game.Guild.GetMemberAsync(s.Id).Result).ToList());
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
