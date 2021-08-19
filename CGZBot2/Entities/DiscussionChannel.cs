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
		private bool isConfirmed;
		private bool isDeleted;


		public DiscussionChannel(DiscordChannel channel)
		{
			Channel = channel;

			ConfirmWaitTask = new Task(() => { while (!ConfirmWait(this)) { Thread.Sleep(1000); if (isDeleted) return; } isConfirmed = true; Confirmed?.Invoke(this); });
		}


		public event Action<DiscussionChannel> Confirmed;

		public event Action<DiscussionChannel> Deleted;


		public DiscordChannel Channel { get; }

		public DiscordMessage ConfirmMessage { get; set; }

		public bool IsConfirmed { get => isConfirmed; init => isConfirmed = value; }

		public bool IsDeleted { get => isDeleted; init => isDeleted = value; }

		public Task ConfirmWaitTask { get; }

		public DiscordChannel Category => Channel.Parent;

		public string Name => Channel.Name;

		public Predicate<DiscussionChannel> ConfirmWait { get; set; }

		public DiscordGuild Guild => Channel.Guild;


		public void Delete(bool needDeleteDcChannel = true)
		{
			isDeleted = true;
			if (needDeleteDcChannel) Channel.DeleteAsync();
			Deleted?.Invoke(this);
		}
	}
}
