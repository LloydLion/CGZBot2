using CGZBot2.Entities;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	class ReputationHandler : BaseCommandModule
	{
		//private static readonly GuildDictionary<List<ReputationRole>> rpRoles =
		//	BotSettings.Load<List<ReputationRole>>(typeof(ReputationHandler), nameof(rpRoles));

		private readonly GuildDictionary<List<MemberReputation>> reputation =
			new() { DefaultValueFactory = () => new List<MemberReputation>() };

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
				//Desrising logic
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
		public Task PrintReputaion(CommandContext ctx, DiscordMember member = null)
		{
			if (member == null) member = ctx.Member;
			var rp = GetReputation(member);

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
			var mems = reputation[member.Guild].Where(s => s.Member == member).ToList();

			if(mems.Count == 0)
			{
				var rp = new MemberReputation(member);
				reputation[member.Guild].Add(rp);
				return rp;
			}

			if (mems.Count > 1) throw new Exception("Ce Pi**ec");
			return mems.Single();
		}

		private void ActionHandler(DiscordMember member, ActionType action)
		{
			var rp = rpGive[action];
			var rpobj = GetReputation(member);

			if (rp < 0) rpobj.RevokeReputation(rp);
			else if(rp > 0) rpobj.GiveReputation(rp);

			//HandlerState.Set(typeof(ReputationHandler), nameof(reputation), reputation);
		}
	}
}
