# CardCore spec: RulesetEvent

**Date:** 2026-10-24
**Status:** Implemented (uncommitted at time of writing)
**Written retroactively** from the code and tests; no design discussion is recorded.

## Problem

Rulesets track state the engine has no model for: turn counters, granted resources, auto-resolved ticks.
That state must survive save/load and replay, so it has to live in the event log.
Without a generic event, each ruleset would force a new `GameEvent` subtype and a `GameState.Apply` case in the engine.

## Design

- `RulesetEvent : GameEvent` carries `string Kind` and `JObject Payload`.
- The engine logs, serializes and replays it but never interprets either field.
  `GameState.Apply` is a no-op for it. A ruleset derives its own state by replaying these events.
- `EmitRulesetEventCommand(string kind, JObject payload)` emits exactly one `RulesetEvent`.
  - Constructor throws `ArgumentException` for null/empty/whitespace `kind`, and `ArgumentNullException` for null `payload`.
  - The payload is deep-cloned on construction and again on `Execute`, so caller mutation cannot change the log.
  - `CanExecute` requires `state.IsStarted`: `LoadEventLog` demands `GameStarted` first, so an earlier ruleset event would produce an unloadable log.
- `GameEventConverter` maps the `RulesetEvent` type name for polymorphic JSON.

## Consequences

- Ruleset events count in `GetEventLog`, `GetStateAtIndex` indexing and `LoadEventLog` like any other event.
- `GameState` is unchanged by them, so `GetStateAtIndex` across a ruleset event returns an identical state; the ruleset must replay the log for its own state.
- Payload comparison needs `JToken.DeepEquals`, not `==`.
- The engine cannot validate `Kind` or `Payload`; typos in `Kind` are the ruleset's problem.
- Adding a new `Kind` never needs an engine change or a doc update beyond the ruleset's own docs.

## Tests

`Tests~/PureCSharp/RulesetEventTests.cs` (12 tests): constructor validation, `CanExecute` before start, append with kind and payload, no payload aliasing, `Apply` no-op, log interleaving with card events, JSON round-trip, `LoadEventLog` replay, `GetStateAtIndex` indexing.

## Docs

`unity-client.md` documents `EmitRulesetEventCommand` and `RulesetEvent`. Architecture diagram: `docs~/architecture.md`.
