using CGZBot2.Entities;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	class DiscussionHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<List<DiscordChannel>> categories =
			BotSettings.Load<List<DiscordChannel>>(typeof(DiscussionHandler), nameof(categories));

		private readonly GuildDictionary<List<DiscussionChannel>> channels =
			//new() { DefaultValueFactory = () => new List<DiscussionChannel>() };
			HandlerState.Get(typeof(DiscussionHandler), nameof(channels), () => new List<DiscussionChannel>());


		public DiscussionHandler()
		{
			Program.Client.ChannelDeleted += OnChannelDeleted;
		}


		[Command("discuss")]
		public Task CreateDiscussion(CommandContext ctx, string name)
		{
			if (CheckDiscussionCategory(ctx.Channel, ctx)) return Task.CompletedTask;

			var dchannel = ctx.Guild.CreateChannelAsync(name, ChannelType.Text, ctx.Channel.Parent).Result;
			var channel = new DiscussionChannel(dchannel);
			channels[ctx].Add(channel);

			channel.ConfirmMessage = channel.Channel.SendMessageAsync
				("Данный канал не подтверждён админстрацией сервера.\nКанал может быть удалён или переименнован в будущем").Result;
			channel.ConfirmMessage.PinAsync().Wait();
			channel.ConfirmMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":white_check_mark:")).Wait();
			channel.ConfirmMessage.CreateReactionAsync(DiscordEmoji.FromName(Program.Client, ":x:")).Wait();

			channel.Confirmed += DisChannelConfirmHandler;
			channel.Deleted += DisChannelDeletedHandler;
			channel.ConfirmWait = ChannelConfirmPredecate;
			channel.ConfirmWaitTask.Start();

			return Task.CompletedTask;
		}

		[Command("close-discuss")]
		public Task CloseDiscussion(CommandContext ctx)
		{
			if (CheckDiscussionCategory(ctx.Channel, ctx)) return Task.CompletedTask;

			var channel = GetDiscussionChannel(ctx.Channel, ctx);
			if (channel == null) return Task.CompletedTask;

			if(!ctx.Member.PermissionsIn(ctx.Channel).HasPermission(Permissions.ManageChannels))
			{
				ctx.RespondAsync("У вас не достаточно прав для этой операции (Управление каналами)").TryDeleteAfter(8000);
				return Task.CompletedTask;
			}

			channel.Delete();

			return Task.CompletedTask;
		}

		[Command("isdiscuss")]
		public Task IsDiscuss(CommandContext ctx)
		{
			if (CheckDiscussionCategory(ctx.Channel, ctx)) return Task.CompletedTask;

			var channel = GetDiscussionChannel(ctx.Channel, ctx);
			if (channel == null) return Task.CompletedTask;

			ctx.RespondAsync("Этот канал **является** дисскусией").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		private void DisChannelConfirmHandler(DiscussionChannel channel)
		{
			channel.ConfirmMessage.DeleteAsync();
			HandlerState.Set(typeof(DiscussionHandler), nameof(channels), channels);
		}

		private void DisChannelDeletedHandler(DiscussionChannel channel)
		{
			HandlerState.Set(typeof(DiscussionHandler), nameof(channels), channels);
		}

		private bool ChannelConfirmPredecate(DiscussionChannel channel)
		{
			if (channel.ConfirmMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":x:")).Result.Where(s => !s.IsBot)
				.Any(s => channel.Guild.GetMemberAsync(s.Id).Result.PermissionsIn(channel.Channel).HasFlag(Permissions.ManageChannels)))
			{
				channels[channel.Guild].Remove(channel);
				channel.Delete();
				return false;
			}

			return channel.ConfirmMessage.GetReactionsAsync(DiscordEmoji.FromName(Program.Client, ":white_check_mark:")).Result.Where(s => !s.IsBot)
				.Any(s => channel.Guild.GetMemberAsync(s.Id).Result.PermissionsIn(channel.Channel).HasFlag(Permissions.ManageChannels));
		}

		private bool CheckDiscussionCategory(DiscordChannel channel, CommandContext ctx = null, DiscordGuild guild = null)
		{
			if (guild == null) guild = ctx.Guild;

			var cats = categories[guild].Where(s => s == channel.Parent).ToArray();

			if (cats.Length == 0)
			{
				ctx?.RespondAsync("Этот канал находится вне категории дисскусий").TryDeleteAfter(8000);
				return true;
			}

			if (cats.Length > 1) throw new Exception("Ce Pi**ec");
			return false;
		}

		private DiscussionChannel GetDiscussionChannel(DiscordChannel channel, CommandContext ctx = null, DiscordGuild guild = null)
		{
			if (guild == null) guild = ctx.Guild;

			var discs = channels[guild].Where(s => s.Channel == channel).ToArray();

			if (discs.Length == 0)
			{
				
				ctx?.RespondAsync("Этот канала не является дисскусией").TryDeleteAfter(8000);
				return null;
			}

			if (discs.Length > 1) throw new Exception("Ce Pi**ec");
			return discs.Single();
		}

		private Task OnChannelDeleted(DiscordClient _, ChannelDeleteEventArgs args)
		{
			if(CheckDiscussionCategory(args.Channel, null, args.Guild)) return Task.CompletedTask;

			var channel = GetDiscussionChannel(args.Channel, null, args.Guild);
			if (channel == null) return Task.CompletedTask;

			channels[channel.Guild].Remove(channel);
			channel.Delete(needDeleteDcChannel: false);
			args.Handled = true;

			return Task.CompletedTask;
		}
	}
}
