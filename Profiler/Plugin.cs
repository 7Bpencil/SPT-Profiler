using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Diagnostics;
using UnityEngine;
using System.Collections.Generic;

namespace SevenBoldPencil.Profiler {
	[DefaultExecutionOrder(int.MaxValue)]
    [BepInPlugin("7Bpencil.Profiler", "7Bpencil.Profiler", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        public static Plugin Instance;

        public static ConfigEntry<KeyboardShortcut> HideShowWindow;
        public static ConfigEntry<KeyboardShortcut> PauseContinueRecording;

		public ManualLogSource LoggerInstance;
		private List<ProfilersGroup> profilersGroups;
		private bool isProfilerWindowVisible;
		private bool isProfilerRecording;

        private void Awake() {
			var section = "Main";
            HideShowWindow = Config.Bind(section, "Hide/Show Window", new KeyboardShortcut(KeyCode.F4), "Hide/Show Window");
            PauseContinueRecording = Config.Bind(section, "Pause/Continue Recording", new KeyboardShortcut(KeyCode.F5), "Pause/Continue Recording");

			isProfilerWindowVisible = true;
			isProfilerRecording = true;

            Instance = this;
			LoggerInstance = Logger;
			profilersGroups = Profilers.GetGroups();
        }

		public void Update()
		{
			if (Input.GetKeyDown(HideShowWindow.Value.MainKey))
			{
				isProfilerWindowVisible = !isProfilerWindowVisible;
			}
			if (Input.GetKeyDown(PauseContinueRecording.Value.MainKey))
			{
				isProfilerRecording = !isProfilerRecording;
			}
		}

		public void LateUpdate()
		{
			if (isProfilerWindowVisible && isProfilerRecording)
			{
				foreach (var profilersGroup in profilersGroups)
				{
					profilersGroup.CollectMeasurements();
				}
			}
			else
			{
				foreach (var profilersGroup in profilersGroups)
				{
					profilersGroup.ClearMeasurements();
				}
			}
		}

		public void OnGUI()
        {
			if (isProfilerWindowVisible)
			{
				for (var i = 0; i < profilersGroups.Count; i++)
				{
					var profilersGroup = profilersGroups[i];
					profilersGroup.DrawWindow(i);
				}
			}
        }
    }

	public class ProfilersGroup
	{
		private struct Measurement
		{
			public string Name;
			public double TimeMs;
			public long Calls;
		}

		private string name;
		private List<IProfiler> profilers;
		private List<Measurement> measurements;
		private Rect windowRect;

		private const int maxVisibleAmount = 25;
		private const float windowWidth = 400f;
		private const float headerHeight = 15f;
		private const float startX = 15f;
		private const float startY = headerHeight + 15f + separatorY;
		private const float height = 20f;
		private const float separatorX = 15f;
		private const float separatorY = 2.5f;

		public ProfilersGroup(string name, List<IProfiler> profilers)
		{
			this.name = name;
			this.profilers = profilers;
			this.measurements = new List<Measurement>(profilers.Count);
			foreach (var profiler in profilers)
			{
				profiler.Init();
			}
		}

		public void CollectMeasurements()
		{
			measurements.Clear();

			foreach (var profiler in profilers)
			{
				var (totalDuration, calls) = profiler.CollectMeasurements();
				if (totalDuration == 0)
				{
					continue;
				}

				var name = profiler.GetName();
				var timeMs = GetDurationMilliseconds(totalDuration);
				measurements.Add(new Measurement()
				{
					Name = name,
					TimeMs = timeMs,
					Calls = calls,
				});
			}

			measurements.Sort((a, b) => b.TimeMs.CompareTo(a.TimeMs));
		}

		public void ClearMeasurements()
		{
			foreach (var profiler in profilers)
			{
				var _ = profiler.CollectMeasurements();
			}
		}

		public static double GetDurationSeconds(long durationTicks)
		{
			return (double)durationTicks / Stopwatch.Frequency;
		}

		public static double GetDurationMilliseconds(long durationTicks)
		{
			return GetDurationSeconds(durationTicks) * 1000;
		}

		public static long GetTicksFromMilliseconds(uint milliseconds)
		{
			return (milliseconds * Stopwatch.Frequency) / 1000;
		}

		private int GetVisibleMeasurementsCount()
		{
			return Mathf.Min(measurements.Count, maxVisibleAmount);
		}

		public void DrawWindow(int windowIndex)
		{
			// if width == 0, then windowRect has not been initialized, so init it
			if (windowRect.width == 0f)
			{
				windowRect.width = windowWidth;
				windowRect.x = 50 + windowIndex * (windowWidth + separatorX);
				windowRect.y = 50;
			}

			var visibleAmount = GetVisibleMeasurementsCount();
			var windowHeight = startY + visibleAmount * (height + separatorY) - separatorY + headerHeight;

			windowRect.height = windowHeight;
            windowRect = GUI.Window(windowIndex, windowRect, WindowFunction, $"Profiler {name}");
		}

        private void WindowFunction(int windowID)
		{
			var x = startX;
			var y = startY;

			var visibleAmount = GetVisibleMeasurementsCount();
			for (var i = 0; i < visibleAmount; i++)
			{
				var measurement = measurements[i];
				var text = $"{measurement.TimeMs:0.0000} ms | {measurement.Calls} | {measurement.Name}";
				var rect = new Rect(x, y, windowWidth, height);
				GUI.Label(rect, text);
				y += height + separatorY;
			}

			GUI.DragWindow();
		}
	}
}
