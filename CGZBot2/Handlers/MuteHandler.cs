using CGZBot2.Entities;
using DSharpPlus;
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
	class MuteHandler : BaseCommandModule
	{
		private static GuildDictionary<DiscordChannel> reportChannel =
			BotSettings.Load<DiscordChannel>(typeof(MuteHandler), nameof(reportChannel));
		private static GuildDictionary<DisRole> mutedRole =
			BotSettings.Load<DisRole>(typeof(MuteHandler), nameof(mutedRole));

		private GuildDictionary<List<MemberMuteStatus>> mutes =
			HandlerState.Get(typeof(MuteHandler), nameof(mutes), () => new List<MemberMuteStatus>());
			//new() { DefaultValueFactory = () => new List<MemberMuteStatus>() };


		public MuteHandler()
		{
			foreach (var l in mutes)
			{
				foreach (var mute in l.Value)
				{
					mute.Cleared += MemberMuteClearHandler;
					mute.ClearWaitTask.Start();
				}

				UpdateReports(l.Key);
			}
		}


		[Command("mute")]
		public Task Mute(CommandContext ctx, DiscordMember member, TimeSpan time, string reason = "")
		{
			var mute = GetMute(member);
			if(mute != null)
			{
				ctx.RespondAsync("Этот пользователь уже замьючен").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			mutes[ctx].Add(MuteMember(member, time, reason));
			UpdateReports(ctx.Guild);

			ctx.RespondAsync("Пользователь замьючен").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[Command("mute")]
		public Task Mute(CommandContext ctx, DiscordMember member, string reason = "")
		{
			var mute = GetMute(member);
			if (mute != null)
			{
				ctx.RespondAsync("Этот пользователь уже замьючен").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			mutes[ctx].Add(MuteMember(member, null, reason));
			UpdateReports(ctx.Guild);

			ctx.RespondAsync("Пользователь замьючен").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[Command("unmute")]
		public Task Unmute(CommandContext ctx, DiscordMember member)
		{
			var mute = GetMute(member, ctx);
			if (mute == null) return Task.CompletedTask;

			mute.Clear();

			ctx.RespondAsync("Пользователь размьючен").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		[Command("setup-mute")]
		public Task SetupMuteRole(CommandContext ctx)
		{
			var role = mutedRole[ctx];

			var channels = ctx.Guild.GetChannelsAsync().Result;

			foreach (var channel in channels)
				channel.AddOverwriteAsync(role, deny: Permissions.All ^ 
					(Permissions.AccessChannels | Permissions.Administrator | Permissions.UseVoice | Permissions.ReadMessageHistory)).Wait();

			ctx.RespondAsync("Права для замьюченой роли установлены").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		private void UpdateReports(DiscordGuild guild)
		{
			var muts = mutes[guild];
			var channel = reportChannel[guild];

			var rpMsgs = muts.Select(s => s.ReportMessage).ToList();
			var toDel = channel.GetMessagesAsync().Result.Where(s => !rpMsgs.Contains(s));

			foreach (var msg in toDel) msg.DeleteAsync().Wait();

			foreach (var mut in muts)
			{
				lock (mut.MsgSyncRoot)
				{
					if (!mut.NeedUpdateReport) continue;

					mut.ReportMessage?.DeleteAsync();

					var builder = new DiscordEmbedBuilder();

					builder
						.WithTitle("Участник замьючен")
						.WithColor(DiscordColor.Purple)
						.WithTimestamp(mut.StartTime)
						.WithAuthor(mut.Member.DisplayName, iconUrl: mut.Member.AvatarUrl)
						.AddField("Время мута", mut.Timeout?.ToString() ?? "Бессрочно")
						.AddField("Время окончания", mut.EndTime?.ToString() ?? "Бессрочно")
						.AddField("Причина", string.IsNullOrWhiteSpace(mut.Reason) ? "Не указана" : mut.Reason);

					mut.ReportMessage = channel.SendMessageAsync(builder).Result;
				}
			}
		}

		private MemberMuteStatus GetMute(DiscordMember member, CommandContext ctx = null)
		{
			var discs = mutes[member.Guild].Where(s => s.Member == member).ToArray();

			if (discs.Length == 0)
			{
				ctx?.RespondAsync("Этот пользователь не замьючен").TryDeleteAfter(8000);
				return null;
			}

			if (discs.Length > 1) throw new Exception("Ce Pi**ec");
			return discs.Single();
		}

		private MemberMuteStatus MuteMember(DiscordMember member, TimeSpan? timeout, string reason)
		{
			var role = mutedRole[member.Guild];

			member.GrantRoleAsync(role).Wait();

			var ret = new MemberMuteStatus(member, timeout, reason);
			ret.Cleared += MemberMuteClearHandler;

			ret.ClearWaitTask.Start();
			return ret;
		}

		private void MemberMuteClearHandler(MemberMuteStatus status)
		{
			status.Member.RevokeRoleAsync(mutedRole[status.Guild]).Wait();
			mutes[status.Guild].Remove(status);
			UpdateReports(status.Guild);
			HandlerState.Set(typeof(MuteHandler), nameof(mutes), mutes);
		}
	}
}
