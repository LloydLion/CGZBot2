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
	class DiscussionChannel
	{
		private readonly StateMachine<ConfirmState> stateMachine = new();
		private ConfirmState startState = ConfirmState.Undetermined;


		public DiscussionChannel(DiscordChannel channel)
		{
			Channel = channel;

			stateMachine.OnStateChangedTo(s => Confirmed?.Invoke(this), ConfirmState.Confirmed);
			stateMachine.OnStateChangedTo(s => Rejected?.Invoke(this), ConfirmState.Deleted);
		}


		public event Action<DiscussionChannel> Confirmed;

		public event Action<DiscussionChannel> Rejected;


		public DiscordChannel Channel { get; }

		public DiscordMessage ConfirmMessage { get; set; }

		public DiscordChannel Category => Channel.Parent;

		public string Name => Channel.Name;

		public ITransitWorker<ConfirmState> ConfirmWorker { get; set; }

		public ITransitWorker<ConfirmState> RejectWorker { get; set; }

		public DiscordGuild Guild => Channel.Guild;

		public IStateMachineReporter<ConfirmState> StateMachine => stateMachine;

		public ConfirmState State { get => stateMachine.CurrentState; init => startState = value; }


		public void Delete()
		{
			Channel.DeleteAsync();
			SoftDelete();
		}

		public void SoftDelete()
		{
			stateMachine.ChangeStateHard(ConfirmState.Deleted);
		}

		public void Run()
		{
			stateMachine.CreateTransit(ConfirmWorker, ConfirmState.Undetermined, ConfirmState.Confirmed);
			stateMachine.CreateTransit(RejectWorker, ConfirmState.Undetermined, ConfirmState.Deleted);

			stateMachine.Run(startState);
		}


		public enum ConfirmState
		{
			Undetermined,
			Confirmed,
			Deleted
		}
	}
}
