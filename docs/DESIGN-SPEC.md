# PackInspect – Design specification

> **Simulation notice:** PackInspect is control software for a simulated
> semiconductor package inspection and test station. No real hardware exists.
> Every device talks to `PackInspect.Simulator` over a network protocol, and
> nothing here has been validated on a physical machine.

This document is the reading guide to the design: what each part does, the one
decision that shaped it, and where its full definition lives. It does not repeat
register maps, commands or schemas; those are defined once, in the documents
linked from each section.

## 1. Station

One unit at a time moves along a single linear axis.

```
 Load (0 mm) --> Camera (100 mm) --> Electrical test (200 mm) --> Unload into pass or reject bin
```

- The handler places a unit on the carrier at the load position and its barcode
  is read.
- At the camera position one 320 x 320 greyscale image is captured and inspected.
- At the test position the multimeter measures resistance or DC voltage,
  whichever the recipe asks for.
- The handler removes the unit into the pass bin or the reject bin. A unit is
  good only if both the vision check and the electrical check pass.

The sequence and its rules: [ARCHITECTURE.md §4](ARCHITECTURE.md#4-machine-cycle).

## 2. Machine states

Seven states: `Idle`, `Homing`, `Ready`, `Running`, `Paused`, `Alarm`, `EStop`.
The transition table is in
[`MachineStateMachine`](../src/PackInspect.Core/Machine/MachineStateMachine.cs),
which wraps the Stateless library;
[`MachineController`](../src/PackInspect.Core/Machine/MachineController.cs)
owns it and does the work around it.

Key decision: **a reset never assumes the axis position.** After an alarm the
machine returns to `Ready` only if the axis still reports that it is homed, and
after an emergency stop it always returns to `Idle` and must be homed again.

Diagram and the reasons for each rule:
[ARCHITECTURE.md §3](ARCHITECTURE.md#3-machine-state-machine).

## 3. Devices and protocols

The sequence and the view models use devices only through interfaces in
`Core/Devices`. The drivers are in `PackInspect.Devices`.

| Device | Interface | Protocol | Defined in |
|---|---|---|---|
| PLC, interlocks, handler | `IPlc` | Modbus TCP, port 5020 | [Data.md §2](Data.md#2-modbus-tcp-map--built) |
| Linear axis | `IMotionAxis` | Modbus TCP, port 5020 | [Data.md §2](Data.md#2-modbus-tcp-map--built) |
| Camera | `ICamera` | Text header and raw pixels over TCP, port 5030 | [Data.md §2a](Data.md#2a-camera-protocol--built) |
| Multimeter | `IInstrument` | SCPI over TCP, port 5025 | [Data.md §4](Data.md#4-scpi-command-set--built) |
| Barcode reader | `IBarcodeReader` | In-process call into the VB.NET `PackInspect.LegacyDriver` | [`LegacyBarcodeReaderAdapter`](../src/PackInspect.Devices/Barcode/LegacyBarcodeReaderAdapter.cs) |

Key decisions:

- **Interlock signals are fail-safe.** True means healthy, so a lost connection
  reads as a fault.
- **The simulator shares no code with the app.** Each side keeps its own copy of
  the addresses, so a wrong address fails in the integration tests instead of
  being wrong on both sides.

Alarm codes and what clears them: [Data.md §3](Data.md#3-alarm-codes--built).

## 4. Inspection

### 4.1 Rule-based vision

[`OpenCvVisionInspector`](../src/PackInspect.Devices/Vision/OpenCvVisionInspector.cs)
decides the vision verdict. The image has three brightness levels (dark body,
mid-grey carrier, bright leads and marking), so fixed thresholds separate them.
Four checks run in order and the first that fails names the defect: body shape,
lead count, marking, and anything else bright on the body. The limits come from
the recipe.

The table of checks and the measured escape rates:
[ARCHITECTURE.md §4](ARCHITECTURE.md#rule-based-vision-inspection).

### 4.2 AI classifier

[`Dinov2DefectClassifier`](../src/PackInspect.Devices/Ai/Dinov2DefectClassifier.cs)
gives a second opinion for the operator's review screen.

- **Backbone:** a frozen DINOv2 ViT-S/14 model in ONNX format, run with ONNX
  Runtime. The image is resized to 224 x 224 and normalised, and the 384-number
  `[CLS]` embedding is taken from the output.
- **Head:** a multiclass logistic regression trained with ML.NET on those
  embeddings and loaded as an ML.NET model file. It names one of six classes:
  `None`, `Scratch`, `CrackChip`, `Contamination`, `MarkingDefect`, `LeadDefect`.
- **The AI verdict is advisory.** It is stored for review and never changes how
  a unit is sorted.

Why this model: [PRD.md §6](PRD.md#6-deep-learning-model). Measured accuracy,
false-call and escape rates: [TEST-PLAN.md §4](TEST-PLAN.md#4-ai-classifier).

## 5. Data, users and audit trail

- Results, recipes, users, alarms and reviews are stored in SQLite through EF
  Core. Schema: [Data.md §5](Data.md#5-database--recipes-lots-unitresults-users-auditentries-alarmhistory-and-aireviews-built).
- A recipe change needs the user's password and a reason, and is saved together
  with its audit entry. The audit trail is insert-only.
- Passwords are hashed with PBKDF2. Rules and honest limits:
  [Security.md](Security.md).

## 6. Publishing results

Each saved unit result is handed to
[`IResultPublisher`](../src/PackInspect.Core/Publishing/IResultPublisher.cs).
By default it is written as a JSON file; an HTTP adapter exists but is switched
off, because no factory server is part of this project.

Payload and adapter: [Data.md §6](Data.md#6-published-result-payload--built).
