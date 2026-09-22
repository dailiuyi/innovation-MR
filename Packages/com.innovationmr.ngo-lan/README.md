# Innovation MR NGO LAN

一个从 `Warcraft-Arena-Unity` 的网络思路中剥离并重构的 Unity 本地包：

- 使用 Netcode for GameObjects（NGO）启动 Host、Dedicated Server 和 Client。
- 使用 Unity Transport 配置监听地址及目标地址。
- 通过 UDP 广播发现同一局域网中的可加入会话。
- 不依赖 Warcraft 玩法代码、Zenject、UniTask、UGS Relay、UI 或具体 MR/XR SDK。

## 环境

- Unity 6（包声明最低 `6000.0`）
- `com.unity.netcode.gameobjects` 2.12.0
- 非 WebGL 平台；WebGL 不提供原生 UDP Socket

源项目使用 Unity `6000.4.10f1`、NGO `2.12.0`、Unity Transport `2.6.0`。本包固定 NGO 2.12.0，以减少 API 漂移。

## 安装

把 `Packages/com.innovationmr.ngo-lan` 复制到目标 Unity 工程的 `Packages/` 目录，或在 Package Manager 中通过本地磁盘添加其 `package.json`。

## 场景设置

1. 创建 `Network` GameObject。
2. 添加 `NetworkManager` 和 `UnityTransport`。
3. 添加 `LanDiscoveryService`。
4. 添加 `NgoLanSessionController`，绑定上面两个组件。
5. 如果需要 NGO 自动生成玩家对象，在 `NetworkManager > Player Prefab` 设置带 `NetworkObject` 的 prefab；否则留空并由业务层自行生成。
6. 从 Package Manager 导入 `Basic LAN Lobby` 示例，将其事件和方法接到世界空间 UI、手势或控制器输入。

本控制器将 NGO Scene Management 关闭，避免基础包擅自控制 MR 场景。你的应用应自行加载场景，并让所有参与端使用一致的 NetworkPrefab 配置。

## 文档入口

- 自动化 Agent / 新接手开发者：[`Documentation~/AGENT_INTEGRATION.md`](Documentation~/AGENT_INTEGRATION.md)
- 分层、生命周期和协议兼容规则：[`Documentation~/ARCHITECTURE.md`](Documentation~/ARCHITECTURE.md)
- 工作区级修改与验收规则：仓库根目录 [`AGENTS.md`](../../AGENTS.md)

## API

```csharp
controller.StartBrowsing();
controller.Discovery.SessionsChanged += sessions => { /* 更新 MR 世界空间房间列表 */ };

controller.StartHost(advertise: true);
controller.Join(selectedSession, connectionPayload);
controller.JoinDirect("192.168.1.20", 7777, connectionPayload);
controller.Shutdown();
```

`StartHost`、`StartServer` 和 `Join` 返回 `true` 仅表示 NGO 接受了本地启动请求；它不证明远端已经连接，也不证明 LAN 广播可达。连接完成和失败应通过事件、NGO 状态与 `LastDisconnectReason` 判断。

`LanSessionInfo.Metadata` 是面向业务的短字符串，可放小型 JSON，例如玩法、空间标识或锚点协商方式。它不是可信输入；接收后必须由业务层验证。

## MR 接入边界

基础网络层只负责“发现并建立 NGO 会话”，不负责：

- 头显、手柄、手部或 Avatar 的状态同步；
- Spatial Anchor 的创建、共享和解析；
- Colocation/共同空间校准；
- 权限 UI、平台账户和云端 Relay。

建议在独立 `NetworkBehaviour` 中同步头和手的局部姿态，并通过一个 `IMRAlignmentProvider` 风格的业务接口把设备坐标转换到共享空间。Anchor ID 或校准模式只应作为受验证的会话元数据/连接 payload 传递，不要写入此包的发现协议核心。

## LAN 行为与限制

- Discovery 默认广播到 UDP `47777`，游戏默认连接到 UDP `7777`。
- 最终连接 IP 使用收到数据报的发送者地址，端口使用主机声明的 NGO 端口。
- 报文校验 magic、协议版本、长度、端口、人数和字段长度。
- 会话默认 4 秒未刷新即过期，缓存最多 64 个会话；接收队列默认最多 256 条且每帧最多处理 64 条。
- 当前为 IPv4 广播，不跨路由器/VLAN，VPN、热点、企业 Wi-Fi 和移动系统本地网络权限可能阻止发现；此时可使用 `JoinDirect`。
- 广播未加密也未认证，只适用于可信 LAN。若场景含不可信设备，应在连接审批中验证一次性口令/HMAC，并限制 payload 大小和客户端频率。

## 连接审批

默认审批只检查人数上限。生产项目应在启动 Host/Server 前设置 `ConnectionApprover`，校验：

- 应用协议版本；
- 用户/设备身份；
- 房间口令或短期签名；
- MR 体验所需能力和 Anchor/地图兼容性；
- `ConnectionData` 最大长度。

## 测试

包内包含 EditMode 纯逻辑测试，覆盖：

- UTF-8 会话报文往返；
- 发送者 IP 与声明游戏端口的组合；
- 畸形/超大报文拒绝；
- TTL 边界和重复发现续期；
- 会话容量上限。

还应在目标设备上完成双实例/双设备 PlayMode 测试，特别是 Quest/Android 的本地网络权限、防火墙、多网卡与同机多实例 UDP 端口复用。

## 来源与许可证

网络分层和 LAN 广播方案参考并重构自 `Reinisch/Warcraft-Arena-Unity` commit `012d73a3e318040c5d2b3dfc0d7b8ca968b63eb2`。源代码采用 Unlicense；详见 `THIRD_PARTY_NOTICES.md`。未复制其 Warcraft/Blizzard/其他第三方美术、音频、商标或游戏内容。
