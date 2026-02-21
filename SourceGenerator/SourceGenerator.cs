using Comfort.Common;
using SPT.Reflection.Patching;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SevenBoldPencil.Profiler
{
	public static class SourceGenerator
	{
		public struct TypeMethod
		{
			public string TypeName;
			public string MethodName;
		}

		public static void Generate()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var assemblyName = "Assembly-CSharp";
			var assembly = assemblies.FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);

			// https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.html
			// I tried all events, but only these three are really visible in profiler
			var unityEvents = new HashSet<string>()
			{
				"FixedUpdate",
				"Update",
				"LateUpdate",
			};

			var resultMethods = new List<TypeMethod>();
			foreach (var assemblyType in assembly.GetTypes())
			{
				// do not check structs and ignore generic classes
				if (assemblyType.IsClass && !assemblyType.ContainsGenericParameters && assemblyType.IsSubclassOf(typeof(MonoBehaviour)))
				{
					foreach (var method in assemblyType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
					{
						if (unityEvents.Contains(method.Name)
							&& method.GetParameters().Length == 0
							&& method.GetMethodBody() != null)
						{
							// if class is nested, its name is returned as OuterClass+Class,
							// in that case convert it to OuterClass.Class
							var typeName = assemblyType.FullName.Replace("+", ".");
							resultMethods.Add(new TypeMethod()
							{
								TypeName = typeName,
								MethodName = method.Name,
							});
						}
					}
				}
			}

			var builder = new StringBuilder();
			builder.AppendLine(@"using Comfort.Common;");
			builder.AppendLine(@"using SPT.Reflection.Patching;");
			builder.AppendLine(@"using System;");
			builder.AppendLine(@"using System.Collections.Generic;");
			builder.AppendLine(@"using System.Diagnostics;");
			builder.AppendLine(@"using System.Reflection;");
			builder.AppendLine(@"using System.Linq;");
			builder.AppendLine(@"using HarmonyLib;");

			builder.AppendLine(@"namespace SevenBoldPencil.Profiler");
			builder.AppendLine(@"{");
			builder.AppendLine(@"    public static class Generated");
			builder.AppendLine(@"    {");
			builder.AppendLine(@"        public static List<IProfiler> GetProfilers()");
			builder.AppendLine(@"        {");
			builder.AppendLine($"            var profilers = new List<IProfiler>({resultMethods.Count})");
			builder.AppendLine(@"            {");
		foreach (var method in resultMethods)
		{
			builder.AppendLine($"                new Measure_{method.MethodName}<{method.TypeName}>(),");
		}
			builder.AppendLine(@"            };");
			builder.AppendLine(@"            return profilers;");
			builder.AppendLine(@"        }");
			builder.AppendLine(@"    }");
		foreach (var unityEvent in unityEvents)
		{
			builder.AppendLine($"    public class Dummy_{unityEvent} {{ }}");
			builder.AppendLine($"    public class Measure_{unityEvent}<T> : MethodProfiler<T, Dummy_{unityEvent}>");
			builder.AppendLine(@"    {");
			builder.AppendLine($"        public Measure_{unityEvent}() : base(\"{unityEvent}\") {{ }}");
			builder.AppendLine(@"        [PatchPrefix] public static bool Prefix(T __instance) { return StartMeasure(__instance); }");
			builder.AppendLine(@"        [PatchPostfix] public static void Postfix(T __instance) { StopMeasure(__instance); }");
			builder.AppendLine(@"    }");
		}
			builder.AppendLine(@"}");

			var result = builder.ToString();
			File.WriteAllText("Development/SPT-Profiler/Profiler/Generated.cs", result);
		}
	}
}
