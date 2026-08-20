# PolarisMagic 实现方案

## 1. 分层

```text
作者 RunAsync
    ↓
PolarisMagic 中间层
    ↓
PolarisCore 游戏包装与补丁
    ↓
原版 MagicItem / MGContainer
```

作者只依赖 PolarisMagic 的稳定 API，不接触原版状态。

## 2. Task 生命周期

```csharp
public delegate Task MagicTaskCallback(
    MagicRuntimeContext context,
    CancellationToken cancellationToken);
```

- 正式魔法开始时调用一次，以后不再调用。
- Task 存活期间保持原版实体存活。
- Task 完成、取消或异常时立即结束实体。
- 外部击杀或切图时取消 Token。
- 取消后的 `finally` 由组件级取消泵继续驱动。

不提供阶段、`OnTick`、`Context.End` 或 `OnDispose`。

## 3. 主线程调度

`MagicSynchronizationContext` 捕获正常 await 的续体。holder 每 Tick 只推进 Task，不会重新调用作者回调：

1. 更新 `MagicClock`。
2. 完成到期等待。
3. 在游戏主线程执行续体。
4. 检查根 Task 状态。

等待 API：

```csharp
NextTickAsync(...)
DelayFramesAsync(...)
WaitUntilAsync(...)
```

作者不得在游戏线程使用 `.Wait()` 或 `.Result`。

## 4. Context

`MagicRuntimeContext` 提供：

- 当前定义和实例 ID。
- `MagicClock`。
- `MagicEntity`。
- Caster、Player、Map。
- `MagicWorldServices`。
- `MagicApi`。

所有对象都是中间层包装器。取消后禁止新的世界操作，只允许资源句柄完成幂等清理。

## 5. Magic API

```csharp
MagicObject MagicApi.CreateObject();
MagicImageHandle MagicObject.AttachImage(string resourceId);
MagicEffectHandle MagicObject.AttachEffect(string resourceId);
```

`MagicObject` 是中间层场景对象，可设置 Position、Velocity、Rotation、Scale 和 Active。图片由 PolarisRes 解析，特效由中间层特效服务创建。所有对象归当前 Task 所有并自动清理。

`MagicObject` 与 `MagicEntity` 都显式实现 `Polaris.Drawing.IMapDrawTarget`（`TryGetMapPosition` 返回当前地图坐标；`MagicObject` 已释放、`MagicEntity` 已回池/已 kill 时返回 false），因此都可以作为其它地图 Surface 的 `Follow` 目标。

## 6. 原版接入

保留必要补丁：

- MKind 注入。
- MDAT 自定义初始化。
- MGContainer holder 安装与清理。
- FEnum 字符串 ID 解析。
- MagicSelector 存档保护。

准备态由中间层处理，不创建作者 Task。正式态才调用 `RunAsync`。

正式态由中间层完全替换原版运行回调。原版只提供和回收 `MagicItem` 容器，不参与自定义魔法的运行状态。

## 7. 错误边界

映射文件无法读取时阻止整批注册；单个提供器的定义非法或字符串 ID 冲突时只跳过该定义。

运行期错误只结束当前实例：Task 异常、非法取消、主线程违规或 Context 失效访问。
