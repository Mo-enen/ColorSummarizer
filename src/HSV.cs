namespace ColorSummarizer;

public struct HSV (int h, int s, int v, bool valid = true) {
	public bool Valid = valid;
	public int H = h;
	public int S = s;
	public int V = v;
	public readonly void Deconstruct (out int h, out int s, out int v) {
		h = H;
		s = S;
		v = V;
	}
}