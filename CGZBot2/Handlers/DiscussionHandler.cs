﻿using CGZBot2.Attributes;
using CGZBot2.Entities;
using CGZBot2.Tools;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Handlers
{
	[Description("Дисскусии")]
	class DiscussionHandler : BaseCommandModule
	{
		private static readonly GuildDictionary<List<DiscordChannel>> categories =
			BotSettings.Load<List<DiscordChannel>>(typeof(DiscussionHandler), nameof(categories));

		private readonly GuildDictionary<List<DiscussionChannel>> channels =
			HandlerState.Get(typeof(DiscussionHandler), nameof(channels), () => new List<DiscussionChannel>());


		public static event Action<DiscussionChannel, DiscordMember> DiscussionCreated;


		public DiscussionHandler()
		{
			Program.Client.ChannelDeleted += OnChannelDeleted;

			foreach (var l in channels)
			{
				foreach (var channel in l.Value)
				{
					InitDiscuss(channel);
				}
			}
		}


		[Command("discuss")]
		[Description("Создаёт дисскусию в этой категории")]
		public Task CreateDiscussion(CommandContext ctx,
			[Description("Тема")] string name)
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

			InitDiscuss(channel);
			DiscussionCreated?.Invoke(channel, ctx.Member);

			return Task.CompletedTask;
		}

		[HelpUseLimits(CommandUseLimit.Admins)]
		[Command("close-discuss")]
		[Description("Закрывает дисскусию в которой была написана")]
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
		[Description("Проверяет является ли текущий канал дисскусией")]
		public Task IsDiscuss(CommandContext ctx)
		{
			if (CheckDiscussionCategory(ctx.Channel, ctx)) return Task.CompletedTask;

			var channel = GetDiscussionChannel(ctx.Channel, ctx);
			if (channel == null) return Task.CompletedTask;

			ctx.RespondAsync("Этот канал **является** дисскусией").TryDeleteAfter(8000);

			return Task.CompletedTask;
		}

		public void InitDiscuss(DiscussionChannel discussion)
		{
			discussion.Confirmed += DisChannelConfirmHandler;
			discussion.Rejected += DisChannelRejectedHandler;

			discussion.ConfirmWorker = new TaskTransitWorker<DiscussionChannel.ConfirmState>(waitReaction("✅"));
			discussion.RejectWorker = new TaskTransitWorker<DiscussionChannel.ConfirmState>(waitReaction("❌"));

			discussion.Run();

			Func<Task> waitReaction(string reaction)
			{
				return async () =>
				{
					bool loop = true;
					while (loop)
					{
						var interact = await Program.Client.GetInteractivity().WaitForReactionAsync(s =>
							s.Emoji.Name == reaction && s.Message == discussion.ConfirmMessage &&
							s.Guild.GetMemberAsync(s.User.Id).Result.PermissionsIn(discussion.Channel).HasPermission(Permissions.ManageChannels));
						loop = interact.TimedOut;
					}
				};
			}
		}

		private void DisChannelConfirmHandler(DiscussionChannel channel)
		{
			channel.ConfirmMessage.DeleteAsync();
			channel.ConfirmMessage = null;
			HandlerState.Set(typeof(DiscussionHandler), nameof(channels), channels);
		}

		private void DisChannelRejectedHandler(DiscussionChannel channel)
		{
			channel.Delete();
			channels[channel.Guild].Remove(channel);
			HandlerState.Set(typeof(DiscussionHandler), nameof(channels), channels);
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

			channel.SoftDelete();
			channels[channel.Guild].Remove(channel);
			HandlerState.Set(typeof(DiscussionHandler), nameof(channels), channels);
			args.Handled = true;

			return Task.CompletedTask;
		}
	}
}
