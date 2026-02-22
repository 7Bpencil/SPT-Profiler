![](preview/preview.png)

Simple profiler that measures specified methods.<br>
Out of the box configured to measure Update, LateUpdate, FixedUpdate on every MonoBehaviour in Assembly-CSharp.<br>
Check `SourceGenerator.cs` to learn how to track methods you are interested in.<br>

### Installation

1) go into `Escape From Tarkov/Development` and clone project
2) run `dotnet build ./SourceGenerator`
3) launch SPT, `*.g.cs` files will be created in `Escape From Tarkov/Development/SPT-Profiler/Profiler` folder
5) you can remove `SourceGenerator.dll` from `Escape From Tarkov/BepInEx/plugins`, otherwise generator will be run every time
5) run `dotnet build ./Profiler`
6) launch SPT
7) press F12 and select 7Bpencil.Profiler to check available keybinds
