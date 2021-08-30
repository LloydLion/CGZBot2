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
	class AnnouncedStream
	{
		private DateTime? realStartDate;
		private DateTime? finishDate;
		private bool requestedRpUpdate;
		private StateMachine<StreamState> stateMachine = new();


		public AnnouncedStream(string name, DiscordMember creator, DateTime startDate,
			string place, StreamingPlaceType placeType)
		{
			stateMachine.SetStartState(StreamState.Announced);

			Name = name;
			Creator = creator;
			StartDate = startDate;
			Place = place;
			PlaceType = placeType;

			stateMachine.OnStateChangedTo(m => { WaitingForStreamer?.Invoke(this); }, StreamState.WaitingForStreamer);
			stateMachine.OnStateChangedTo(m => { realStartDate = DateTime.Now; Started?.Invoke(this); }, StreamState.Running);
			stateMachine.OnStateChangedTo(m => { finishDate = DateTime.Now; Finished?.Invoke(this); }, StreamState.Finished);
			stateMachine.OnStateChangedTo(m => { Canceled?.Invoke(this); }, StreamState.Canceled);
		}


		public event Action<AnnouncedStream> WaitingForStreamer;

		public event Action<AnnouncedStream> Started;

		public event Action<AnnouncedStream> Finished;

		public event Action<AnnouncedStream> Canceled;


		public string Name { get; set; }

		public object SyncRoot => stateMachine.SyncRoot;

		public DiscordMember Creator { get; }

		public DiscordMessage ReportMessage { get; set; }

		public StreamState? ReportMessageType { get; set; }

		public string Place { get; set; }

		public StreamingPlaceType PlaceType { get; set; }

		public ITransitWorker<StreamState>  StartWorker { get; set; }

		public ITransitWorker<StreamState> StreamerWaitWorker { get; set; }

		public ITransitWorker<StreamState> StreamEndWorker { get; set; }

		public StreamState State { get => stateMachine.CurrentState; init => stateMachine.SetStartState(value); }

		public DateTime CreationDate { get; init; } = DateTime.Now;

		public DateTime StartDate { get; set; }

		public DateTime? RealStartDate { get => realStartDate; init => realStartDate = value; }

		public DateTime? FinishDate { get => finishDate; init => finishDate = value; }

		public bool NeedReportUpdate => State != ReportMessageType || requestedRpUpdate;

		public DiscordGuild Guild => Creator.Guild;


		public void Cancel()
		{
			stateMachine.ChangeStateHard(StreamState.Canceled);
			stateMachine.UpdateState();
		}

		public void RequestReportMessageUpdate()
		{
			requestedRpUpdate = true;
		}

		public void ResetReportUpdate()
		{
			requestedRpUpdate = false;
		}

		public void Run()
		{
			stateMachine.CreateTransit(StartWorker, StreamState.Announced, StreamState.WaitingForStreamer);
			stateMachine.CreateTransit(StreamerWaitWorker, StreamState.WaitingForStreamer, StreamState.Running);
			stateMachine.CreateTransit(StreamEndWorker, StreamState.Running, StreamState.Finished);

			stateMachine.Run();
		}


		public enum StreamingPlaceType
		{
			Discord,
			Internet
		}

		[Flags]
		public enum StreamState
		{
			Announced = 0b10000,
			WaitingForStreamer = 0b11000,
			Running = 0b00100,
			Finished = 0b00010,
			Canceled = 0b00011
		}
	}
}
