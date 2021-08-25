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
		private StreamState startState = StreamState.Announced;
		private bool requestedRpUpdate;
		private StateMachine<StreamState> stateMachine = new();


		public AnnouncedStream(string name, DiscordMember creator, DateTime startDate,
			string place, StreamingPlaceType placeType)
		{
			Name = name;
			Creator = creator;
			StartDate = startDate;
			Place = place;
			PlaceType = placeType;

			stateMachine.CreateTransit(new TaskTransitWorker<StreamState>(CreateTransitTask(() => StartWait), true), StreamState.Announced, StreamState.WaitingForStreamer);
			stateMachine.CreateTransit(new TaskTransitWorker<StreamState>(CreateTransitTask(() => StreamerWait), true), StreamState.WaitingForStreamer, StreamState.Running);
			stateMachine.CreateTransit(new TaskTransitWorker<StreamState>(CreateTransitTask(() => StreamEndWait), true), StreamState.Running, StreamState.Finished);

			stateMachine.OnStateChangedTo(m => { WaitingForStreamer?.Invoke(this); }, StreamState.WaitingForStreamer);
			stateMachine.OnStateChangedTo(m => { Started?.Invoke(this); realStartDate = DateTime.Now; }, StreamState.Running);
			stateMachine.OnStateChangedTo(m => { Finished?.Invoke(this); finishDate = DateTime.Now; }, StreamState.Finished);
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

		public Predicate<AnnouncedStream> StartWait { get; set; }

		public Predicate<AnnouncedStream> StreamerWait { get; set; }

		public Predicate<AnnouncedStream> StreamEndWait { get; set; }

		public StreamState State { get => stateMachine.CurrentState; init => startState = value; }

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
			stateMachine.Run(startState);
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


		private Func<Task> CreateTransitTask(Func<Predicate<AnnouncedStream>> getPredicate)
		{
			return () => new Task(() =>
			{
				bool t;

				do
				{
					if (State == StreamState.Canceled) throw new Exception();
					Thread.Sleep(1000);
					try { t = !getPredicate()(this); } catch (Exception) { t = true; }
				} while (t);
			});
		}
	}
}
