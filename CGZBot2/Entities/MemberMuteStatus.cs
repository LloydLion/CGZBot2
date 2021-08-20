using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class MemberMuteStatus
	{
		private bool isReportUpdateRequested = false;


		public MemberMuteStatus(DiscordMember member, TimeSpan? muteTime, string reason)
		{
			Member = member;
			Reason = reason;
			Timeout = muteTime;

			ClearWaitTask = new Task(() => 
			{
				while ((IsInfinity || DateTime.Now - StartTime <= Timeout.Value) && !IsCleared) Thread.Sleep(1000);
				IsCleared = true; Cleared?.Invoke(this);
			});
		}


		public event Action<MemberMuteStatus> Cleared;


		public DiscordMember Member { get; }

		public string Reason { get; set; }

		public object MsgSyncRoot { get; } = new object();

		public DiscordMessage ReportMessage { get; set; }

		public Task ClearWaitTask { get; }

		public TimeSpan? Timeout { get; set; }

		public bool IsInfinity { get => Timeout == null; }

		public DateTime StartTime { get; init; } = DateTime.Now;

		public DateTime? EndTime => Timeout?.AddTo(StartTime);

		public bool IsCleared { get; private set; }

		public bool NeedUpdateReport => ReportMessage == null || isReportUpdateRequested;

		public DiscordGuild Guild => Member.Guild;


		public void SetInfinityTimeout()
		{
			Timeout = null;
		}

		public void Clear()
		{
			IsCleared = true;
		}

		public void RequestReportMessageUpdate()
		{
			isReportUpdateRequested = true;
		}

		public void ResetReportUpdate()
		{
			isReportUpdateRequested = false;
		}
	}
}
