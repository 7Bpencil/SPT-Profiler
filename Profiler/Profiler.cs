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
	// otherwise methods T.MethodA and T.MethodB will overwrite each other stopwatches
	public abstract class MethodProfiler<T, V> : ModulePatch, IProfiler
	{
		private static string _methodName;
		private static string _fullName;
		private static bool _isStaticMethod;

		private static Dictionary<T, InstanceData> _instancesData;
		private static InstanceData _staticInstanceData;

		public MethodProfiler(string methodName)
		{
			_methodName = methodName;
			_fullName = $"{typeof(T).Name}.{methodName}";
		}

        protected override MethodBase GetTargetMethod()
        {
			var targetMethod = AccessTools.Method(typeof(T), _methodName);
			_isStaticMethod = targetMethod.IsStatic;

			if (_isStaticMethod)
			{
				_staticInstanceData = new();
			}
			else
			{
				_instancesData = new();
			}

            return targetMethod;
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

			void ProcessInstance(InstanceData instanceData)
			{
				if (!instanceData.IsRunning && time - instanceData.End <= validTime)
				{
					sum += instanceData.End - instanceData.Start;
					count += 1;
				}
			}

			if (_isStaticMethod)
			{
				ProcessInstance(_staticInstanceData);
			}
			else
			{
				foreach (var instanceData in _instancesData.Values)
				{
					ProcessInstance(instanceData);
				}
			}

			return (sum, count);
		}

		public static bool StartMeasure(T instance)
		{
			if (!IsCorrectType(instance))
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
			if (!IsCorrectType(instance))
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
		public static bool IsCorrectType(T instance)
		{
			return _isStaticMethod || instance.GetType() == typeof(T);
		}

		public static InstanceData GetInstanceData(T instance)
		{
			if (_isStaticMethod)
			{
				return _staticInstanceData;
			}
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
