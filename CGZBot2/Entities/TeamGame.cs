using CGZBot2.Tools;
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
		private readonly HashSet<DiscordMember> members = new();
		private readonly HashSet<DiscordMember> invited = new();
		private DateTime? startDate;
		private DateTime? finishDate;
		private bool requestedRpUpdate;
		private readonly StateMachine<GameState> stateMachine = new();


		public TeamGame(DiscordMember creator, string name, string description, int membersCount)
		{
			stateMachine.SetStartState(GameState.Created);

			Creator = creator;
			GameName = name;
			Description = description;
			TargetMembersCount = membersCount;

			stateMachine.OnStateChangedTo(m => { WaitingForMembers?.Invoke(this); }, GameState.Created);
			stateMachine.OnStateChangedTo(m => { WaitingForCreator?.Invoke(this); }, GameState.WaitingForCreator);
			stateMachine.OnStateChangedTo(m => { startDate = DateTime.Now; Started?.Invoke(this); }, GameState.Running);
			stateMachine.OnStateChangedTo(m => { finishDate = DateTime.Now; Finished?.Invoke(this); }, GameState.Finished);
			stateMachine.OnStateChangedTo(m => { Canceled?.Invoke(this); }, GameState.Canceled);
		}


		public event Action<TeamGame> WaitingForCreator;

		public event Action<TeamGame> WaitingForMembers;

		public event Action<TeamGame> Started;

		public event Action<TeamGame> Finished;

		public event Action<TeamGame> Canceled;


		public DiscordMember Creator { get; }

		public DiscordChannel CreatedVoice { get; set; }

		public object SyncRoot => stateMachine.SyncRoot;

		public DiscordMessage ReportMessage { get; set; }

		public GameState? ReportMessageType { get; set; }

		public IStateMachineReporter<GameState> StateMachine => stateMachine;

		public bool ReqAllInvited { get; set; }

		public ISet<DiscordMember> TeamMembers { get => members; init => ((IReadOnlyCollection<DiscordMember>)value).CopyTo(members); }

		public string GameName { get; set; }

		public string Description { get; set; }

		public int TargetMembersCount { get; set; }

		public ISet<DiscordMember> Invited { get => invited; set { invited.Clear(); ((IReadOnlyCollection<DiscordMember>)value).CopyTo(invited); } }

		public ITransitWorker<GameState> MembersWaitWorker { get; set; }

		public ITransitWorker<GameState> CreatorWaitWorker { get; set; }

		public ITransitWorker<GameState> GameEndWorker { get; set; }

		public bool NeedReportUpdate => State != ReportMessageType || requestedRpUpdate;

		public GameState State { get => stateMachine.CurrentState; init => stateMachine.SetStartState(value); }

		public DateTime CreationDate { get; init; } = DateTime.Now;

		public DateTime? StartDate { get => startDate; init => startDate = value; }

		public DateTime? FinishDate { get => finishDate; init => finishDate = value; }

		public DiscordGuild Guild => Creator.Guild;


		[Flags]
		public enum GameState
		{
			Created = 0b10000,
			WaitingForCreator = 0b11000,
			Running = 0b00100,
			Finished = 0b00010,
			Canceled = 0b00011
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
			stateMachine.ChangeStateHard(GameState.Canceled);
		}

		public void Run()
		{
			stateMachine.CreateTransit(MembersWaitWorker, GameState.Created, GameState.WaitingForCreator);
			stateMachine.CreateTransit(new InvertedTransitWorker<GameState>(MembersWaitWorker), GameState.WaitingForCreator, GameState.Created);
			stateMachine.CreateTransit(CreatorWaitWorker, GameState.WaitingForCreator, GameState.Running);
			stateMachine.CreateTransit(GameEndWorker, GameState.Running, GameState.Finished);

			stateMachine.Run();
		}
	}
}
