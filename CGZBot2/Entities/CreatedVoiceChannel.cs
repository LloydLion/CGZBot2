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
		public CreatedVoiceChannel(DiscordChannel channel, DiscordMember creator)
		{
			DeleteTask = new Task(() => { while (!Program.Connected || MemberCount > 0) Thread.Sleep(100);
				Channel.DeleteAsync(); Deleted?.Invoke(this); });
			Channel = channel;
			Creator = creator;
		}


		public event Action<CreatedVoiceChannel> Deleted;


		public DiscordChannel Channel { get; }

		public DiscordMember Creator { get; }

		public DiscordMessage ReportMessage { get; set; }

		public Task DeleteTask { get; }

		public int MemberCount => Channel.Users.Count();

		public DateTime CreationDate => Channel.CreationTimestamp.UtcDateTime;

		public string Name => Channel.Name;

		public bool Closed => DeleteTask.IsCompleted;
	}
}
