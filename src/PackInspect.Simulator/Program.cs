using PackInspect.Simulator;

await using var simulator = SimulatorHost.Start();

Console.WriteLine("PackInspect simulator (no real hardware)");
Console.WriteLine($"  PLC and axis  Modbus TCP  127.0.0.1:{simulator.ModbusPort}");
Console.WriteLine($"  Multimeter    SCPI        127.0.0.1:{simulator.ScpiPort}");
Console.WriteLine($"  Camera        TCP         127.0.0.1:{simulator.CameraPort}");
Console.WriteLine();
Console.WriteLine("Fault injection keys:");
Console.WriteLine("  D  guard door open / closed");
Console.WriteLine("  E  emergency stop pressed / released");
Console.WriteLine("  A  air pressure low / ok");
Console.WriteLine("  T  axis timeout on / off (moves never complete)");
Console.WriteLine("  I  instrument not responding on / off");
Console.WriteLine("  Q  quit");
Console.WriteLine();

// Shows the truth about each unit next to the bin the app chose for it.
simulator.UnitSorted += sorted => Console.WriteLine(
    $"unit {sorted.Unit.Number,4}: truth {sorted.Unit.Defect,-13} " +
    $"{(sorted.Unit.ElectricallyGood ? "electrical ok  " : "electrical FAIL")} -> " +
    $"sorted {(sorted.SortedAsPass ? "PASS  " : "REJECT")} " +
    $"{(sorted.SortedCorrectly ? "(correct)" : sorted.SortedAsPass ? "(ESCAPE: bad unit passed)" : "(FALSE CALL: good unit rejected)")}");

if (Console.IsInputRedirected)
{
    // No keyboard (for example when started by a script): run until the process is stopped.
    await Task.Delay(Timeout.Infinite);
}

var faults = simulator.Faults;
PrintStatus();

while (true)
{
    // ReadKey blocks, so it runs on a pool thread and the simulator keeps scanning.
    var key = await Task.Run(() => Console.ReadKey(intercept: true).Key);
    switch (key)
    {
        case ConsoleKey.D:
            faults.DoorOpen = !faults.DoorOpen;
            break;
        case ConsoleKey.E:
            faults.EStopPressed = !faults.EStopPressed;
            break;
        case ConsoleKey.A:
            faults.AirPressureLow = !faults.AirPressureLow;
            break;
        case ConsoleKey.T:
            faults.AxisTimeout = !faults.AxisTimeout;
            break;
        case ConsoleKey.I:
            faults.InstrumentNotResponding = !faults.InstrumentNotResponding;
            break;
        case ConsoleKey.Q:
            return;
        default:
            continue;
    }

    PrintStatus();
}

void PrintStatus() =>
    Console.WriteLine(
        $"door={(faults.DoorOpen ? "OPEN" : "closed")}  " +
        $"e-stop={(faults.EStopPressed ? "PRESSED" : "released")}  " +
        $"air={(faults.AirPressureLow ? "LOW" : "ok")}  " +
        $"axis-timeout={(faults.AxisTimeout ? "ON" : "off")}  " +
        $"instrument={(faults.InstrumentNotResponding ? "SILENT" : "ok")}  " +
        $"axis={simulator.AxisStatus}");
