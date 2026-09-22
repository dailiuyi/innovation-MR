# Agent Guide

## Repository purpose

This workspace currently contains one reusable Unity Package Manager package:

- `Packages/com.innovationmr.ngo-lan`

It provides Netcode for GameObjects 2.12 session startup plus IPv4 UDP LAN discovery. It is infrastructure for an MR game, not a complete Unity project and not an MR/XR implementation.

## Start here

Before changing code, read in this order:

1. `Packages/com.innovationmr.ngo-lan/README.md`
2. `Packages/com.innovationmr.ngo-lan/Documentation~/AGENT_INTEGRATION.md`
3. `Packages/com.innovationmr.ngo-lan/Documentation~/ARCHITECTURE.md`
4. The Runtime source relevant to the task
5. Existing EditMode tests

Do not infer behavior from the upstream Warcraft project. This package is the maintained implementation; upstream is provenance only.

## Supported baseline

- Unity: 6 (`6000.0` minimum declared)
- NGO: `com.unity.netcode.gameobjects` `2.12.0`
- Transport: `UnityTransport`
- LAN discovery: IPv4 UDP broadcast
- Unsupported discovery target: WebGL

Do not silently upgrade Unity or NGO. Treat dependency-version changes as compatibility work requiring API review and Unity compilation.

## Package boundaries

### Infrastructure owned here

- NGO Host/Server/Client startup and shutdown
- Unity Transport endpoint configuration
- LAN beacon encode/decode, discovery cache and expiry
- Connection-approval extension point
- UI-independent lobby sample

### Application-owned; do not add to this package by default

- XR SDK integration, tracking, hands, controllers and avatars
- Spatial Anchor persistence/sharing
- Colocation calibration and shared-coordinate alignment
- Gameplay state, scenes, maps and player classes
- Product UI and platform-account authentication
- Relay/cloud matchmaking

Add application behavior through a separate assembly that consumes this package. Do not make Runtime depend on MR/XR SDKs.

## Core public entry points

- `InnovationMR.NgoLan.Session.NgoLanSessionController`
  - `StartHost(bool)`
  - `StartServer(bool)`
  - `Join(LanSessionInfo, byte[])`
  - `JoinDirect(string, ushort, byte[])`
  - `StartBrowsing()`
  - `Shutdown()`
  - `ConnectionApprover`
- `InnovationMR.NgoLan.Discovery.LanDiscoveryService`
  - `SessionsChanged`
  - `DiscoveryError`
  - `Sessions`
- `InnovationMR.NgoLan.Discovery.LanSessionInfo`

A `true` return from a startup method means NGO accepted the local start request. It does not prove that a remote client completed connection or that UDP discovery is reachable. Use NGO events and device testing for those outcomes.

## Required invariants

Preserve these unless the task explicitly changes the protocol:

1. The connection IP comes from the UDP sender endpoint, never from advertised JSON.
2. The NGO game port comes from the validated beacon payload and must remain in `1..65535`.
3. Protocol `magic` and `format` are both checked before accepting a beacon.
4. UDP work may receive off the Unity main thread; Unity-facing events and state changes remain on the main thread.
5. Discovery queues and registries remain bounded.
6. `SessionsChanged` publishes a snapshot isolated from the mutable internal registry.
7. Disabling/destroying discovery components releases sockets and coroutines; disabling/destroying the session controller releases NGO event subscriptions.
8. Discovery data is untrusted. Authentication belongs in connection approval, not in UI filtering alone.
9. NGO Scene Management remains disabled by this package; the host application owns scene orchestration.

Any wire-format breaking change must increment `LanDiscoveryProtocol.Format`, update tests and document compatibility impact.

## Modification guidance

- Keep Runtime free of Zenject, UniTask, UGS Relay and concrete XR SDK dependencies.
- Prefer a policy/delegate/interface extension point over game-specific conditionals.
- Keep public APIs small; put protocol/cache helpers behind `internal` and test through `InternalsVisibleTo`.
- Never copy third-party Warcraft assets, names or game content into this package.
- Do not hand-create `.meta` GUIDs. Let Unity import the package, then commit generated `.meta` files from the host project workflow.

## Verification

This workspace alone has no Unity project manifest or guaranteed Unity executable. Do not claim compilation or test success from text inspection.

For static checks available without Unity:

```bash
python - <<'PY'
import json, pathlib
root = pathlib.Path('Packages/com.innovationmr.ngo-lan')
for path in root.rglob('*'):
    if path.suffix in {'.json', '.asmdef'}:
        json.loads(path.read_text(encoding='utf-8'))
print('JSON/asmdef syntax OK')
PY
```

In a host Unity project, run EditMode tests using that project's installed Unity version. A typical Windows batch invocation is:

```text
"<UNITY_EXE>" -batchmode -nographics -quit -projectPath "<PROJECT_PATH>" -runTests -testPlatform EditMode -testResults "<PROJECT_PATH>/TestResults/ngo-lan-editmode.xml" -logFile "<PROJECT_PATH>/TestResults/ngo-lan-editmode.log"
```

Before marking networking work complete, also verify:

- Host starts and advertises.
- A second process/device discovers it.
- Client connects to the advertised game port.
- Version mismatch is rejected by application policy where required.
- Shutdown and disable release sockets; browsing can restart.
- Direct-IP join works when broadcast discovery is unavailable.
- Target MR devices satisfy local-network permissions and firewall requirements.

## Reporting rules for agents

When handing work off, state separately:

- Static checks performed
- Unity compilation status
- EditMode/PlayMode test status
- Number and type of physical devices tested
- Known platform/network limitations

Never report “tests pass” when tests were only inspected or when Unity was unavailable.
