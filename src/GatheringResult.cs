using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace ColorSummarizer;

public readonly struct GatheringResult {


	// SUB
	public enum FilterMode { All, Common, Extreme, }

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
	public readonly List<(int pixCount, Color color)> ResultColors = [];
	private readonly int MaxResultHeight;
	private readonly ulong TotalResultHeight;


	// MSG
	public GatheringResult (Color[] colors, FilterMode filter) {

		// Clear
		MaxResultHeight = 0;
		ResultColors.Clear();
		var columns = new List<HueColumn>();

		// Create New
		int len = colors.Length;
		var colSpan = new ReadOnlySpan<Color>(colors);
		var allPixs = new List<Pix>();

		// Get All Pixels
		switch (filter) {
			case FilterMode.All:
				for (int i = 0; i < len; i++) {
					allPixs.Add(new Pix(colSpan[i]));
				}
				break;
			case FilterMode.Common:
				for (int i = 0; i < len; i++) {
					var pix = new Pix(colSpan[i]);
					if (pix.S < 0.2f || pix.L < 0.1f || pix.L > 0.9f) {
						continue;
					}
					allPixs.Add(pix);
				}
				break;
			case FilterMode.Extreme:
				for (int i = 0; i < len; i++) {
					var pix = new Pix(colSpan[i]);
					if (pix.S >= 0.2f && pix.L >= 0.1f && pix.L <= 0.9f) {
						continue;
					}
					allPixs.Add(pix);
				}
				break;
		}
		allPixs.Sort((a, b) => a.H.CompareTo(b.H));

		// Get Hue Columns
		var allPixSpan = CollectionsMarshal.AsSpan(allPixs);
		int currentIndex = 0;
		int hueLeft = ContinueSearch(allPixSpan, currentIndex, -1, 0, allPixSpan.Length - 1);
		int availableRight = hueLeft > 0 ? hueLeft : allPixSpan.Length - 1;
		int hueRight = ContinueSearch(allPixSpan, currentIndex, 1, 0, availableRight);
		int availableLeft = hueRight;
		FillColumn(columns, allPixSpan, availableRight, availableLeft + allPixSpan.Length - availableRight + 1);
		currentIndex = hueRight + 1;
		while (true) {

			// Search for Limit
			hueRight = ContinueSearch(allPixSpan, currentIndex, 1, availableLeft, availableRight);
			if (hueRight >= availableRight) {
				break;
			}

			// Calculate Hue Column
			FillColumn(columns, allPixSpan, currentIndex, hueRight - currentIndex + 1);

			// Next
			currentIndex = hueRight + 1;
		}
		foreach (var column in columns) {
			column.Pixels.Sort((a, b) => {
				int order = b.SteppedS.CompareTo(a.SteppedS);
				return order != 0 ? order : a.V.CompareTo(b.V);
			});
		}

		// Split Column for Stepped S
		var newColumns = new List<HueColumn>();
		for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++) {
			var sourceColumn = columns[columnIndex];
			var pSpan = CollectionsMarshal.AsSpan(sourceColumn.Pixels);
			int pLen = pSpan.Length;
			if (pLen == 0) continue;
			var anchorPix = pSpan[0];
			var newColumn = new HueColumn() {
				TargetHue = sourceColumn.TargetHue,
			};
			for (int i = 0; i < pLen; i++) {
				var pix = pSpan[i];
				if (pix.SteppedS != anchorPix.SteppedS || pix.Distance(anchorPix) > 0.06f) {
					anchorPix = pix;
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

		// Sort Column by Count
		newColumns.Sort((a, b) => b.Pixels.Count.CompareTo(a.Pixels.Count));

		// Fill Result Colors
		MaxResultHeight = 0;
		TotalResultHeight = 0;
		foreach (var column in newColumns) {
			var pSpan = CollectionsMarshal.AsSpan(column.Pixels);
			int pLen = pSpan.Length;
			if (pLen == 0) continue;
			float sumR = 0f;
			float sumG = 0f;
			float sumB = 0f;
			for (int i = 0; i < pLen; i++) {
				var _pix = pSpan[i];
				sumR += _pix.R;
				sumG += _pix.G;
				sumB += _pix.B;
			}
			ResultColors.Add((pLen, new Color(
				(int)Math.Clamp(sumR / pLen, 0, 255),
				(int)Math.Clamp(sumG / pLen, 0, 255),
				(int)Math.Clamp(sumB / pLen, 0, 255),
				255
			)));
			MaxResultHeight = Math.Max(MaxResultHeight, pLen);
			TotalResultHeight += (ulong)pLen;
		}

		// Sort Result Colors






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


	public readonly void DrawResult (Rectangle uiRect) {
		int columnCount = ResultColors.Count;
		if (columnCount == 0) return;
		float currentX = uiRect.X;
		for (int i = 0; i < columnCount; i++) {
			var (pixHeight, resultColor) = ResultColors[i];
			float uiWidth = uiRect.Width * pixHeight / TotalResultHeight;
			Raylib.DrawRectangleRec(
				new Rectangle(currentX, uiRect.Y, uiWidth, uiRect.Height),
				resultColor
			);
			currentX += uiWidth;
		}
	}


}

