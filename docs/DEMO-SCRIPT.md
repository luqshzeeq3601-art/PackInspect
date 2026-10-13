# PackInspect – Demonstration script

> **Purpose:** a 10 to 15 minute live demonstration for a technical interview.
> Everything shown runs against the simulator; no real hardware is involved.

## 0. Opening (30 seconds)

> *"PackInspect is control software for a simulated semiconductor package
> inspection and test station, written in C# on .NET 10 with WPF. A state machine
> runs the cycle, the devices are reached over Modbus TCP and SCPI, recipe
> changes are signed off and audited, and a DINOv2 classifier gives the operator
> a second opinion on each image. There is no real machine: a separate simulator
> process plays the hardware, and I can inject faults into it."*

## Scene 1: Start the simulator

1. In terminal 1:
   ```powershell
   dotnet run --project src/PackInspect.Simulator
   ```
2. Explain:
   * It listens on three local ports: 5020 (Modbus TCP: PLC and axis), 5025
     (SCPI: multimeter) and 5030 (camera).
   * It shares no code with the app. If a driver used the wrong address, it
     would fail here just as it would against a real PLC.
   * The terminal lists the fault keys: `D` door, `E` emergency stop, `A` air
     pressure, `T` axis timeout, `I` instrument silent.

## Scene 2: Start the app and home the machine

1. In terminal 2:
   ```powershell
   dotnet run --project src/PackInspect.App
   ```
2. The app opens at the login dialog. Log in as `operator` / `Operator123!`.
3. Point to the state banner (`Idle`) and the connection indicator, then click
   **Home**. The state goes `Homing`, then `Ready`.
4. Explain that the views and the sequence only know interfaces (`IPlc`,
   `IMotionAxis`, `ICamera`, `IInstrument`); the Modbus and SCPI drivers are
   named in one place, the composition root.

## Scene 3: Run a lot

1. On the **Production** tab, under **Lot setup**: enter lot number
   `LOT-DEMO-01`, choose recipe `QFN-5x5-100R`, quantity `10`, click
   **Open lot**, then **Start**.
2. What to point at:
   * The state is `Running`.
   * For each unit the axis goes to the camera position (100 mm) and then the
     test position (200 mm), and the handler drops the unit into the pass or
     reject bin.
   * **Lot summary** shows the running yield; **Last unit** shows its image;
     the result list shows vision verdict, defect, measured value and cycle time.
   * The simulator terminal prints the truth about each unit next to the bin
     the app chose, and flags any escape or false call.
   * Every result is saved to SQLite and handed to `IResultPublisher`, which
     writes a JSON file per unit.

## Scene 4: Inject a fault

1. While the lot runs, press `D` in the simulator terminal (door open).
2. The app stops motion, goes to `Alarm` and lists `DOOR_OPEN`.
3. Click **Reset** with the door still open: it is refused. Alarms latch and the
   cause must be gone first.
4. Press `D` again to close the door, click **Reset**: the state returns to
   `Ready` because the axis is still homed. Click **Start** to finish the lot.
5. Optional: press `E` (emergency stop). The state is `EStop`. Release it and
   reset: the state returns to `Idle`, and the machine must be homed again,
   because a real emergency stop removes drive power.
6. Explain: a device fault is a normal event. It becomes a named alarm; it
   never crashes the app and is never swallowed.

## Scene 5: Change a recipe with sign-off

1. Click **Switch user** and log in as `engineer` / `Engineer123!`.
2. On the **Recipe Management** tab select `QFN-5x5-100R`, click **Edit**,
   change **High Limit** from `105` to `106`, click **Save with Sign-Off**.
3. The dialog asks for the password again and a reason. Enter both and click
   **Sign & Commit Change**.
4. Open the **Audit Trail** tab and click **Refresh**: the entry shows who,
   when, the field, the old and new value and the reason.
5. Log in as `admin` / `Admin123!` to show the **Users (Admin)** tab.

## Scene 6: AI review

This scene needs the model files; see the README.

1. Open the **AI Review** tab.
2. The list shows each unit with the rule-based result, the AI label and its
   confidence.
3. Select a unit to see its image, then click **Confirm AI Verdict**, or choose
   a label and click **Submit Override**.
4. Point at the metrics at the top: false-call rate, escape rate and operator
   override rate.
5. Say it plainly: on held-out synthetic images the classifier measures 92%
   accuracy against a 95% target, and it rejects too many good units to decide
   alone. That is why it is advisory and a person confirms or overrides it.

## Questions to expect

* **Why DINOv2 with an ML.NET head, not a CNN trained from scratch?**
  Training from scratch needs many labelled defect images and a GPU, and defects
  are rare. A frozen pretrained backbone gives usable features on a CPU, and the
  head trains in seconds in C#.
* **Why did the classifier miss its target?**
  It confuses good units with units that have one missing lead. One
  whole-image embedding loses that small local detail; per-patch features are
  the next thing to try.
* **Why two processes?**
  So the drivers cross a real socket. In one process, timeouts and wrong
  addresses would be hidden.
* **Why is FluentAssertions, MediatR or AutoMapper not used?**
  A project rule: the base library and xUnit's own `Assert` are enough here, and
  fewer packages means less to explain and to licence-check.
