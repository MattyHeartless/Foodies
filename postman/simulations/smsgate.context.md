# SMSGate simulation context

Dependency: https://api.sms-gate.app, POST /3rdparty/v1/messages. Contract taken from the user-provided working request and official sending-messages documentation; see contracts/smsgate.openapi.json. No provider credentials or real recipients are in this simulation.

Run: postman simulation run postman/simulations/smsgate.sim.yaml --port 4605 --no-history

In another PowerShell session: set FOODIES_SMS_SIMULATION to http://127.0.0.1:4605, then dotnet test backend/Foodies.Tests -c Release --filter FullyQualifiedName~SmsGatewayTests. The test-only DelegatingHandler routes the adapter's unchanged HTTPS configuration to this local HTTP simulator using x-mock-slug=smsgate. Production has no HTTP bypass.

Verified: native JSON structure, Basic fixture authentication, valid UTF-8, stable message id, 300-second TTL, exactly 180 characters, accepted Pending acknowledgement, HTTP failures and malformed acknowledgements. HTTP simulation covers PIN, OrderPlaced to a store recipient and OrderReady to a customer recipient, plus a 500 failure; all calls remain local. SQL tests establish two unique outbox rows across repeated placement/ready requests, private per-store kitchen phone, manager-only edits, validation, and optional disabling. The full backend suite passed 51 tests; the focused gateway and SQL checks passed 9 tests with HTTP simulation enabled. Pending proves submission only. Provider duplicate-ID semantics and handset delivery of the two new order templates remain unverified.

Full SQL integration tests could not complete because SQL Server localhost:1433 was unreachable on 2026-11-05. 22 SQL-dependent tests failed on connection; 27 other tests passed in that initial run. A subsequent focused run passed all 8 gateway tests with the simulation enabled. No real SMS was sent.
