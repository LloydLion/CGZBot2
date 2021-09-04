using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class CreatedVoiceChannel
	{
		private bool hardClose = false;


		public CreatedVoiceChannel(DiscordChannel channel, DiscordMember creator)
		{
			DeleteTask = new Task(() =>
			{
				while (!Program.Connected || (!hardClose && Channel.IsExist() && MemberCount > 0)) Thread.Sleep(1000);
				if (Channel.IsExist()) Channel.TryDelete(); Deleted?.Invoke(this);
			});
			Channel = channel;
			Creator = creator;
		}


		public event Action<CreatedVoiceChannel> Deleted;


		public DiscordChannel Channel { get; }

		public DiscordMember Creator { get; }

		public DiscordMessage ReportMessage { get; set; }

		public Task DeleteTask { get; }

		public int MemberCount => Channel.Users.Count();

		public DateTime CreationDate => Channel.CreationTimestamp.LocalDateTime;

		public string Name => Channel.Name;

		public bool Closed => DeleteTask.IsCompleted;


		public void Close()
		{
			hardClose = true;
		}
	}
}
