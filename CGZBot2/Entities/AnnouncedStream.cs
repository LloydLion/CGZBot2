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


		public AnnouncedStream(string name, DiscordMember creator, DateTime startDate,
			string place, StreamingPlaceType placeType)
		{
			Name = name;
			Creator = creator;
			StartDate = startDate;
			Place = place;
			PlaceType = placeType;

			StartWaitTask = CreateTransitTask(StartWait, StreamState.WaitingForStreamer);
			StartWaitTask.ContinueWith(s => { WaitingForStreamer?.Invoke(this); StreamerWaitTask.Start(); });
			StreamerWaitTask = CreateTransitTask(StreamerWait, StreamState.Running);
			StreamerWaitTask.ContinueWith(s => { realStartDate = DateTime.Now; Started?.Invoke(this); StreamEndWaitTask.Start(); });
			StreamEndWaitTask = CreateTransitTask(StreamEndWait, StreamState.Finished);
			StreamEndWaitTask.ContinueWith(s => { finishDate = DateTime.Now; Finished?.Invoke(this); });
		}


		public event Action<AnnouncedStream> WaitingForStreamer;

		public event Action<AnnouncedStream> Started;

		public event Action<AnnouncedStream> Finished;


		public string Name { get; }

		public DiscordMember Creator { get; }

		public DiscordMessage ReportMessage { get; set; }

		public StreamState? ReportMessageType { get; set; }

		public Task StartWaitTask { get; }

		public Task StreamEndWaitTask { get; }

		public Task StreamerWaitTask { get; }

		public string Place { get; }

		public StreamingPlaceType PlaceType { get; }

		public Predicate<AnnouncedStream> StartWait { get; set; }

		public Predicate<AnnouncedStream> StreamerWait { get; set; }

		public Predicate<AnnouncedStream> StreamEndWait { get; set; }

		public StreamState State { get; private set; } = StreamState.Announced;

		public DateTime CreationDate { get; init; } = DateTime.Now;

		public DateTime StartDate { get; init; }

		public DateTime? RealStartDate { get => realStartDate; init => realStartDate = value; }

		public DateTime? FinishDate { get => finishDate; init => finishDate = value; }

		public DiscordGuild Guild => Creator.Guild;


		public Task LaunchWaitTask()
		{
			switch(State)
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
				case StreamState.Finished:
					return null;
				default: throw new Exception("ChZH, KAKOGO HRENA?");
			}
		}


		public enum StreamingPlaceType
		{
			Discord,
			Internet
		}

		[Flags]
		public enum StreamState
		{
			Announced = 0b1000,
			WaitingForStreamer = 0b1100,
			Running = 0b0010,
			Finished = 0b0001
		}


		private Task CreateTransitTask(Predicate<AnnouncedStream> predicate, StreamState targetState)
		{
			return new Task(() => { while (!predicate(this)) Thread.Sleep(1000); State = targetState; });
		}
	}
}
