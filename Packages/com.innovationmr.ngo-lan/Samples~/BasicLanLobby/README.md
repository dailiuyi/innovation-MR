# Basic LAN Lobby Sample

这是一个代码示例，不含 Scene 或 Prefab。

1. 在场景中创建一个对象，添加 `NetworkManager`、`UnityTransport`、`LanDiscoveryService` 和 `NgoLanSessionController`。
2. 把 `NetworkManager` 与 `LanDiscoveryService` 引用绑定到控制器。
3. 另建对象并添加 `BasicLanLobby`，绑定控制器。
4. 将你的世界空间 UI/手势操作分别调用 `Host()`、`Join(index)` 和 `Leave()`。
5. 根据 `Discovered` 列表生成 MR 房间卡片；列表变化由 `LanDiscoveryService.SessionsChanged` 驱动。

若需要认证，请在启动主机前设置：

```csharp
sessions.ConnectionApprover = request =>
{
    bool valid = ValidatePayload(request.Payload);
    return (valid, valid ? string.Empty : "Invalid join token");
};
```
