using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace ColorSummarizer;

public readonly struct GatheringResult {


	// SUB
	public struct Pix {
		public byte R;
		public byte G;
		public byte B;
		public float H;
		public float S;
		public float V;
		public float L;
		public int SteppedS;
		public Pix (Color color) {
			var (h360, s, v) = Raylib.ColorToHSV(color);
			float h = h360 / 360f;
			R = color.R;
			G = color.G;
			B = color.B;
			H = h;
			S = s;
			V = v;
			L = (color.R * 0.3f + color.G * 0.587f + color.B * 0.113f) / 255f;
			SteppedS = (int)MathF.Round(s * 12);
		}
		public readonly float Distance (Pix other) {
			float disH = MathF.Abs(H - other.H);
			float disS = MathF.Abs(S - other.S);
			float disV = MathF.Abs(V - other.V);
			return disH * 0.5f + disS * 0.15f + disV * 0.35f;
		}
		public readonly Color GetColor () => new(R, G, B, (byte)255);
	}

	public struct HueColumn () {
		public float TargetHue;
		public float AverageHue;
		public readonly List<Pix> Pixels = [];
	}


	// VAR
	private static readonly Color[] CachePixels = new Color[1 * 128];
	public readonly List<(int height, Texture2D texture)> ResultTextures = [];
	public readonly List<HueColumn> Columns = [];


	// MSG
	public GatheringResult (Color[] colors, bool forExtreme) {

		// Clear
		foreach (var (_, texture) in ResultTextures) {
			if (Raylib.IsTextureReady(texture)) {
				Raylib.UnloadTexture(texture);
			}
		}
		ResultTextures.Clear();
		Columns.Clear();

		// Create New
		int len = colors.Length;
		var colSpan = new ReadOnlySpan<Color>(colors);
		var allPixs = new List<Pix>();

		// Get All Pixels
		for (int i = 0; i < len; i++) {
			var pix = new Pix(colSpan[i]);
			bool extreme = pix.S < 0.2f || pix.L < 0.1f || pix.L > 0.9f;
			if (extreme != forExtreme) continue;
			// pix.S < 0.2f || pix.V < 0.1f || pix.V > 0.9f
			allPixs.Add(pix);
		}
		allPixs.Sort((a, b) => a.H.CompareTo(b.H));

		// Get Hue Columns
		var allPixSpan = CollectionsMarshal.AsSpan(allPixs);
		int currentIndex = 0;
		int hueLeft = ContinueSearch(allPixSpan, currentIndex, -1, 0, allPixSpan.Length - 1);
		int availableRight = hueLeft > 0 ? hueLeft : allPixSpan.Length - 1;
		int hueRight = ContinueSearch(allPixSpan, currentIndex, 1, 0, availableRight);
		int availableLeft = hueRight;
		FillColumn(Columns, allPixSpan, availableRight, availableLeft + allPixSpan.Length - availableRight + 1);
		currentIndex = hueRight + 1;
		while (true) {

			// Search for Limit
			hueRight = ContinueSearch(allPixSpan, currentIndex, 1, availableLeft, availableRight);
			if (hueRight >= availableRight) {
				break;
			}

			// Calculate Hue Column
			FillColumn(Columns, allPixSpan, currentIndex, hueRight - currentIndex + 1);

			// Next
			currentIndex = hueRight + 1;
		}
		foreach (var column in Columns) {
			column.Pixels.Sort((a, b) => {
				int order = b.SteppedS.CompareTo(a.SteppedS);
				return order != 0 ? order : a.V.CompareTo(b.V);
			});
		}

		// Split Column for Stepped S
		var newColumns = new List<HueColumn>();
		for (int columnIndex = 0; columnIndex < Columns.Count; columnIndex++) {
			var sourceColumn = Columns[columnIndex];
			var pSpan = CollectionsMarshal.AsSpan(sourceColumn.Pixels);
			int pLen = pSpan.Length;
			if (pLen == 0) continue;
			int anchorS = pSpan[0].SteppedS;
			var newColumn = new HueColumn() {
				TargetHue = sourceColumn.TargetHue,
			};
			for (int i = 0; i < pLen; i++) {
				var pix = pSpan[i];
				if (pix.SteppedS != anchorS) {
					anchorS = pix.SteppedS;
					newColumns.Add(newColumn);
					newColumn = new HueColumn() {
						TargetHue = sourceColumn.TargetHue,
					};
				} else {
					newColumn.Pixels.Add(pix);
				}
			}
			if (newColumn.Pixels.Count > 0) {
				newColumns.Add(newColumn);
			}
		}
		Columns.Clear();
		Columns.AddRange(newColumns);

		// Fill Result Texture
		foreach (var column in Columns) {
			var pSpan = CollectionsMarshal.AsSpan(column.Pixels);
			int pLen = pSpan.Length;
			if (pLen == 0) continue;
			int cacheLen = CachePixels.Length;
			Array.Clear(CachePixels);
			int currentY = 0;
			int accCount = 0;
			float sumH = 0;
			float sumS = 0;
			float sumV = 0;
			var anchorPix = pSpan[0];
			for (int i = 0; i < pLen; i++) {
				var pix = pSpan[i];
				if (pix.Distance(anchorPix) > 0.06f) {
					anchorPix = pix;
					if (accCount > 128) {
						int drawLen = Math.Clamp(accCount / 128, 1, 8);
						for (int _y = 0; _y < drawLen; _y++) {
							CachePixels[currentY] = Raylib.ColorFromHSV(
								sumH / accCount * 360f,
								sumS / accCount,
								sumV / accCount
							);
							currentY++;
							if (currentY >= cacheLen) goto _SKIP_;
						}
					}
					accCount = 0;
					sumH = 0;
					sumS = 0;
					sumV = 0;
				}
				sumH += pix.H;
				sumS += pix.S;
				sumV += pix.V;
				accCount++;
			}
			_SKIP_:;
			var texture = Util.GetTextureFromPixelsLogic(CachePixels, 1, cacheLen);
			if (texture.HasValue && currentY > 0) {
				ResultTextures.Add((currentY, texture.Value));
			}
		}

		// Func
		static int ContinueSearch (Span<Pix> allPixs, int startIndex, int delta, int left, int right) {
			const float THRESHOLD = 0.04f;
			int len = allPixs.Length;
			int end = startIndex + allPixs.Length;
			int index = startIndex;
			int result = startIndex;
			var targetPix = allPixs[startIndex];
			for (int i = 0; i < len; i++, index = (index + delta) % len) {
				if (index < left || index > right) break;
				var pix = allPixs[index];
				if (Math.Abs(pix.H - targetPix.H) > THRESHOLD) break;
				result = index;
			}
			return result;
		}
		static void FillColumn (List<HueColumn> columns, Span<Pix> allPix, int index, int length) {
			const float HUE_THRESHOLD = 0.04f;
			int minSplitCount = Math.Max(256, length / 4);
			int pixLen = allPix.Length;
			var column = new HueColumn();
			for (int i = 0; i < length; i++) {
				int _index = (index + i) % pixLen;
				var pix = allPix[_index];
				if (column.Pixels.Count == 0) {
					column.TargetHue = pix.H;
				}
				column.Pixels.Add(pix);
				// New Column Check
				if (
					column.Pixels.Count >= minSplitCount &&
					MathF.Abs(column.TargetHue - pix.H) > HUE_THRESHOLD
				) {
					columns.Add(column);
					column.Pixels.Clear();
				}
			}
			if (column.Pixels.Count > 0) {
				columns.Add(column);
			}
		}
	}


	public void DrawResult (Rectangle uiRect) {
		int columnCount = ResultTextures.Count;
		if (columnCount == 0) return;
		float columnW = uiRect.Width / columnCount;
		float columnH = uiRect.Height;
		for (int i = 0; i < columnCount; i++) {
			var (targetHeight, texture) = ResultTextures[i];
			Raylib.DrawTexturePro(
				texture,
				source: new Rectangle(0, texture.Height - targetHeight, texture.Width, targetHeight),
				dest: new Rectangle(
					uiRect.X + i * columnW,
					uiRect.Y,
					columnW, columnH
				),
				origin: new Vector2(),
				0f, Color.White
			);
		}
	}


}

