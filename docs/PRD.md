# Product requirements – PackInspect

## 1. Problem statement

**In the factory.** Back-end semiconductor lines inspect and test packaged units
before shipping. The station software has to coordinate a PLC, motion axes, a
camera and a test instrument, stop safely on any interlock, record a traceable
result for every unit, and keep recipe changes under control. Rule-based vision
also rejects good units on harmless cosmetic variation (false calls), which
costs yield and operator time.

**For the owner.** Employers ask for experience integrating PLCs, motion, vision
and instruments, but a fresh graduate has no access to a machine. A typical
web or CRUD portfolio cannot show that skill.

**What this project is.** A complete station controller running against a
software simulator. It is not validated on real hardware and says so everywhere.

## 2. Objectives and success criteria

| # | Objective | How it is checked |
|---|---|---|
| O1 | Run the full cycle unattended | A 100-unit lot completes against the simulator, every result is saved, and the UI stays responsive |
| O2 | Fail safely | Door open, axis timeout and instrument not responding each raise the right alarm, stop motion and recover after Reset (integration tests) |
| O3 | Hardware-independent | UI and sequence compile against interfaces only; tests swap in fakes with no UI change |
| O4 | Traceable | Every result and every recipe change records who, what, when, old and new value; a recipe change needs a password and a reason |
| O5 | AI inspection with a human in the loop | Classifier scored on a held-out synthetic test set (target: at least 95% accuracy), reported as measured in [TEST-PLAN.md](TEST-PLAN.md); false-call and escape rates shown in the app. Scoring on real images is future work (section 7) |
| O6 | Explainable by the owner | Each phase ends with a plain-language walkthrough; CI builds and tests every push |

### Non-goals

- Drivers for real hardware.
- Functional-safety certification. A real emergency stop is hard-wired; the
  software only reacts to it.
- SECS/GEM, multi-station lines, cloud services, GPU training.
- A factory server. Only the `IResultPublisher` seam is built: results go to
  local files by default, and an HTTP adapter that is switched off by default
  shows how a server would be reached.

## 3. Target audience

| Audience | What they need from it |
|---|---|
| **Primary:** hiring managers and senior engineers at machine builders (vision, test and automation equipment) and at assembly and test factories | Evidence of C#, WPF/MVVM, SQL and device integration, in a codebase the candidate can explain line by line |
| **Secondary:** the owner | Small, conventional code and a prepared reason for each design decision |
| **Simulated users in the app:** Operator, Engineer, Admin | Operator runs lots and reviews AI verdicts. Engineer edits recipes with sign-off. Admin manages users and reads the audit trail |
| **Future consumer:** a factory server | Per-unit results through `IResultPublisher` |

## 4. Scope by phase

One machine cycle: load unit, move to camera, capture image, vision inspect,
move to test position, electrical test, sort pass or fail, unload.

| Phase | Deliverable | Skill it demonstrates |
|---|---|---|
| 1 | Simulator, machine state machine (Idle, Homing, Ready, Running, Paused, Alarm, EStop), basic WPF screen with live state and alarm list | State machines, MVVM, Modbus, async |
| 2 | Full cycle: Modbus motion, OpenCV inspection with per-recipe thresholds, SCPI electrical test with limits, results in SQLite, lot summary with yield | Motion, vision, instruments, SQL |
| 3 | Recipes, users and roles, hashed passwords, audit trail, sign-off on recipe change | Data integrity, access control |
| 4 | VB.NET legacy driver, `IResultPublisher` with a local implementation, GitHub Actions | Legacy interop, CI |
| 5 | AI classifier, operator review screen, false-call tracking | Applied deep learning in C# |
| 6 | README with diagram and screenshots, design spec, test plan with results, demo script | Engineering documentation |

Fault injection in the simulator: door open, emergency stop, air pressure low,
axis timeout, instrument not responding.

Testing scope: unit tests for state machine transitions, sequence logic against
fake devices, pass/fail limit logic and the audit trail; integration tests that
run a full cycle against the simulator with at least two injected faults.

## 5. Technology

The stack, the reason for each choice and the licence of every package are in
[ARCHITECTURE.md](ARCHITECTURE.md#9-technology-stack).

## 6. Deep learning model

**Choice: DINOv2 ViT-S/14 (with registers) as a frozen feature extractor, plus a
small classifier head trained in ML.NET.**

How it works:

1. Each image is resized to 224x224 and normalised with OpenCvSharp.
2. The DINOv2 ONNX model turns it into a 384-number "embedding" that describes
   what is in the image. The backbone is never retrained.
3. ML.NET trains a multiclass classifier (logistic regression) on those
   embeddings. This takes minutes on a laptop CPU.
4. The head is exported to ONNX. The app runs backbone and head through ONNX
   Runtime behind an `IDefectClassifier` interface.

Why this one:

- **It is the current approach in industrial inspection.** Strong recent methods
  (Dinomaly, CVPR 2025; AnomalyDINO; FoundAD, ICLR 2026) build on frozen DINO
  features rather than training a CNN from scratch.
- **It satisfies the constraints.** DINOv2 weights are Apache-2.0 and not gated,
  and a ready-made ONNX export exists
  (`onnx-community/dinov2-with-registers-small`), so no Python is needed.
- **It fits ML.NET.** ML.NET's own image trainer is TensorFlow-based and cannot
  export to ONNX, so "frozen backbone plus ML.NET head" is the route that honours
  "train with ML.NET, run with ONNX Runtime".
- **It can be explained in one sentence:** a pretrained model describes the
  image, and a small classifier decides.

Rejected:

| Option | Reason |
|---|---|
| DINOv3 (newest, August 2025) | Custom Meta licence and a gated download: not open-source, so it breaks the zero-cost rule. The backbone is a configured ONNX file, so it can be swapped later |
| ResNet-18 features | Works and is simpler, but is 2015 technology. Kept as the fallback if DINOv2 is too slow on CPU |
| YOLO11 / YOLO12 | AGPL-3.0, and detection needs box labels that the review workflow does not use |
| CNN trained in PyTorch | Adds Python and removes ML.NET from a C# portfolio |

Stretch goal, only if phase 5 has time: an AnomalyDINO-style heatmap (distance
between patch features and a bank of good-unit patches) to show *where* the
defect is on the review screen.

To be proven by a short experiment at the start of phase 5, not assumed: the
exact ONNX input and output names, the CPU inference time per image, and that
ML.NET's ONNX export accepts the chosen head. If the export fails, the app
loads the ML.NET model file for the head only.

As built: the backbone runs through ONNX Runtime and the head is loaded as an
ML.NET model file, which is the fallback above.

## 7. Dataset

**Primary: synthetic package images from the simulator** (trained on, and used
in the running app).

- Classes: Good, Scratch, Crack/Chip, Contamination, MarkingDefect, LeadDefect.
- About 600 images per class, fixed random seed, split 70/15/15 into training,
  validation and test.
- Variation so the task is not trivial: position and rotation offset, lighting,
  sensor noise, blur, defect size and contrast.
- Labels are exact because the generator knows what it drew. The same ground
  truth scores the rule-based OpenCV inspection (phase 2) and the AI (phase 5),
  which gives a real comparison of their false-call rates.
- The generator is built in phase 2 with these classes, so phase 5 needs no rework.

**Future work, not built: VisA, PCB subsets** (offline evaluation only).

- Real industrial images from Amazon Science: 10,821 images across 12 object
  types with normal/anomaly labels. Licence CC BY 4.0.
- It would answer "does this work on real images?": same backbone, a binary
  good/defect head, AUROC and confusion matrix reported as measured.
- Nothing in this repository has been scored on VisA or on any other real image.

**Metrics shown in the app and in the test plan:**

| Metric | Definition |
|---|---|
| False-call rate (overkill) | Good units the AI rejected / good units inspected |
| Escape rate (underkill) | Defective units the AI passed / defective units inspected |
| Operator override rate | AI verdicts changed on the review screen / verdicts reviewed |

## 8. Risks and honest limits

| Risk | Handling |
|---|---|
| Synthetic images are easier than real ones | Stated in the README; scoring on real images is future work; production accuracy is never claimed |
| The classifier misses its accuracy target | The measured number is reported in TEST-PLAN.md, not the target; the AI verdict is advisory and never changes how a unit is sorted |
| ONNX export of the ML.NET head fails | Tested first; fallback described in section 6 |
| DINOv2 is too slow on a CPU-only laptop | Measured first; fall back to the quantised model or ResNet-18 |
| Scope creep across six phases | One phase at a time, with a stop after each |
