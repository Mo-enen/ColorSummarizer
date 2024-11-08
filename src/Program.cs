using Raylib_cs;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using ColorSummarizer;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Numerics;


// ======== Init ========

string ProjectName = Assembly.GetExecutingAssembly().GetName().Name;

// Config
string SavingFolder = Util.CombinePaths(Environment.GetFolderPath(
	Environment.SpecialFolder.LocalApplicationData), "Moenen", ProjectName
);
Util.CreateFolder(SavingFolder);
string ConfigPath = Util.CombinePaths(SavingFolder, "Config.txt");
int WindowWidth = 1000;
int WindowHeight = 1000;

// Load Config
{
	foreach (string line in Util.ForAllLinesInFile(ConfigPath, Encoding.UTF8)) {
		int cIndex = line.IndexOf(':');
		if (cIndex < 0 || cIndex + 1 >= line.Length) continue;
		if (line.StartsWith("WindowWidth:") && int.TryParse(line[(cIndex + 1)..], out int width)) {
			WindowWidth = width;
			continue;
		}
		if (line.StartsWith("WindowHeight:") && int.TryParse(line[(cIndex + 1)..], out int height)) {
			WindowHeight = height;
			continue;
		}
	}
}
WindowWidth = Math.Clamp(WindowWidth, 200, 4000);
WindowHeight = Math.Clamp(WindowHeight, 200, 4000);

Texture2D CurrentTexture = default;
string ErrorMessage = "";
int GlobalFrame = 0;
int AlertFrame = int.MinValue;
Color[] RequiringColors = null;
int RequiringCountDown = 0;
AccumulateResult CurrentAccumulateResult = default;
GatheringResult CurrentGatheringResultA = default;
GatheringResult CurrentGatheringResultB = default;
bool HasResult = false;
bool RequireAccumulation = args.Contains(" -accumulation") || args.Contains(" -acc");
bool RequireGathering = !args.Contains(" -no-gathering") && !args.Contains(" -ng");

// Init Raylib
Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
Raylib.SetWindowState(ConfigFlags.AlwaysRunWindow | ConfigFlags.ResizableWindow);
Raylib.InitWindow(WindowWidth, WindowHeight, Util.GetDisplayName(ProjectName));
Raylib.SetTargetFPS(48);

// Res
var assembly = Assembly.GetExecutingAssembly();
var stream = assembly.GetManifestResourceStream($"{ProjectName}.Icon.png");
if (stream != null) {
	using (stream)
	using (var reader = new BinaryReader(stream)) {
		var pngBytes = reader.ReadBytes((int)stream.Length);
		var img = Raylib.LoadImageFromMemory(".png", pngBytes);
		Raylib.SetWindowIcon(img);
		Raylib.UnloadImage(img);
	}
}


// ======== Running ========

while (!Raylib.WindowShouldClose()) {

	if (GlobalFrame < AlertFrame + 24) {
		Raylib.ClearBackground(new Color(
			Math.Clamp((AlertFrame + 24 - GlobalFrame) * 255 / 24, 0, 255),
			0, 0, 255
		));
	} else {
		Raylib.ClearBackground(Color.Black);
	}

	// File Drop
	if (Raylib.IsFileDropped()) {
		foreach (var path in Raylib.GetDroppedFiles()) {
			string ext = Util.GetExtensionWithDot(path);
			if (ext != ".png") continue;

			// Reset
			ErrorMessage = "";
			HasResult = false;
			if (Raylib.IsTextureReady(CurrentTexture)) {
				Raylib.UnloadTexture(CurrentTexture);
			}

			// Load Image
			var img = Raylib.LoadImage(path);
			if (!Raylib.IsImageReady(img)) {
				ErrorMessage = $"Fail to load image data for \"{Util.GetNameWithoutExtension(path)}\"";
				goto _ALERT_;
			}

			CurrentTexture = Raylib.LoadTexture(path);
			int len = img.Width * img.Height;
			if (len <= 0) {
				ErrorMessage = $"Image \"{Util.GetNameWithoutExtension(path)}\" is empty";
				goto _ALERT_;
			}

			// Load Colors
			var colors = new Color[len];
			unsafe {
				var pColors = Raylib.LoadImageColors(img);
				for (int i = 0; i < len; i++) {
					colors[i] = pColors[i];
				}
				Raylib.UnloadImageColors(pColors);
			}

			// Analysis
			RequiringColors = colors;
			RequiringCountDown = 5;

			// Finish
			Raylib.UnloadImage(img);
			HasResult = true;
			break;

			// Alert
			_ALERT_:;
			AlertFrame = GlobalFrame;
			HasResult = true;
			continue;
		}
	}

	// UI
	if (!Raylib.IsWindowMinimized()) {

		// Requiring
		if (RequiringColors != null) {

			// Loading Hint
			Raylib.DrawText("Analyzing...", 42, 42, 64, Color.RayWhite);

			// Analysis
			if (RequiringCountDown > 0) {
				RequiringCountDown--;
				goto _END_;
			} else {
				if (RequireAccumulation) {
					CurrentAccumulateResult = new AccumulateResult(RequiringColors);
				}
				if (RequireGathering) {
					CurrentGatheringResultA = new GatheringResult(RequiringColors, false);
					CurrentGatheringResultB = new GatheringResult(RequiringColors, true);
				}
				RequiringColors = null;
				HasResult = true;
			}
		}

		// Drop Hint
		if (!HasResult) {
			Raylib.DrawText("Drag and drop png file into window", 42, 42, 64, Color.RayWhite);
			goto _END_;
		}

		// Error Hint
		if (!string.IsNullOrEmpty(ErrorMessage)) {
			Raylib.DrawText(ErrorMessage, 42, 42, 64, new Color(255, 64, 0, 255));
			goto _END_;
		}

		int rWidth = Raylib.GetRenderWidth();
		int rHeight = Raylib.GetRenderHeight();
		int uiLeft = 0;
		int resultCount = (RequireAccumulation ? 1 : 0) + (RequireGathering ? 1 : 0);

		// Draw Accumulate Textures
		if (RequireAccumulation) {
			int accW = rWidth / resultCount;
			CurrentAccumulateResult.DrawResult(new Rectangle(uiLeft, 0, accW, rHeight));
			uiLeft += accW;
		}

		// Draw Gathering Textures 
		if (RequireGathering) {
			int gW = rWidth / resultCount;
			CurrentGatheringResultA.DrawResult(new Rectangle(uiLeft, 0, gW, rHeight / 2f));
			CurrentGatheringResultB.DrawResult(new Rectangle(uiLeft, rHeight / 2f, gW, rHeight / 2f));
			uiLeft += gW;
		}



	}

	// Finish
	_END_:;
	GlobalFrame++;
	Raylib.EndDrawing();
}



// ======== Quit ========


// Save Config
{
	var builder = new StringBuilder();
	if (!Raylib.IsWindowMinimized()) {
		builder.AppendLine($"WindowWidth:{Raylib.GetScreenWidth()}");
		builder.AppendLine($"WindowHeight:{Raylib.GetScreenHeight()}");
	} else {
		builder.AppendLine($"WindowWidth:{WindowWidth}");
		builder.AppendLine($"WindowHeight:{WindowHeight}");
	}
	Util.TextToFile(builder.ToString(), ConfigPath, Encoding.UTF8);
}


// Close Windows Terminal on Quit
#if DEBUG
Process.GetProcessesByName("WindowsTerminal").ToList().ForEach(item => item.CloseMainWindow());
#endif
