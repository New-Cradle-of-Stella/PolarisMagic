# PolarisMagic 设计规范

## 文件

每个魔法只有三个文件：

```text
ExampleMagic.pmagic       # 静态参数
ExampleMagic.pmagic.g.cs  # 生成的注册代码
ExampleMagic.pmagic.cs    # 用户实现
```

## 静态定义

`.pmagic` 保存：

- `Id`
- `MpCost`
- `CastTime`
- `MpCrystalizeRatio`
- `NeutralCrystalizeRatio`
- `PrepareTime`
- `ManaDrainLock`
- `ProjectilePower`
- `ShotgunRatio`
- `SuperArmorTiredTime`

数字 ID 由 PolarisMagic 分配并持久化。原版 `def_aim` 固定为 `NORMAL`。

## 用户回调

一次施法只调用一次 `RunAsync`，不会每帧重复调用：

```csharp
private async Task RunAsync(
    MagicRuntimeContext context,
    CancellationToken cancellationToken)
{
    var magicObject = context.Magic.CreateObject();
    magicObject.Position = context.Self.Position;
    magicObject.AttachImage("example.fireball");
    magicObject.AttachEffect("example.fire_trail");

    while (!cancellationToken.IsCancellationRequested)
    {
        magicObject.Position += magicObject.Velocity * context.Clock.DeltaFrames;
        await context.NextTickAsync(cancellationToken);
    }
}
```

- Task 未完成：魔法继续运行。
- Task 完成、取消或异常：魔法立即结束。
- 外部击杀、切图或关闭组件：取消 Token。
- 清理使用 `try/finally`。

## 中间层

PolarisMagic 只在内部按 Tick 推进 await，不会再次调用 `RunAsync`。它负责：

- 适配原版 `MagicItem`、holder、对象池和 Tick。
- 完全接管自定义魔法的运行回调，不继续调用原版 callback。
- 将 Task 续体恢复到游戏主线程。
- 提供 `NextTickAsync`、`DelayFramesAsync`、`WaitUntilAsync`。
- 通过 `context.Magic` 创建魔法对象，并挂载图片和特效。
- 在取消后继续驱动 `finally`，完成后释放实例。
- 隔离并发施法的 Context、Token、资源和 code-behind 实例。

```csharp
MagicObject CreateObject();
MagicImageHandle MagicObject.AttachImage(string resourceId);
MagicEffectHandle MagicObject.AttachEffect(string resourceId);
```

对象及其挂载项归当前施法所有，Task 结束时自动清理；也可以提前 `Dispose`。

`MagicObject` 和 `MagicEntity` 显式实现 `Polaris.Drawing.IMapDrawTarget`，可以直接交给别的地图 Drawing Surface `Follow`；目标失效（对象已释放 / 实体已回池）后跟随方按 `MapTargetLostBehavior` 处置。

作者不能访问原版 `MagicItem`、`MGContainer`、holder、`phase`、`t` 或 Raw 对象，只使用稳定的 `MagicRuntimeContext`。
