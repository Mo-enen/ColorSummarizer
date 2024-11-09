using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Raylib_cs;

namespace ColorSummarizer;

static class Util {

	private const MethodImplOptions INLINE = MethodImplOptions.AggressiveInlining;


	// Extension
	public static void Deconstruct (this Vector3 v, out float x, out float y, out float z) {
		x = v.X;
		y = v.Y;
		z = v.Z;
	}
	public static void Deconstruct (this Color c, out byte r, out byte g, out byte b, out byte a) {
		r = c.R;
		g = c.G;
		b = c.B;
		a = c.A;
	}

	public static Rectangle Fit (this Rectangle rect, float targetAspect, float pivotX = 0.5f, float pivotY = 0.5f) {
		float sizeX = rect.Width;
		float sizeY = rect.Height;
		if (targetAspect > rect.Width / rect.Height) {
			sizeY = sizeX / targetAspect;
		} else {
			sizeX = sizeY * targetAspect;
		}
		return new Rectangle(
			rect.X + MathF.Abs(rect.Width - sizeX) * pivotX,
			rect.Y + MathF.Abs(rect.Height - sizeY) * pivotY,
			sizeX, sizeY
		);
	}


	[MethodImpl(INLINE)]
	public static int UMod (this int value, int step) =>
		value > 0 || value % step == 0 ?
		value % step :
		value % step + step;


	// File
	public static void TextToFile (string data, string path, Encoding encoding, bool append = false) {
		CreateFolder(GetParentPath(path));
		using FileStream fs = new(path, append ? FileMode.Append : FileMode.Create);
		using StreamWriter sw = new(fs, encoding);
		sw.Write(data);
		fs.Flush();
		sw.Close();
		fs.Close();
	}


	public static void BytesToFile (byte[] bytes, string path, int length = -1) {
		CreateFolder(GetParentPath(path));
		FileStream fs = new(path, FileMode.Create, FileAccess.Write);
		bytes ??= [];
		fs.Write(bytes, 0, length < 0 ? bytes.Length : length);
		fs.Close();
		fs.Dispose();
	}


	public static IEnumerable<string> ForAllLinesInFile (string path, Encoding encoding) {
		if (!FileExists(path)) yield break;
		using StreamReader sr = new(path, encoding);
		while (sr.Peek() >= 0) yield return sr.ReadLine();
	}


	public static void CreateFolder (string path) {
		if (string.IsNullOrEmpty(path) || FolderExists(path)) return;
		string pPath = GetParentPath(path);
		if (!FolderExists(pPath)) {
			CreateFolder(pPath);
		}
		Directory.CreateDirectory(path);
	}


	public static IEnumerable<string> EnumerateFiles (string path, bool topOnly, string searchPattern) {
		if (!FolderExists(path)) yield break;
		var option = topOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
		foreach (string str in Directory.EnumerateFiles(path, searchPattern, option)) {
			yield return str;
		}
	}
	public static IEnumerable<string> EnumerateFiles (string path, bool topOnly, params string[] searchPatterns) {
		if (!FolderExists(path)) yield break;
		var option = topOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
		if (searchPatterns == null || searchPatterns.Length == 0) {
			foreach (var filePath in Directory.EnumerateFiles(path, "*", option)) {
				yield return filePath;
			}
		} else {
			foreach (var pattern in searchPatterns) {
				foreach (var filePath in Directory.EnumerateFiles(path, pattern, option)) {
					yield return filePath;
				}
			}
		}
	}


	public static IEnumerable<string> EnumerateFolders (string path, bool topOnly, string searchPattern = "*") {
		if (!FolderExists(path)) yield break;
		var option = topOnly ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
		foreach (string str in Directory.EnumerateDirectories(path, searchPattern, option)) {
			yield return str;
		}
	}


	public static bool CopyFolder (string from, string to, bool copySubDirs, bool ignoreHidden, bool overrideFile = false) {

		// Get the subdirectories for the specified directory.
		DirectoryInfo dir = new(from);

		if (!dir.Exists) return false;

		DirectoryInfo[] dirs = dir.GetDirectories();
		// If the destination directory doesn't exist, create it.
		if (!Directory.Exists(to)) {
			Directory.CreateDirectory(to);
		}

		// Get the files in the directory and copy them to the new location.
		FileInfo[] files = dir.GetFiles();
		foreach (FileInfo file in files) {
			try {
				string tempPath = Path.Combine(to, file.Name);
				if (!ignoreHidden || (file.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) {
					file.CopyTo(tempPath, overrideFile);
				}
			} catch { }
		}

		// If copying subdirectories, copy them and their contents to new location.
		if (copySubDirs) {
			foreach (DirectoryInfo subdir in dirs) {
				try {
					string temppath = Path.Combine(to, subdir.Name);
					if (!ignoreHidden || (subdir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) {
						CopyFolder(subdir.FullName, temppath, copySubDirs, ignoreHidden, overrideFile);
					}
				} catch { }
			}
		}
		return true;
	}


	// Path
	public static string GetParentPath (string path) {
		if (string.IsNullOrEmpty(path)) return "";
		var info = Directory.GetParent(path);
		return info != null ? info.FullName : "";
	}


	public static string CombinePaths (string path1, string path2) => Path.Combine(path1, path2);
	public static string CombinePaths (string path1, string path2, string path3) => Path.Combine(path1, path2, path3);


	public static string GetExtensionWithDot (string path) => Path.GetExtension(path);//.txt


	public static string GetNameWithoutExtension (string path) => Path.GetFileNameWithoutExtension(path);


	public static bool FolderExists (string path) => Directory.Exists(path);


	public static bool FileExists (string path) => !string.IsNullOrEmpty(path) && File.Exists(path);


	public static string GetDisplayName (string name) {

		// Remove "m_" at Start
		if (name.Length > 2 && name[0] == 'm' && name[1] == '_') {
			name = name[2..];
		}

		// Replace "_" to " "
		name = name.Replace('_', ' ');

		// Add " " Space Between "a Aa"
		for (int i = 0; i < name.Length - 1; i++) {
			char a = name[i];
			char b = name[i + 1];
			if (
				char.IsLetter(a) &&
				(char.IsLetter(b) || char.IsNumber(b)) &&
				!char.IsUpper(a) &&
				(char.IsUpper(b) || char.IsNumber(b))
			) {
				name = name.Insert(i + 1, " ");
				i++;
			}
		}

		return name;
	}


	// Texture
	public static unsafe byte[] TextureToPngBytes (Texture2D texture) {
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


	public static unsafe Texture2D? GetTextureFromPixelsLogic (Color[] pixels, int width, int height) {
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


	public static void FillPixelsIntoTexture (Color[] pixels, Texture2D texture) {
		if (pixels == null) return;
		int width = texture.Width;
		int height = texture.Height;
		if (pixels.Length != width * height) return;
		int index = 0;
		for (int y = 0; y < height / 2; y++) {
			for (int x = 0; x < width; x++) {
				int i = (height - y - 1) * width + x;
				(pixels[index], pixels[i]) = (pixels[i], pixels[index]);
				index++;
			}
		}
		Raylib.UpdateTexture(texture, pixels);
	}


}