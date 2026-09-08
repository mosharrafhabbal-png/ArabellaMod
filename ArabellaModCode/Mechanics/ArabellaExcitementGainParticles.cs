using Godot;

namespace ArabellaMod.Mechanics;

/// <summary>A short ribbon of red motes drifting right to left across the meter face.</summary>
public sealed partial class ArabellaExcitementGainParticles : Control
{
    private const int MaxParticles = 64;
    private readonly List<Mote> _motes = [];
    private readonly RandomNumberGenerator _random = new();

    private sealed class Mote
    {
        public float Age;
        public float Lifetime;
        public float StartX;
        public float TravelX;
        public float Height;
        public float Radius;
        public float Phase;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _random.Randomize();
        SetProcess(false);
    }

    internal void Emit(float meterWidth, float meterHeight, int gain)
    {
        if (meterWidth <= 0f || gain <= 0)
            return;

        int count = 10 + Math.Min(gain, 4) * 2;
        int overflow = _motes.Count + count - MaxParticles;
        if (overflow > 0)
            _motes.RemoveRange(0, overflow);

        for (int i = 0; i < count; i++)
        {
            float startX = meterWidth * _random.RandfRange(0.86f, 0.99f);
            _motes.Add(new Mote
            {
                Age = -_random.RandfRange(0f, 0.16f),
                Lifetime = _random.RandfRange(0.55f, 0.85f),
                StartX = startX,
                TravelX = -startX * _random.RandfRange(0.80f, 1f),
                Height = meterHeight * _random.RandfRange(0.35f, 0.65f),
                Radius = _random.RandfRange(0.7f, 1.4f),
                Phase = _random.RandfRange(0f, Mathf.Tau)
            });
        }

        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        for (int i = _motes.Count - 1; i >= 0; i--)
        {
            Mote mote = _motes[i];
            mote.Age += (float)delta;
            if (mote.Age >= mote.Lifetime)
                _motes.RemoveAt(i);
        }

        QueueRedraw();
        if (_motes.Count == 0)
            SetProcess(false);
    }

    public override void _Draw()
    {
        foreach (Mote mote in _motes)
        {
            if (mote.Age < 0f)
                continue;

            float progress = Mathf.Clamp(mote.Age / mote.Lifetime, 0f, 1f);
            float alpha = Mathf.Min(progress / 0.10f, 1f) * (1f - progress);
            Vector2 position = new(
                mote.StartX + mote.TravelX * progress,
                mote.Height +
                Mathf.Sin(progress * Mathf.Tau + mote.Phase) * 1.2f);
            float radius = mote.Radius * (1f - progress * 0.4f);
            DrawCircle(position, radius * 2.2f, new Color(0.95f, 0.04f, 0.14f, alpha * 0.12f), antialiased: true);
            DrawCircle(position, radius, new Color(1f, 0.12f, 0.24f, alpha * 0.85f), antialiased: true);
        }
    }

    public override void _ExitTree()
    {
        _motes.Clear();
    }
}
