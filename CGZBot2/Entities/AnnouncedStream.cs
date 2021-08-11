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
		private StreamState state = StreamState.Announced;
		private bool requestedRpUpdate;


		public AnnouncedStream(string name, DiscordMember creator, DateTime startDate,
			string place, StreamingPlaceType placeType)
		{
			Name = name;
			Creator = creator;
			StartDate = startDate;
			Place = place;
			PlaceType = placeType;

			StartWaitTask = CreateTransitTask(() => StartWait, StreamState.WaitingForStreamer);
			StartWaitTask.ContinueWith(s => { if (state == StreamState.Canceled) return; WaitingForStreamer?.Invoke(this); StreamerWaitTask.Start(); });
			StreamerWaitTask = CreateTransitTask(() => StreamerWait, StreamState.Running);
			StreamerWaitTask.ContinueWith(s => { if (state == StreamState.Canceled) return; realStartDate = DateTime.Now; Started?.Invoke(this); StreamEndWaitTask.Start(); });
			StreamEndWaitTask = CreateTransitTask(() => StreamEndWait, StreamState.Finished);
			StreamEndWaitTask.ContinueWith(s => { if (state == StreamState.Canceled) return; finishDate = DateTime.Now; Finished?.Invoke(this); });
		}


		public event Action<AnnouncedStream> WaitingForStreamer;

		public event Action<AnnouncedStream> Started;

		public event Action<AnnouncedStream> Finished;

		public event Action<AnnouncedStream> Canceled;


		public string Name { get; set; }

		public object MsgSyncRoot { get; } = new object();

		public DiscordMember Creator { get; }

		public DiscordMessage ReportMessage { get; set; }

		public StreamState? ReportMessageType { get; set; }

		public Task StartWaitTask { get; }

		public Task StreamEndWaitTask { get; }

		public Task StreamerWaitTask { get; }

		public string Place { get; set; }

		public StreamingPlaceType PlaceType { get; set; }

		public Predicate<AnnouncedStream> StartWait { get; set; }

		public Predicate<AnnouncedStream> StreamerWait { get; set; }

		public Predicate<AnnouncedStream> StreamEndWait { get; set; }

		public StreamState State { get => state; init => state = value; }

		public DateTime CreationDate { get; init; } = DateTime.Now;

		public DateTime StartDate { get; set; }

		public DateTime? RealStartDate { get => realStartDate; init => realStartDate = value; }

		public DateTime? FinishDate { get => finishDate; init => finishDate = value; }

		public bool NeedReportUpdate => State != ReportMessageType || requestedRpUpdate;

		public DiscordGuild Guild => Creator.Guild;


		public Task LaunchWaitTask()
		{
			switch (State)
			{
				case StreamState.Announced:
					StartWaitTask.Start();
					return StartWaitTask;
				case StreamState.WaitingForStreamer:
					StreamerWaitTask.Start();
					return StreamerWaitTask;
				case StreamState.Running:
					StreamEndWaitTask.Start();
					return StreamEndWaitTask;
				case StreamState.Finished or StreamState.Canceled:
					return null;
				default: throw new Exception("ChZH, KAKOGO HRENA?");
			}
		}

		public void Cancel()
		{
			state = StreamState.Canceled;
			Canceled?.Invoke(this);
		}

		public void RequestReportMessageUpdate()
		{
			requestedRpUpdate = true;
		}

		public void ResetReportUpdate()
		{
			requestedRpUpdate = false;
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


		private Task CreateTransitTask(Func<Predicate<AnnouncedStream>> getPredicate, StreamState targetState)
		{
			return new Task(() =>
			{
				do
				{
					if (state == StreamState.Canceled) return;
					Thread.Sleep(1000);
				} while (!getPredicate()(this));

				state = targetState; 
			});
		}
	}
}
