# Architecture and Compatibility Boundaries

## Purpose

`com.innovationmr.ngo-lan` establishes a LAN session and starts NGO. It deliberately stops before MR tracking, shared-space alignment and gameplay replication.

## Layers

```text
+--------------------------------------------------+
| Host MR/Game application                         |
| UI, XR rig, anchors, colocation, gameplay         |
+--------------------------+-----------------------+
                           |
                           v
+--------------------------------------------------+
| Session facade                                   |
| NgoLanSessionController                          |
| NGO lifecycle, transport endpoint, approval hook |
+--------------------------+-----------------------+
                           |
               +-----------+-----------+
               v                       v
+--------------------------+  +---------------------+
| Discovery                |  | NGO / UnityTransport|
| LanDiscoveryService      |  | Host/Server/Client  |
| Protocol + registry      |  | ConnectionData      |
+--------------------------+  +---------------------+
```

Dependency direction is downward only. The package does not call into an MR SDK or game layer.

## Component responsibilities

### `LanDiscoveryProtocol`

- Serializes/deserializes versioned beacon JSON.
- Enforces total packet and field limits.
- Validates magic, format, game port and player counts.
- Uses the UDP sender address as the session address.

It does not authenticate the sender and does not decide whether an application version is compatible.

### `LanSessionRegistry`

- Stores bounded discovery state.
- Refreshes `LastSeen` on repeat beacons.
- Expires stale entries.
- Emits sorted snapshots isolated from internal registry state through the service.

It is intentionally independent of sockets so EditMode tests can verify TTL and capacity.

### `LanDiscoveryService`

- Owns UDP browser/advertiser sockets.
- Receives and decodes beacons in an asynchronous loop.
- Moves decoded values through a bounded queue.
- Applies queue items and publishes events from Unity `Update`.
- Stops advertising and browsing from `OnDisable`.

It does not start NGO and does not own product UI.

### `NgoLanSessionController`

- Configures `UnityTransport` for Host, Server or Client.
- Starts/stops NGO.
- Starts advertising after Host/Server startup.
- Stops discovery while in a session and resumes after stop.
- Exposes a server-side `ConnectionApprover` policy.
- Disables NGO Scene Management.

It does not wait for a remote connection before returning from `Join`, and it does not implement player/avatar synchronization.

## Lifecycle

### Client browsing

```text
Disabled/Idle -> StartBrowsing -> Browsing
Browsing -> Join -> NGO client starting
NGO stop/disconnect -> Browsing
OnDisable -> sockets closed, cache cleared
```

### Host

```text
Idle/Browsing -> StartHost
  -> stop browsing
  -> configure transport listen 0.0.0.0:<gamePort>
  -> install approval callback
  -> NGO StartHost
  -> start beacon advertising
Shutdown/NGO stop
  -> stop advertising
  -> resume browsing
```

`StartHost`, `StartServer` and `Join` are command-acceptance APIs, not completion tasks. Consumers must observe events/state for completion.

## Discovery wire contract

Current constants:

- Magic: `0x494D5231` (`IMR1`)
- Format: `1`
- Default discovery port: `47777/UDP`
- Default game port: `7777/UDP`
- Maximum packet: `4096` bytes

Logical payload:

```text
magic       int
format      int
id          string
name        string
version     string
metadata    string
players     int
maxPlayers  int
port        int (validated then converted to ushort)
```

The packet does not carry a trusted IP. The receiver builds:

```text
endpoint = UDP sender address + validated payload game port
```

### Compatibility policy

- Non-breaking display/metadata behavior can remain format 1.
- Adding a field that old readers may safely ignore requires explicit tests before retaining format 1.
- Removing/renaming fields, changing meanings or changing validation rules incompatibly requires incrementing `Format`.
- Multiple format support should use explicit per-version decoders; never accept unknown formats.
- Application/game compatibility belongs to `GameVersion` and Host connection approval, not solely the discovery format.

Any protocol change requires:

1. Update protocol implementation.
2. Update encode/decode tests.
3. Add old/new compatibility cases.
4. Update README and this document.
5. Perform mixed-version device testing.

## Trust boundaries

### Untrusted inputs

- Every beacon field, including ID, name, metadata and advertised port.
- UDP sender frequency and number of identities.
- NGO `ConnectionData` payload.
- Client identity and MR capability claims.

### Existing resource controls

- Maximum packet size.
- Per-field/value validation.
- Bounded receive queue.
- Per-frame processing budget.
- Bounded session registry.
- Session TTL.

These controls improve robustness but do not authenticate sessions. On a shared or hostile LAN, an attacker can still spoof rooms or fill discovery capacity. Production authentication must occur during NGO connection approval using a signed/short-lived payload or an application-approved equivalent.

## Thread boundary

- UDP `ReceiveAsync` continuation may run outside the Unity main thread.
- It may decode pure data and enqueue under a lock.
- It must not touch Unity scene objects or invoke product UI from that path.
- `Update` drains a bounded number of values and publishes `SessionsChanged` on the Unity main thread.
- Background exceptions are staged and reported from `Update`.

Future edits must preserve this boundary.

## Extension strategy

Prefer application-side adapters:

```text
MrLobbyAdapter
  consumes NgoLanSessionController

MrConnectionPolicy
  assigns ConnectionApprover

MrRigNetworkPose : NetworkBehaviour
  synchronizes tracking poses

MrSharedSpaceCoordinator
  maps tracking space to shared space
```

If a new concern is broadly reusable but optional, add an interface or separate assembly. Do not add direct dependencies from Runtime to a specific vendor SDK.

## Deliberate exclusions

- Relay and cloud matchmaking
- Encryption and host authentication in discovery
- IPv6 multicast
- Cross-subnet discovery
- NAT traversal
- Automatic scene synchronization
- Player prefab architecture
- Spatial Anchor transport
- Voice/video transport

A request for one of these is an architectural extension, not a small patch. Document the design and threat model before implementation.

## Test ownership

### EditMode, package-owned

- Protocol serialization and rejection
- Registry expiry and capacity
- Pure policy/parser helpers added later

### PlayMode, host-project-owned

- `NetworkManager` and prefab correctness
- Host/client lifecycle
- Approval callbacks and disconnect reasons
- Scene/application transitions

### Device/integration, product-owned

- Firewall and permissions
- Real LAN broadcast behavior
- Multiple headsets/clients
- Sleep/background recovery
- Shared-space/Anchor correctness

Static inspection cannot substitute for either PlayMode or device tests.
