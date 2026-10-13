# Architecture essentials

The rules that must never be broken. Reasons and detail are in
[ARCHITECTURE.md](ARCHITECTURE.md).

## 1. Dependency direction

```
App  ->  Core, Devices, Data
Devices  ->  Core, LegacyDriver
Data  ->  Core
Core  ->  nothing in this solution
Simulator  ->  nothing in this solution
LegacyDriver  ->  nothing in this solution
```

- `Core` has no UI types and no I/O libraries (no WPF, Modbus, OpenCV, EF Core).
- `Simulator` shares no code with the app. It only speaks the wire protocols.
- `tests/PackInspect.Tests/Unit/ArchitectureTests.cs` enforces this. If a new
  dependency is truly needed, change the rule here and the test together.

## 2. Devices behind interfaces

- Every device is used through an interface in `Core/Devices`: `IPlc`,
  `IMotionAxis`, `ICamera`, `IInstrument`.
- The sequence and the view models never name a concrete driver. Only the
  composition root in `App` does.
- Interfaces speak the machine's language (home, capture, measure), never the
  protocol's (registers, SCPI strings).

## 3. Strict MVVM

- Code-behind contains `InitializeComponent()` and nothing else.
- View models never reference a WPF control, window or dispatcher.
- Machine rules live in `Core`. A view model shows state and forwards commands.

## 4. Async and cancellation

- All device I/O is `async` and takes a `CancellationToken`.
- No `.Result`, `.Wait()`, `Thread.Sleep` or blocking socket call on the UI thread.
- `OperationCanceledException` means "stop requested"; it is never an alarm.

## 5. Faults are alarms

- A device fault becomes an alarm with a code and moves the machine to Alarm.
  It never crashes the app and is never swallowed.
- Interlock signals are fail-safe: `true` means healthy, so lost communication
  reads as a fault.

## 6. State changes go through the state machine

- Nothing sets `MachineState` directly. Only `MachineStateMachine` transitions do.
- Every trigger is fired under one lock; Stateless is not thread-safe.

## 7. Data

- Schema, register map, SCPI commands and payloads are defined in
  [Data.md](Data.md). Change the document and the code together.
- The audit trail is insert-only. Timestamps are stored in UTC.

## 8. Honesty

- Everything runs against a simulator. No document, comment, commit message or
  UI text may suggest real-hardware validation.
