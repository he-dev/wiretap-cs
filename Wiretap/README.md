

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
- Wiretap treats contracts as activities.

----

## Specification

- The central unit of observation is an **activity** that represents a named contract you want to observe.
- Every activity must end with exactly one **status** that records its outcome. 
- Both activities and statuses can log through `state` additional structured data about their context.
- There are two types of activities:
    - `buzz`: these activities have a duration; They usually log a single status but can log two: `zero` for when they start and `okay`, `fail`, or `void` for how they ended.
    - `snap`: these activities do not have a duration. They log only a single status.
- `okay`: means the activity completed along an expected coded path. For example, finding zero records, choosing a fallback path, or rejecting invalid input can all be `Ok` if those are programmed outcomes.
- `fail`: means that something went wrong during the activity.
- `void`: means the activity outcome is not known. Nothing went particularly wrong but it dit not reach `okay` either.
- Activities are hierarchical.
- Activities can contain inner activities that can be counted as batches. Their statistics are automatically attached to the `state`.
- Activities use generics an typed contracts to prevent accidental usage of wrong statuses on foreign activities.


## Advantages

Telemetry becomes **explicit, structured, and type-checked**. Instead of scattering freeform information logs through the codebase, the system defines named activities and status factories that describe the observable contract.

This improves consistency. The same activity produces the same shape of telemetry across call sites. Status messages, dimensions, counters, reasons, overloads, durations, and exceptions are centralized in the contract definitions rather than recreated manually at every log statement.

It also improves correctness. The generic activity/status relationship prevents logging a status for the wrong activity. Required context, such as an overload or outcome, is enforced instead of being silently omitted. Runtime failures in telemetry are treated as defects in the program’s observability contract.

The activity/status model makes telemetry easier to query and aggregate. Observations have stable names and common dimensions such as activity, channel, status, reason, overload, duration, and contract-specific measurements. This supports questions like which activities ran, which expected paths they took, how often they failed, how long they took, and what result values they produced.

The model avoids mixing contractual observations with diagnostic commentary. Information-level telemetry remains meaningful and stable, while debug and trace logging can still be used for exploratory or implementation-level details without weakening the contract layer.

Finally, the framework encourages deliberate modeling. If something matters operationally, it must be represented as part of an activity status or as a separate activity. If it does not matter enough to be part of the contract, it should remain diagnostic. This keeps the observable surface of the system intentional rather than accidental.


## about statuses

Framework-emitted statuses are intentionally structural.

They describe only what the framework can observe about the activity scope, not what the underlying operation has actually done.

`Zero` is the zeroth status marker of the observation. It does not mean the business operation has started.

`Last` is the final structural status marker. It does not imply success, failure, completion, cancellation, or incompletion.

`Void` is a final structural status for activities whose contract says no user-visible outcome status may be emitted.

`Leak` is a structural marker that a terminal status escaped after a terminal status had already been observed.

