using Raylib_cs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColorSummarizer;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;


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
string Message = "Drag and drop png file into window";
int GlobalFrame = 0;
int AlertFrame = int.MinValue;

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
			Message = "";
			if (Raylib.IsTextureReady(CurrentTexture)) {
				Raylib.UnloadTexture(CurrentTexture);
			}
			// Load Image
			var img = Raylib.LoadImage(path);
			if (!Raylib.IsImageReady(img)) {
				Message = "Fail to load image data";
				goto _ALERT_;
			}
			CurrentTexture = Raylib.LoadTexture(path);
			int len = img.Width * img.Height;
			if (len <= 0) {
				Message = "Image is empty";
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
			Analysis(colors);

			// Finish
			Raylib.UnloadImage(img);
			break;

			// Alert
			_ALERT_:;
			AlertFrame = GlobalFrame;
			continue;
		}
	}

	// UI
	if (!Raylib.IsWindowMinimized()) {

		// Hint
		if (!string.IsNullOrEmpty(Message)) {
			Raylib.DrawText(Message, 42, 42, 64, Color.RayWhite);
			goto _END_;
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


// ======== Func ========
static void Analysis (Color[] colors) {

	//Raylib.ColorFromHSV
	



}
static unsafe byte[] TextureToPngBytes (Texture2D texture) {
	var fileType = Marshal.StringToHGlobalAnsi(".png");
	int fileSize = 0;
	char* result = Raylib.ExportImageToMemory(
		Raylib.LoadImageFromTexture(texture),
		(sbyte*)fileType.ToPointer(),
		&fileSize
	);
	if (fileSize == 0) return [];
	var resultBytes = new byte[fileSize];
	Marshal.Copy((nint)result, resultBytes, 0, fileSize);
	Marshal.FreeHGlobal((nint)result);
	Marshal.FreeHGlobal(fileType);
	return resultBytes;
}
static unsafe Texture2D? GetTextureFromPixelsLogic (Color[] pixels, int width, int height) {
	int len = width * height;
	if (len == 0) return null;
	Texture2D textureResult;
	var image = new Image() {
		Format = PixelFormat.UncompressedR8G8B8A8,
		Width = width,
		Height = height,
		Mipmaps = 1,
	};
	if (pixels != null && pixels.Length == len) {
		var bytes = new byte[pixels.Length * 4];
		int index = 0;
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				int i = (height - y - 1) * width + x;
				var p = pixels[i];
				bytes[index * 4 + 0] = p.R;
				bytes[index * 4 + 1] = p.G;
				bytes[index * 4 + 2] = p.B;
				bytes[index * 4 + 3] = p.A;
				index++;
			}
		}
		fixed (void* data = bytes) {
			image.Data = data;
			textureResult = Raylib.LoadTextureFromImage(image);
		}
	} else {
		textureResult = Raylib.LoadTextureFromImage(image);
	}
	Raylib.SetTextureFilter(textureResult, TextureFilter.Point);
	return textureResult;

}
