# Historical protocol `/4` bounded-regression tests

This project is intentionally outside `Infinium.sln`. It links the existing
`tests/Infinium.EvaluationTests/EvaluatorV2PublicProtocolTests.cs` source at its
immutable tracked path and is invoked only by the accepted bounded-regression
wrapper.

The assembly name remains `Infinium.EvaluationTests` solely because the frozen
`/4` evaluator grants that exact historical friend-assembly identity. This is
not a default product test project and must not be referenced by product,
current public-fixture, or default solution projects.
