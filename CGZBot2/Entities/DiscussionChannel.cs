using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class DiscussionChannel
	{
		public DiscussionChannel(DiscordChannel channel)
		{
			Channel = channel;

			ConfirmWaitTask = new Task(() => { while (!ConfirmWait(this)) { Thread.Sleep(1000); if (IsDeleted) return; } IsConfirmed = true; Confirmed?.Invoke(this); });
		}


		public event Action<DiscussionChannel> Confirmed;


		public DiscordChannel Channel { get; }

		public DiscordMessage ConfirmMessage { get; set; }

		public bool IsConfirmed { get; private set; }

		public bool IsDeleted { get; private set; }

		public Task ConfirmWaitTask { get; }

		public DiscordChannel Category => Channel.Parent;

		public string Name => Channel.Name;

		public Predicate<DiscussionChannel> ConfirmWait { get; set; }

		public DiscordGuild Guild => Channel.Guild;


		public void Delete(bool needDeleteDcChannel = true)
		{
			IsDeleted = true;
			if(needDeleteDcChannel) Channel.DeleteAsync();
		}
	}
}
