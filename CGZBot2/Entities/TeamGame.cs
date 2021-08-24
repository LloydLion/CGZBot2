using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CGZBot2.Entities
{
	class TeamGame
	{
		private readonly List<DiscordMember> members = new();
		private readonly List<DiscordMember> invited = new();
		private DateTime? startDate;
		private DateTime? finishDate;
		private bool requestedRpUpdate;
		private GameState state = GameState.Created;


		public TeamGame(DiscordMember creator, string name, string description, int membersCount)
		{
			Creator = creator;
			GameName = name;
			Description = description;
			TargetMembersCount = membersCount;

			MembersWaitTask = CreateTransitTask(() => MembersWait, GameState.Running);
			MembersWaitTask.ContinueWith(s => { if (State == GameState.Canceled) return; startDate = DateTime.Now; Started?.Invoke(this); GameEndWaitTask.Start(); });
			GameEndWaitTask = CreateTransitTask(() => GameEndWait, GameState.Finished);
			GameEndWaitTask.ContinueWith(s => { if (State == GameState.Canceled) return; finishDate = DateTime.Now; Finished?.Invoke(this); });
		}


		public event Action<TeamGame> Started;

		public event Action<TeamGame> Finished;

		public event Action<TeamGame> Canceled;


		public DiscordMember Creator { get; }

		public DiscordChannel CreatedVoice { get; set; }

		public object MsgSyncRoot { get; } = new object();

		public DiscordMessage ReportMessage { get; set; }

		public GameState? ReportMessageType { get; set; }

		public bool ReqAllInvited { get; set; }

		public bool IsWaitingForCreator { get; set; }

		public ICollection<DiscordMember> TeamMembers { get => members; init => ((IReadOnlyCollection<DiscordMember>)value).CopyTo(members); }

		public string GameName { get; set; }

		public string Description { get; set; }

		public int TargetMembersCount { get; set; }

		public ICollection<DiscordMember> Invited { get => invited; set { invited.Clear(); ((IReadOnlyCollection<DiscordMember>)value).CopyTo(invited); } }

		public Task MembersWaitTask { get; }

		public Task GameEndWaitTask { get; }

		public Predicate<TeamGame> MembersWait { get; set; }

		public Predicate<TeamGame> GameEndWait { get; set; }

		public bool NeedReportUpdate => State != ReportMessageType || requestedRpUpdate;

		public GameState State { get => state; init => state = value; }

		public DateTime CreationDate { get; init; } = DateTime.Now;

		public DateTime? StartDate { get => startDate; init => startDate = value; }

		public DateTime? FinishDate { get => finishDate; init => finishDate = value; }

		public DiscordGuild Guild => Creator.Guild;


		[Flags]
		public enum GameState
		{
			Created = 0b1000,
			Running = 0b0100,
			Finished = 0b0010,
			Canceled = 0b0011
		}


		public void RequestReportMessageUpdate()
		{
			requestedRpUpdate = true;
		}

		public void ResetReportUpdate()
		{
			requestedRpUpdate = false;
		}

		public void Cancel()
		{
			state = GameState.Canceled;
			Canceled?.Invoke(this);
		}

		public Task LaunchWaitTask()
		{
			switch (State)
			{
				case GameState.Created:
					MembersWaitTask.Start();
					return MembersWaitTask;
				case GameState.Running:
					GameEndWaitTask.Start();
					return GameEndWaitTask;
				case GameState.Finished or GameState.Canceled:
					return null;
				default: throw new Exception("ChZH, KAKOGO HRENA?");
			}
		}

		private Task CreateTransitTask(Func<Predicate<TeamGame>> getPredicate, GameState targetState)
		{
			return new Task(() =>
			{
				do
				{
					Thread.Sleep(1000);
					if (state == GameState.Canceled) return;
				} while (!getPredicate()(this));

				state = targetState;
			});
		}
	}
}
