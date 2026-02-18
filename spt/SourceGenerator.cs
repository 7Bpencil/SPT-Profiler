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
			if (assembly == null)
			{
				return;
			}

			var resultMethods = new List<TypeMethod>();
			foreach (var assemblyType in assembly.GetTypes())
			{
				// do not include structs and generic classes
				if (assemblyType.IsClass && !assemblyType.ContainsGenericParameters)
				{
					foreach (var method in assemblyType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
					{
						if ((method.Name == "Update" || method.Name == "LateUpdate")
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
			builder.AppendLine("using Comfort.Common;");
			builder.AppendLine("using SPT.Reflection.Patching;");
			builder.AppendLine("using System;");
			builder.AppendLine("using System.Collections.Generic;");
			builder.AppendLine("using System.Diagnostics;");
			builder.AppendLine("using System.Reflection;");
			builder.AppendLine("using System.Linq;");
			builder.AppendLine("using HarmonyLib;");
			builder.AppendLine("namespace SevenBoldPencil.Profiler");
			builder.AppendLine("{");
			builder.AppendLine("public static class Generated");
			builder.AppendLine("{");

			builder.AppendLine("public static int GetProfilersCount()");
			builder.AppendLine("{");
			builder.AppendLine($"return {resultMethods.Count};");
			builder.AppendLine("}");

			builder.AppendLine("public static void AppendProfilers(List<IProfiler> profilers)");
			builder.AppendLine("{");
			foreach (var method in resultMethods)
			{
				builder.AppendLine($"profilers.Add(new Measure_{method.MethodName}<{method.TypeName}>());");
			}
			builder.AppendLine("}");

			builder.AppendLine("}");
			builder.AppendLine("}");

			var result = builder.ToString();
			File.WriteAllText("Generated.cs", result);
		}
	}
}
