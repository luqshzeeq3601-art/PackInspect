using PackInspect.Simulator.Imaging;

namespace PackInspect.Simulator.Units;

/// <summary>
/// The physical state the simulated devices share: where the carrier is and
/// which unit is on it. The camera and the multimeter only "see" the unit when
/// the carrier is at their station, so a sequence that skips a move fails.
/// </summary>
internal sealed class StationModel(SimulatorOptions options)
{
    public const double CameraPositionMm = 100;
    public const double TestPositionMm = 200;
    private const double PositionToleranceMm = 0.05;

    // What an open-circuit reading looks like on a real multimeter (SCPI overload value).
    private const double Overload = 9.9E37;

    private readonly Lock _gate = new();
    private readonly Random _random = options.Seed is { } seed ? new Random(seed) : new Random();
    private readonly List<SortedUnit> _sorted = [];
    private SimulatedUnit? _current;
    private double _axisPositionMm;
    private int _unitCount;

    public event Action<SortedUnit>? UnitSorted;

    public double AxisPositionMm
    {
        get
        {
            lock (_gate)
            {
                return _axisPositionMm;
            }
        }

        set
        {
            lock (_gate)
            {
                _axisPositionMm = value;
            }
        }
    }

    public bool UnitPresent
    {
        get
        {
            lock (_gate)
            {
                return _current is not null;
            }
        }
    }

    public IReadOnlyList<SortedUnit> SortedUnits
    {
        get
        {
            lock (_gate)
            {
                return [.. _sorted];
            }
        }
    }

    /// <summary>Places the next unit on the carrier. A unit left there by an interrupted cycle is discarded.</summary>
    public void LoadNextUnit()
    {
        lock (_gate)
        {
            var defect = _random.NextDouble() < options.DefectRate
                ? (UnitDefect)_random.Next(1, Enum.GetValues<UnitDefect>().Length)
                : UnitDefect.None;

            bool electricallyGood = _random.NextDouble() >= options.ElectricalFailRate;
            double resistance = electricallyGood
                ? 100 + NextGaussian() * 0.8
                : _random.Next(2) == 0 ? 112 + _random.NextDouble() * 10 : Overload;
            double voltage = electricallyGood ? 3.3 + NextGaussian() * 0.01 : 2.6 + _random.NextDouble() * 0.2;

            _current = new SimulatedUnit(++_unitCount, defect, resistance, voltage, electricallyGood, _random.Next());
        }
    }

    public void UnloadUnit(bool sortedAsPass)
    {
        SortedUnit sorted;
        lock (_gate)
        {
            if (_current is null)
            {
                return;
            }

            sorted = new SortedUnit(_current, sortedAsPass);
            _sorted.Add(sorted);
            _current = null;
        }

        UnitSorted?.Invoke(sorted);
    }

    /// <summary>What the camera sees now: the unit if it is under the camera, otherwise an empty carrier.</summary>
    public UnitImage CaptureImage()
    {
        SimulatedUnit? unit;
        lock (_gate)
        {
            unit = IsAt(CameraPositionMm) ? _current : null;
        }

        return unit is null
            ? UnitImageGenerator.GenerateEmpty(seed: 0)
            : UnitImageGenerator.Generate(unit.Defect, unit.ImageSeed);
    }

    public double MeasureResistance()
    {
        lock (_gate)
        {
            return IsAt(TestPositionMm) && _current is not null ? _current.ResistanceOhm : Overload;
        }
    }

    public double MeasureVoltage()
    {
        lock (_gate)
        {
            return IsAt(TestPositionMm) && _current is not null ? _current.VoltageV : 0;
        }
    }

    private bool IsAt(double positionMm) => Math.Abs(_axisPositionMm - positionMm) <= PositionToleranceMm;

    // Box-Muller: turns two uniform random numbers into one normally distributed number.
    private double NextGaussian() =>
        Math.Sqrt(-2.0 * Math.Log(1.0 - _random.NextDouble())) * Math.Cos(2.0 * Math.PI * _random.NextDouble());
}
