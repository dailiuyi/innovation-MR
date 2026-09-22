# Agent Integration Contract

本文档面向任何自动化编码 Agent 或新接手的开发者。目标是在没有原始剥离过程上下文的情况下，将本包接入一个 Unity 6 MR 项目。

## 1. 前置条件

宿主项目必须满足：

- Unity 6；
- `com.unity.netcode.gameobjects` 2.12.0；
- 使用 `UnityTransport`；
- 目标平台允许原生 IPv4 UDP Socket；
- Host 与 Client 位于允许广播和点对点 UDP 的网络中，或准备使用 Direct IP；
- 已明确 MR/XR SDK，但该 SDK 不应成为本包 Runtime 的依赖。

如果目标是 WebGL、跨公网匹配、跨 VLAN 发现或 NAT 穿透，请停止直接套用 LAN Discovery；这些需求超出本包边界。

## 2. 导入方式

将目录完整复制到宿主项目：

```text
<PROJECT>/Packages/com.innovationmr.ngo-lan
```

或者通过 Unity Package Manager 的 Add package from disk 选择：

```text
<PACKAGE>/package.json
```

不要只复制 `Runtime/*.cs`，否则会遗漏 asmdef、测试、许可证和版本契约。

## 3. 场景对象契约

在启动场景中创建一个常驻对象，最少挂载：

```text
NetworkRoot
├── NetworkManager
├── UnityTransport
├── LanDiscoveryService
└── NgoLanSessionController
```

在 `NgoLanSessionController` Inspector 中绑定：

- `Network Manager` → 同对象的 `NetworkManager`
- `Discovery` → 同对象的 `LanDiscoveryService`
- `Game Port` → 默认 `7777`，按项目需要统一修改
- `Max Players`
- `Game Version`
- `Session Name`
- `Metadata`

注意：

- `Reset()` 只在编辑器添加/重置组件时尝试自动查找引用；不要把它当作运行时依赖注入。
- 如果对象跨场景存在，由宿主应用负责 `DontDestroyOnLoad` 和重复实例治理。
- 本包会关闭 NGO Scene Management，场景切换由宿主应用负责。

## 4. 最小 Lobby 适配器

```csharp
using System.Collections.Generic;
using InnovationMR.NgoLan.Discovery;
using InnovationMR.NgoLan.Session;
using UnityEngine;

public sealed class MrLobbyAdapter : MonoBehaviour
{
    [SerializeField] private NgoLanSessionController controller;
    private IReadOnlyList<LanSessionInfo> sessions;

    private void OnEnable()
    {
        controller.Discovery.SessionsChanged += OnSessionsChanged;
        controller.SessionError += OnSessionError;
        controller.StartBrowsing();
    }

    private void OnDisable()
    {
        controller.Discovery.SessionsChanged -= OnSessionsChanged;
        controller.SessionError -= OnSessionError;
    }

    public void Host() => controller.StartHost(advertise: true);
    public void Join(int index) => controller.Join(sessions[index]);
    public void Leave() => controller.Shutdown();

    private void OnSessionsChanged(IReadOnlyList<LanSessionInfo> value)
    {
        sessions = value;
        // Rebuild world-space MR room cards here.
    }

    private void OnSessionError(string message)
    {
        // Present a non-blocking error in the MR UI.
    }
}
```

`StartHost`、`StartServer` 和 `Join` 返回 `true` 只代表 NGO 接受了本地启动调用，不代表远端握手已经完成。连接成功、断开和拒绝原因应结合：

- `PeerConnected`
- `PeerDisconnected`
- `SessionStopped`
- `LastDisconnectReason`
- `NetworkManager` 的连接状态

## 5. 生产连接审批

发现报文不可信。仅在客户端比较 `GameVersion` 不能阻止绕过 discovery 的直接连接。Host/Server 启动前必须按产品威胁模型设置 `ConnectionApprover`：

```csharp
controller.ConnectionApprover = request =>
{
    if (request.Payload == null || request.Payload.Length == 0 || request.Payload.Length > 512)
        return (false, "Invalid join payload");

    bool accepted = VerifyProtocolIdentityAndSignature(request.Payload);
    return accepted
        ? (true, string.Empty)
        : (false, "Connection rejected");
};

controller.StartHost(advertise: true);
```

建议 payload 至少包含：

- 应用协议版本；
- 房间 ID；
- 设备/用户临时身份；
- nonce 与短有效期；
- HMAC 或签名；
- 必需的 MR 能力/共享空间模式。

不要把口令、长期密钥或个人敏感信息放进 LAN beacon 的 `Metadata`。

## 6. MR 层接入

新建独立业务程序集，例如：

```text
Assets/MRNetworking/
├── InnovationMR.Game.MRNetworking.asmdef
├── MrRigNetworkPose.cs
├── MrSharedSpaceCoordinator.cs
└── MrLobbyAdapter.cs
```

该程序集可以引用：

- `InnovationMR.NgoLan.Runtime`
- 选定的 XR SDK
- 游戏业务程序集

依赖方向必须是：

```text
MR/Game layer -> InnovationMR.NgoLan.Runtime -> NGO/UnityTransport
```

禁止反向让 `InnovationMR.NgoLan.Runtime` 引用具体 XR SDK 或游戏程序集。

推荐职责：

- `MrRigNetworkPose`：仅同步头、左手、右手等必要姿态；
- `MrSharedSpaceCoordinator`：处理 Anchor/Colocation 与共享坐标；
- `MrLobbyAdapter`：把会话列表映射为世界空间 UI；
- 玩家生成/所有权：使用独立 `NetworkBehaviour` 和预制体配置处理。

## 7. Metadata 契约

`LanSessionInfo.Metadata` 最好是小型、可版本化 JSON，例如：

```json
{
  "schema": 1,
  "mode": "colocation",
  "map": "lab-a",
  "anchorMode": "shared-cloud"
}
```

消费方必须：

- 捕获 JSON 解析异常；
- 校验 schema；
- 限制字段和值；
- 对未知字段保持前向兼容；
- 不信任其中的地址、身份或权限声明。

不要通过 Metadata 改变 LAN wire format；两者是不同版本层。

## 8. 验收矩阵

### 静态验收

- `package.json` 和两个 asmdef 可解析；
- Runtime 不出现 Zenject、UniTask、Unity Services、具体 XR SDK 引用；
- 公开 API 名称与 README/示例一致；
- 许可证和第三方声明存在。

### Unity EditMode

运行 `InnovationMR.NgoLan.Tests.EditMode`，至少覆盖：

- UTF-8 beacon 往返；
- sender IP + advertised game port；
- 畸形、外来、超大报文拒绝；
- TTL 边界；
- 重复发现续期；
- registry 容量。

### 双进程/双设备 PlayMode

1. Host 调用成功并开始广告；
2. Client 在预期时间内发现 Host；
3. Client 连接到广告端口；
4. 连接审批可接受有效 payload、拒绝无效 payload；
5. Host 退出后会话在 TTL 后消失；
6. Client 断线后恢复浏览；
7. 禁用/销毁对象后端口可再次绑定；
8. 广播被阻止时 Direct IP 可连接。

### 目标 MR 设备

验证：

- 本地网络权限；
- 系统/路由器防火墙；
- Wi-Fi AP 是否启用 client isolation；
- 休眠/切后台/恢复；
- 多网卡、热点、VPN；
- Anchor 或共享坐标失败时网络会话是否可安全退出。

## 9. 常见失败诊断

| 症状 | 优先检查 |
|---|---|
| 完全发现不到 Host | UDP 47777、防火墙、AP isolation、移动平台本地网络权限、是否同一广播域 |
| 能发现但无法连接 | UDP 7777、Host `0.0.0.0` 监听、广告端口和 transport 端口是否一致 |
| Direct IP 可用但发现不可用 | 网络禁止广播；继续使用 Direct IP 或另做目录服务 |
| `StartHost` 返回 false | `NetworkManager`/`UnityTransport` 引用、已有实例是否监听、端口占用 |
| `Join` 返回 true 但没有连接 | 这只表示 StartClient 已发起；查看 NGO 回调和 `LastDisconnectReason` |
| 版本不匹配 | Inspector 的 `gameVersion` 与 beacon；同时检查 Host 审批 payload 的协议版本 |
| 退出后无法重新绑定 | 检查对象生命周期、是否调用 `Shutdown`、是否存在重复 `NetworkManager` |

## 10. Agent 交付格式

完成集成后，Agent 应明确报告：

```text
Package path:
Unity version:
NGO version:
Static checks:
Unity compile:
EditMode tests:
PlayMode tests:
Physical devices tested:
Connection approval policy:
Known limitations:
Files changed:
```

未知项写“未验证”，不要用“应该可用”替代测试结果。
