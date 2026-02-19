using Comfort.Common;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;

namespace SevenBoldPencil.Profiler
{
	public interface IProfiler
	{
		public void Init();
		public string GetName();
		public (long sumDuration, long instances) GetDuration(long time, long validTime);
	}

	public class InstanceData
	{
		public bool IsRunning;
		public long Start;
		public long End;
	}

	// V type arg is used to generate different classes with same T type
	// otherwise T.Method0 and T.Method1 will overwrite each other stopwatches
	public abstract class MethodProfiler<T, V> : ModulePatch, IProfiler
	{
		private readonly string _methodName;
		private readonly string _fullName;

		private static Dictionary<T, InstanceData> _instancesData = new();

		public MethodProfiler(string methodName)
		{
			_methodName = methodName;
			_fullName = $"{typeof(T).Name}.{methodName}";
		}

		public void Init()
		{
			Enable();
		}

		public string GetName()
		{
			return _fullName;
		}

		public (long sumDuration, long instances) GetDuration(long time, long validTime)
		{
			long sum = 0;
			long count = 0;
			foreach (var instanceData in _instancesData.Values)
			{
				if (!instanceData.IsRunning && time - instanceData.End <= validTime)
				{
					sum += instanceData.End - instanceData.Start;
					count += 1;
				}
			}
			return (sum, count);
		}

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(T), _methodName);
        }

		public static bool StartMeasure(T instance)
		{
			if (IsWrongType(instance))
			{
				return true;
			}

			var instanceData = GetInstanceData(instance);
			instanceData.IsRunning = true;
			instanceData.Start = Stopwatch.GetTimestamp();
			return true;
		}

		public static void StopMeasure(T instance)
		{
			if (IsWrongType(instance))
			{
				return;
			}

			var instanceData = GetInstanceData(instance);
			instanceData.IsRunning = false;
			instanceData.End = Stopwatch.GetTimestamp();
		}

		// if we have class A with virtual method
		// and class B that inherits A,
		// then instance of B will be tracked twice.
		// this detects when tracker A receives instance of B
		public static bool IsWrongType(T instance)
		{
			return instance.GetType() != typeof(T);
		}

		public static InstanceData GetInstanceData(T instance)
		{
			if (_instancesData.TryGetValue(instance, out var instanceData))
			{
				return instanceData;
			}

			var newInstanceData = new InstanceData();
			_instancesData.Add(instance, newInstanceData);

			return newInstanceData;
		}
	}
}
