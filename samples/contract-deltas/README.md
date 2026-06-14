# Contract Delta Fixtures

These fixtures are deterministic reducer inputs. Each one names the sample repository it is intended for and the expected classification shape.

Use the generic `samples/contract-delta.example.json` for quick CLI demos. Use these fixtures when testing a specific repository so accidental generic-name matches are easier to understand.

## Fixtures

| File | Target repo | Purpose |
| --- | --- | --- |
| `contract-delta-v2.example.json` | any indexed repo | Structured v2 shape covering property, endpoint, SQL table, and package references. |
| `modern-sample.customer-profile.json` | `samples/modern-sample` | Full semantic match for `CustomerProfileResponse.primaryEmail`. |
| `jvm-modern.order-status.json` | `samples/jvm-modern-sample` | JVM semantic match for `OrderResponse.status`. |
| `servicebus.transient-status.json` | `c-sharp-sample-repos/ProjectExtensions.Azure.ServiceBus` | Generic `status` syntax/textual match in a reduced-coverage legacy repo. |
| `fluentjdf.status-builder.json` | `c-sharp-sample-repos/fluentjdf` | Generic `status` match with Tier1 method invocation evidence and warning-worthy fan-out. |

External sample repos are optional local fixtures and are not required for normal unit tests.
