namespace PG.UI
{
    // Fixed-size, allocation-free frame timing window for the in-game diagnostics overlay.
    public sealed class RollingFrameWindow
    {
        public const int Capacity = 120;

        readonly float[] _milliseconds = new float[Capacity];
        int _next;
        int _count;
        float _sum;

        public int Count => _count;
        public float AverageMilliseconds => _count == 0 ? 0f : _sum / _count;
        public float AverageFramesPerSecond => AverageMilliseconds <= 0f ? 0f : 1000f / AverageMilliseconds;

        public float SlowestMilliseconds
        {
            get
            {
                float slowest = 0f;
                for (int i = 0; i < _count; i++)
                    if (_milliseconds[i] > slowest) slowest = _milliseconds[i];
                return slowest;
            }
        }

        public void AddSeconds(float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds) || float.IsNaN(seconds)) return;
            float milliseconds = seconds * 1000f;
            if (_count == Capacity) _sum -= _milliseconds[_next];
            else _count++;
            _milliseconds[_next] = milliseconds;
            _sum += milliseconds;
            _next = (_next + 1) % Capacity;
        }
    }
}
