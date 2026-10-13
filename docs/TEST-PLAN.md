# PackInspect – Test plan and results

> **Simulation notice:** every test runs against `PackInspect.Simulator` or
> against in-memory fakes (`tests/PackInspect.Tests/Fakes`). No real hardware
> and no real image was used for any number in this document.

## 1. Strategy

- **Unit tests** run the machine logic in `Core` against fake devices: no
  socket, no window, no simulator.
- **Integration tests** host the simulator in-process on free ports and run the
  real Modbus, SCPI and camera drivers, the real OpenCV inspection and a real
  SQLite file against it. They carry `[Trait("Category", "Integration")]`.
- **Framework:** xUnit v3 on Microsoft.Testing.Platform, with xUnit's built-in
  `Assert` only.
- **No sleeping:** a test waits for a machine event with a timeout
  (`Fakes/MachineWait`), never for a guessed delay.
- **Isolation:** each test that needs a database or image folder gets its own
  temporary folder.

## 2. Test inventory

Counts are from `dotnet test --solution PackInspect.slnx --list-tests`; a
`[Theory]` counts once per data row.

| Area | Test classes | Tests |
|---|---|---|
| State machine and controller | `MachineStateMachineTests` (43), `MachineControllerTests` (15) | 58 |
| Cycle, lots and limits | `InspectionCycleTests` (11), `LotRulesTests` (13), `LotSessionTests` (7), `ElectricalLimitsTests` (14) | 45 |
| Drivers and parsing | `ScpiReplyTests` (12), `VisionInspectorTests` (11), `LegacyBarcodeReaderAdapterTests` (14), `LegacyBarcodeReaderTests` (5) | 42 |
| Data, users and audit trail | `ResultStoreTests` (7), `UserServiceTests` (8), `PasswordSecurityTests` (5), `RecipeSignOffTests` (3), `AuditStoreTests` (2), `AlarmHistoryTests` (1), `AiReviewStoreTests` (1) | 27 |
| Result publishing | `FabTraceHttpResultPublisherTests` (5), `ResultPublisherTests` (3) | 8 |
| AI classifier | `AiReviewMetricsCalculationTests` (2), `DefectClassifierTrainingTests` (2), `Dinov2ModelVerificationTests` (1) | 5 |
| Architecture rules | `ArchitectureTests` (6) | 6 |
| Integration against the simulator | `SimulatorStationTests` (8) | 8 |
| **Total** | | **199** |

A normal run executes 198 of them in about 25 seconds on the development
laptop. The remaining one is the full-dataset classifier training in section 4,
which is marked explicit because it takes several minutes.

Three tests need the DINOv2 model file, which is not committed. On a clone
without it (including CI) they are reported as skipped, not as passed.

## 3. Fault injection against the simulator

Each test injects a fault into the running simulator, checks the alarm, clears
the fault and checks that the machine recovers.

| Fault injected | Expected | Recovery checked | Test |
|---|---|---|---|
| Guard door opened during a lot | `DOOR_OPEN`, state `Alarm`; Reset refused while the door is open | Lot finishes after the door is closed and Reset | `Injected_door_open_during_a_lot_raises_alarm_and_the_lot_finishes_after_recovery` |
| Emergency stop pressed | State `EStop` | Reset returns to `Idle`; homing is required again | `Injected_emergency_stop_requires_homing_again` |
| Axis never completes a move | `AXIS_MOVE_TIMEOUT`, state `Alarm` | Lot finishes after Reset | `Injected_axis_timeout_during_a_move_raises_alarm_and_the_lot_finishes_after_recovery` |
| Axis never completes homing | `AXIS_HOME_TIMEOUT`, state `Alarm` | Reset returns to `Idle`; homing then succeeds | `Injected_axis_timeout_during_homing_raises_alarm_and_homing_works_again_afterwards` |
| Multimeter stops answering | `INSTRUMENT_NO_RESPONSE`, state `Alarm` | Lot finishes after Reset; the interrupted unit is not recorded | `Injected_instrument_silence_raises_alarm_and_the_lot_finishes_after_recovery` |

Unattended running (PRD objective O1) is covered by
`Lot_of_one_hundred_mixed_units_runs_unattended_and_every_result_is_stored`:
a 100-unit lot with injected defects and electrical failures completes without
an alarm, and 100 results and 100 images are stored.

## 4. AI classifier

### 4.1 Dataset

- Synthetic images from the simulator's `UnitImageGenerator`, with random
  position, rotation, lighting, blur and sensor noise.
- Six classes: `None` (good), `Scratch`, `CrackChip`, `Contamination`,
  `MarkingDefect`, `LeadDefect`.
- 600 images per class, each with its own fixed seed, split 70/15/15:
  2,520 training, 540 validation, 540 held-out test.

### 4.2 Method

`Full_dataset_training_meets_the_accuracy_target_and_saves_the_head` embeds
every image with the frozen DINOv2 backbone, trains the ML.NET head on the
training split, scores it on the held-out test split and writes
`models/training-report.md`. The numbers below are copied from that report.

The head is multiclass logistic regression fitted with L-BFGS on one thread.
One thread matters: with ML.NET's multi-threaded SDCA trainer the same code and
seeds gave 89.3% on one run and 85.9% on the next, so no single number could be
reported honestly.

```powershell
dotnet test --solution PackInspect.slnx --explicit only
```

### 4.3 Result: the accuracy target is not met

| Measure | Target | Measured |
|---|---|---|
| Accuracy on the held-out test set | at least 95% | **91.67%** (495 of 540) |
| Accuracy on the validation set | | 91.67% |

Confusion matrix on the test set (rows: truth, columns: predicted):

| Truth | None | Scratch | CrackChip | Contamination | MarkingDefect | LeadDefect | Class accuracy |
|---|---|---|---|---|---|---|---|
| None | 69 | 0 | 0 | 0 | 0 | 21 | 76.7% |
| Scratch | 1 | 87 | 0 | 0 | 0 | 2 | 96.7% |
| CrackChip | 0 | 0 | 90 | 0 | 0 | 0 | 100.0% |
| Contamination | 2 | 0 | 0 | 88 | 0 | 0 | 97.8% |
| MarkingDefect | 0 | 0 | 0 | 0 | 90 | 0 | 100.0% |
| LeadDefect | 19 | 0 | 0 | 0 | 0 | 71 | 78.9% |

What it shows: the classifier separates four classes well but confuses good
units with `LeadDefect`, in both directions. A lead defect is one missing lead
out of 24, a small local change that the single whole-image embedding does not
capture reliably.

### 4.4 Compared with rule-based vision on the same 540 images

| Inspection | False-call rate (good units rejected) | Escape rate (defective units passed) |
|---|---|---|
| AI classifier | 23.33% (21 of 90) | 4.89% (22 of 450) |
| Rule-based OpenCV | 0.00% (0 of 90) | 4.44% (20 of 450) |

On these images the AI is worse than the rule-based check on both measures, and
it rejects far too many good units to decide on its own. This is why its verdict
is advisory: the rule-based check and the electrical test sort the unit, and the
AI label goes to the operator's review screen.

### 4.5 Other measurements

| Measure | Measured |
|---|---|
| Backbone time per image, with preprocessing | about 88 ms on the development laptop CPU |
| Head time per image | under 0.01 ms |
| Model files | backbone 23.3 MB (ONNX), head under 10 KB (ML.NET) |

### 4.6 What was tried, and what is open

Head variants were compared on the validation split only: SDCA and L-BFGS
trainers, feature normalisation, lighter regularisation, and adding the mean of
the patch embeddings to the `[CLS]` embedding. The best reached 92.2% on
validation, so none meets the target. L-BFGS on the `[CLS]` embedding was kept
because it was the best simple option and is repeatable.

The test split was scored more than once while this was worked out, so the
test figure is a slightly optimistic estimate, not a blind one.

Open items:

- **The 95% target (PRD objective O5) is not met.** Closing it needs features
  that keep local detail, for example per-patch embeddings, not a different
  linear head.
- **No real images.** Scoring on the VisA dataset is future work
  ([PRD.md §7](PRD.md#7-dataset)). Nothing here says how the classifier would
  behave on real parts.

## 5. Manual checks

Done once by hand in the running app against the simulator, not automated.

| Check | Observed |
|---|---|
| A 100-unit lot runs in the app | Completed 100 of 100; 100 result files written under `data/published` |
| The window stays responsive during the lot | Tabs switched in under half a second while the state was `Running` |
| Door opened during the lot | State `Alarm` with `DOOR_OPEN`; after closing the door and Reset the lot continued |
| Recipe change with sign-off | New recipe version saved; audit trail shows user, old value 105, new value 106 and the reason |
| Start-up without a user | The app opens at the login dialog; opening a lot without logging in is refused with "Log in before opening a lot." |
| Password entry | The login and sign-off fields show dots, not the password |
| AI review | Selecting a unit shows its stored image; confirming a verdict updates the reviewed count |
| No network call for publishing | No `store_and_forward` folder was created with the default settings |

## 6. Commands

Run from the repository root in PowerShell.

```powershell
dotnet test --solution PackInspect.slnx                                             # all tests
dotnet test --solution PackInspect.slnx --filter-not-trait "Category=Integration"   # unit tests only
dotnet test --solution PackInspect.slnx --filter-trait "Category=Integration"       # integration tests only
dotnet test --solution PackInspect.slnx --explicit only                             # full classifier training
```
