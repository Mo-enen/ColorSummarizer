using Raylib_cs;
using System;
using System.Collections.Generic;

namespace ColorSummarizer;

public struct AccumulateResult {

	public HSV[,,] Accumulation = new HSV[360, 101, 101]; // [h,s,v]
	public Texture2D[] Textures = new Texture2D[12];
	public static Color[] CachePixels = new Color[360 * 101];

	public AccumulateResult (Color[] colors) {

		// Init Textures
		for (int i = 0; i < Textures.Length; i++) {
			var texture = Util.GetTextureFromPixelsLogic(CachePixels, 360, 101);
			if (texture.HasValue) {
				Textures[i] = texture.Value;
			}
		}
		int len = colors.Length;
		int resultTextureCount = Textures.Length;
		var colSpan = new ReadOnlySpan<Color>(colors);
		var queue = new Queue<(int i, int j, int k)>();

		// Accumulation
		var acc = Accumulation;
		for (int i = 0; i < len; i++) {
			var col = colSpan[i];
			var (h360, s01, v01) = Raylib.ColorToHSV(col);
			if (s01 < 0.2f || v01 < 0.1f || v01 > 0.9f) continue;
			int h = ((int)MathF.Round(h360)).UMod(360);
			int s = ((int)MathF.Round(s01 * 100f)).UMod(101);
			int v = ((int)MathF.Round(v01 * 100f)).UMod(101);
			if (acc[h, s, v].Valid) continue;
			acc[h, s, v] = new HSV(h, s, v);
			queue.Enqueue((h, s, v));

		}

		// Iterate
		while (queue.TryDequeue(out var _taskHSV)) {
			var (i, j, k) = _taskHSV;
			int l = Math.Max(i - 1, 0);
			int d = Math.Max(j - 1, 0);
			int b = Math.Max(k - 1, 0);
			int r = Math.Min(i + 1, 359);
			int u = Math.Min(j + 1, 100);
			int f = Math.Min(k + 1, 100);
			var targetHSV = acc[i, j, k];
			for (int _i = l; _i <= r; _i++) {
				for (int _j = d; _j <= u; _j++) {
					for (int _k = b; _k <= f; _k++) {
						if (_i != i && _j != j && _k != k) continue;
						var _hsv = acc[_i, _j, _k];
						if (_hsv.Valid) continue;
						acc[_i, _j, _k] = targetHSV;
						queue.Enqueue((_i, _j, _k));
					}
				}
			}
		}

		// Fill Result Textures
		var cachePixs = CachePixels;
		for (int tIndex = 0; tIndex < resultTextureCount; tIndex++) {
			int v = (int)MathF.Round(tIndex * 100f / (resultTextureCount - 1));
			for (int i = 0; i < 360; i++) {
				for (int j = 0; j < 101; j++) {
					var (_h, _s, _v) = acc[i, j, v];
					cachePixs[j * 360 + i] = Raylib.ColorFromHSV(_h, _s / 100f, _v / 100f);
				}
			}
			Util.FillPixelsIntoTexture(cachePixs, Textures[tIndex]);
		}

	}

}
