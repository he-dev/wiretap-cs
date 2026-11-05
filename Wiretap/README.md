

# Concepts

Wiretap is an attempt to create a framework that builds on top of standard logging interfaces and adds a layer of reliable monitoring contracts.

Typical logging conventions that log bare messages like `logging.info(...)` have a lot of disadvantanges.

- logs are not predicatable so monitoring is difficult to setup
- logs are easy to destroy during refactorings because their purpose is unknown
- logs related to monitoring are not easily identifieable
- logs are not consistent
- logs are not reliably filterable
- logs playing a role in monitoring are not easy to find in code

- Wiretap tries to formalize logging contracts and to make them visible, enforcable, and testable because contracts can be used in tests to simulate certain situations that monitoring is supposed to recognize without the necessity to similate real errors.
- Wiretap truns logging contracts into features.
- Wiretap applies only to `info`, `warning` and `error` level logs. `debug` and `trace` remain casual.
- Wiretap establishes a new way of thinking about logging.
- Wiretap gives logs meaning, thus helping to standardize their pattern which makes it more convenient to work with them for everyone; both authors and readers.
- Wiretap is a promise what a system is going to deliver.
- Wiretap treats contracts as plots.

----

## Terminology

- Plots are opened with `BeginPlotAbout` and incoming trace context with `ContinuePlotAbout`.
- Plot events are `ZERO`, `TICK`, and `LAST`.
- Stories are `Story.Core` and `Story.Buzz`, replacing `Domain` and `System` respectively. The telemetry key is `plot.story`.
- `SNAP` is a free event emitted through `logger.Snap(about, story)`.
- `BEEP` reports framework findings with `Story.Buzz` and `context.beep_code`. Invalid sequences use Error and remarks use Debug. Enabled finding codes are emitted once per plot and do not enter lifecycle history.

The following symbolic example shows the field names and possible values.

```json
{
    "plot": {
        "about": "LoadWorkflow",
        "event": "ZERO|TICK|LAST|SNAP|BEEP",
        "story": "CORE|BUZZ",
        "duration_ms": 0
    }
}
```

C# event and story names use PascalCase. The specification below retains earlier design details beyond these terminology changes.

## Specification

- The central unit of observation is a **plot** that represents a named contract you want to observe.
- Every plot must end with exactly one **status** that records its outcome.
- Both plots and statuses can log through `state` additional structured data about their context.
- Plots have a duration.
- `okay`: means the plot completed along an expected coded path. For example, finding zero records, choosing a fallback path, or rejecting invalid input can all be `Ok` if those are programmed outcomes.
- `fail`: means that something went wrong during the plot.
- `void`: means the plot outcome is not known. Nothing went particularly wrong but it dit not reach `okay` either.
- Plots are hierarchical.
- Plots can contain inner plots that can be counted as batches. Their statistics are automatically attached to the `state`.
- Plots use generics an typed contracts to prevent accidental usage of wrong statuses on foreign plots.


## Advantages

Telemetry becomes **explicit, structured, and type-checked**. Instead of scattering freeform information logs through the codebase, the system defines named plots and status factories that describe the observable contract.

This improves consistency. The same plot produces the same shape of telemetry across call sites. Status messages, dimensions, counters, reasons, overloads, durations, and exceptions are centralized in the contract definitions rather than recreated manually at every log statement.

It also improves correctness. The generic plot/status relationship prevents logging a status for the wrong plot. Required context, such as an overload or outcome, is enforced instead of being silently omitted. Runtime failures in telemetry are treated as defects in the program’s observability contract.

The plot/status model makes telemetry easier to query and aggregate. Observations have stable names and common dimensions such as plot, channel, status, reason, overload, duration, and contract-specific measurements. This supports questions like which plots ran, which expected paths they took, how often they failed, how long they took, and what result values they produced.

The model avoids mixing contractual observations with diagnostic commentary. Information-level telemetry remains meaningful and stable, while debug and trace logging can still be used for exploratory or implementation-level details without weakening the contract layer.

Finally, the framework encourages deliberate modeling. If something matters operationally, it must be represented as part of a plot status or as a separate plot. If it does not matter enough to be part of the contract, it should remain diagnostic. This keeps the observable surface of the system intentional rather than accidental.


## about statuses

Framework-emitted statuses are intentionally structural.

They describe only what the framework can observe about the plot scope, not what the underlying operation has actually done.

`Zero` is the zeroth status marker of the observation. It does not mean the business operation has started.

`Last` is the final structural status marker. It does not imply success, failure, completion, cancellation, or incompletion.

`Void` is a final structural status for plots whose contract says no user-visible outcome status may be emitted.

`Leak` is a structural marker that a terminal status escaped after a terminal status had already been observed.

