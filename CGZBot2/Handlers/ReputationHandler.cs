using CGZBot2.Entities;
using DSharpPlus;
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
	[Description("Репутация")]
	class ReputationHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<List<ReputationRole>> rpRoles =
			BotSettings.Load<List<ReputationRole>>(typeof(ReputationHandler), nameof(rpRoles));

		private readonly GuildDictionary<List<MemberReputation>> reputation =
			HandlerState.Get(typeof(ReputationHandler), nameof(reputation), () => new List<MemberReputation>());

		private static readonly Dictionary<ActionType, int> rpGive = new()
		{
			{ ActionType.SendMessage, 2 },
			{ ActionType.SendAttachment, 8 },
			{ ActionType.StartGame, 20 },
			{ ActionType.AnnounceStream, 40 },
			{ ActionType.CreateDisscussion, 30 },
			{ ActionType.CreateVoiceChannel, 14 },
			{ ActionType.CreateParty, 20 },
			{ ActionType.MemberMute, -25 },
		};

		private readonly Task withTimeRpDecrisingTask;


		public ReputationHandler()
		{
			foreach (var l in reputation)
			{
				foreach (var rp in l.Value)
				{
					rp.LevelChanged += ReputationLevelChanged;
				}
			}


			Program.Client.MessageCreated += (s, a) =>
			{
				if (a.Guild != null)
				{
					ActionHandler(a.Guild.GetMemberAsync(a.Author.Id).Result, ActionType.SendMessage);

					if (a.Message.Attachments.Any()) ActionHandler(a.Guild.GetMemberAsync(a.Author.Id).Result, ActionType.SendAttachment);
				}

				return Task.CompletedTask;
			};

			DiscussionHandler.DiscussionCreated += (a, c) => ActionHandler(c, ActionType.CreateDisscussion);
			GameHandler.GameCreated += (a) => ActionHandler(a.Creator, ActionType.StartGame);
			GameHandler.PartyCreated += (a) => ActionHandler(a.Creator, ActionType.CreateParty);
			MuteHandler.MemberMuted += (a) => ActionHandler(a.Member, ActionType.MemberMute);
			StreamingHandler.StreamCreated += (a) => ActionHandler(a.Creator, ActionType.AnnounceStream);
			VoiceHandler.ChannelCreated += (a) => ActionHandler(a.Creator, ActionType.CreateVoiceChannel);

			withTimeRpDecrisingTask = new Task
			(() => {
				while(true)
				{
					Thread.Sleep(new TimeSpan(6, 0, 0));

					foreach (var l in reputation)
					{
						foreach (var member in l.Value)
						{
							member.RevokeReputation(20);
							HandlerState.Set(typeof(ReputationHandler), nameof(reputation), reputation);
						}
					}
				}
			});
		}


		private enum ActionType
		{
			SendMessage,
			SendAttachment,
			AnnounceStream,
			StartGame,
			CreateDisscussion,
			CreateVoiceChannel,
			CreateParty,
			MemberMute,
		}


		[Command("reputation")]
		[Description("Показывает репутацию участника")]
		public Task PrintReputaion(CommandContext ctx,
			[Description("Участник (по умолчанию - вы)")] DiscordMember member = null)
		{
			if (member == null) member = ctx.Member;
			var rp = GetReputation(member);

			if(rp == null)
			{
				ctx.RespondAsync("Для ботов и администраторов сервера подщёт репутации не производится").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			var builder = new DiscordEmbedBuilder();

			builder
				.WithTitle("Репутация")
				.WithAuthor(member.DisplayName, iconUrl: member.AvatarUrl)
				.WithTimestamp(DateTime.Now)
				.WithColor(DiscordColor.Chartreuse)
				.AddField("Уровень", rp.Level.ToString())
				.AddField("Репутация на уровне", $"{rp.ReputationOnLevel}/{rp.NextLevelCost}")
				.AddField("Всего репутации", rp.TotalReputation.ToString());

			ctx.RespondAsync(builder).TryDeleteAfter(20000);

			return Task.CompletedTask;
		}

		private MemberReputation GetReputation(DiscordMember member)
		{
			if (member.IsBot || member.Permissions.HasPermission(Permissions.Administrator)) return null;

			lock (reputation)
			{
				var mems = reputation[member.Guild].Where(s => s.Member == member).ToList();

				if (mems.Count == 0)
				{
					var rp = new MemberReputation(member);

					rp.LevelChanged += ReputationLevelChanged;

					reputation[member.Guild].Add(rp);
					return rp;
				}

				if (mems.Count > 1) throw new Exception("Ce Pi**ec");
				return mems.Single();
			}
		}

		private void ReputationLevelChanged(MemberReputation rp, int levelChange)
		{
			var member = rp.Member;
			var troles = rpRoles[rp.Guild].Where(s => s.TargetLevel <= rp.Level).Select(s => s.Role.Origin).ToList();
			var roles = member.Roles.Intersect(rpRoles[rp.Guild].Select(s => s.Role.Origin));

			foreach (var role in roles)
				if (!troles.Contains(role))
					member.RevokeRoleAsync(role).Wait();

			foreach (var role in troles)
				if (!roles.Contains(role))
					member.GrantRoleAsync(role).Wait();
		}

		private void ActionHandler(DiscordMember member, ActionType action)
		{
			var rp = rpGive[action];
			var rpobj = GetReputation(member);
			if (rpobj == null) return;

			if (rp < 0) rpobj.RevokeReputation(-rp);
			else if(rp > 0) rpobj.GiveReputation(rp);

			HandlerState.Set(typeof(ReputationHandler), nameof(reputation), reputation);
		}
	}
}
