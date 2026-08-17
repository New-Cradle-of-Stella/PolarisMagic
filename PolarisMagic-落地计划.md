# PolarisMagic 落地计划

本文规定 PolarisMagic 的最终实现。魔法运行流程不再使用图结构：没有 `.pmstate`、节点目录、端口、连线、图变量、图构建器或图执行器。每个阶段就是 code-behind 中的一个 `MagicStageCallback`，阶段转移通过回调返回值完成。

## 1. 工程与目录

PolarisMagic.csproj 保持 netstandard2.1，不新增 JSON NuGet 包。数字 ID 映射使用 Polaris.Runtime.props 已引用的游戏 Newtonsoft.Json；`.pmagic` 只由 PolarisTools 在构建期读取，模组运行时只携带生成的 C# 定义。

目标目录固定为：

```text
PolarisMagic/
├─ Authoring/
│  ├─ MagicDefinitionDocument.cs
│  ├─ MagicDefinitionParser.cs
│  ├─ MagicDefinitionValidator.cs
│  ├─ MagicCodeGenerator.cs
│  └─ MagicDiagnostic.cs
├─ Definitions/
│  ├─ MagicDefinition.cs
│  ├─ MagicDefinitionBuilder.cs
│  ├─ MagicBaseParameters.cs
│  ├─ MagicDefinitionProviderAttribute.cs
│  └─ IMagicDefinitionProvider.cs
├─ Runtime/
│  ├─ MagicBehavior.cs
│  ├─ MagicStageCallback.cs
│  ├─ MagicStageResult.cs
│  ├─ MagicRuntimeContext.cs
│  ├─ MagicRuntimeInstance.cs
│  ├─ MagicRuntimeRegistry.cs
│  ├─ MagicEndReason.cs
│  └─ MagicEntity.cs
├─ Registration/
│  ├─ MagicRegistry.cs
│  ├─ MagicIdMapStore.cs
│  └─ CustomMKindInstaller.cs
├─ Integration/
│  ├─ PolarisMagicHolder.cs
│  ├─ CustomMagicInitializer.cs
│  └─ Patches/
├─ Diagnostics/
│  ├─ MagicRuntimeReporter.cs
│  └─ PatchTargetVerifier.cs
└─ PolarisMagicComponent.cs
```

以下旧类型不得保留：`MagicGraphDefinition`、`MagicGraphBuilder`、`MagicGraphExecutor`、`MagicGraphValidator`、`MagicNodeSpec`、`MagicNodeKind`、`MagicNodeCatalog`、`MagicPort*`、`MagicVariableRef`、`MagicValue` 以及只服务于图的 evaluator、槽位和连接类型。

## 2. 作者文件与生成边界

每项魔法固定由三个同名文件组成：

```text
ExampleMagic.pmagic
ExampleMagic.pmagic.g.cs
ExampleMagic.pmagic.cs
```

- `.pmagic`：用户编辑的 XML 静态定义。
- `.pmagic.g.cs`：PolarisTools 可整体覆盖，生成 partial behavior、静态参数、工厂和 provider。
- `.pmagic.cs`：用户维护的 code-behind，保存实例字段并实现阶段回调。

`.pmagic.g.cs` 和 `.pmagic.cs` 组成同一个类型。生成部分固定声明：

```csharp
internal sealed partial class ExampleMagic : MagicBehavior
{
    // 生成的静态属性只读投影。
}
```

`.pmagic.cs` 不存在时，PolarisTools 只创建能够编译的最小桩；以后不追加、重命名或删除阶段方法。工具不扫描 code-behind 控制流，也不生成流程图。

## 3. `.pmagic` 格式

根元素固定为：

```xml
<?xml version="1.0" encoding="utf-8"?>
<MagicDefinition Version="1" Id="example.fire_arrow">
  <Base
    MpCost="24"
    CastTime="36"
    MpCrystalizeRatio="0.5"
    NeutralCrystalizeRatio="0.2"
    PrepareTime="14"
    ManaDrainLock="5"
    ProjectilePower="100"
    ShotgunRatio="1.5"
    SuperArmorTiredTime="0" />
</MagicDefinition>
```

Version 固定为 1。XML 解析使用安全设置：`DtdProcessing.Prohibit`、`XmlResolver = null`、`MaxCharactersFromEntities = 0`。未知元素、未知属性、重复属性、命名空间、CDATA 和混合文本全部报错；注释与纯空白允许。

`Id` 必须匹配 `^[a-z][a-z0-9_.-]{2,127}$`。必需字段为 MpCost、CastTime、MpCrystalizeRatio、NeutralCrystalizeRatio；其余字段默认值依次为 14、5、100、1.5、0。Float 使用 `InvariantCulture`，必须是有限数；时间和费用非负，两个结晶比例在 0–1 内。

自定义静态属性后续通过显式的 `<Properties>` 封闭子格式加入，不允许借未知 XML 字段临时扩展。运行状态、阶段、攻击、资源和世界句柄都不写入 `.pmagic`。

## 4. 公开定义契约

Authoring 中立枚举只保留 `MagicDiagnosticSeverity`。PolarisMagic 不向作者公开专用的瞄准枚举或 Aim API；安装 MKind 时将原版 `def_aim` 固定为 `NORMAL`。

`MagicDefinition` 是封存后的不可变对象，包含字符串 Id、运行时 NumericId、`MagicBaseParameters`、`Func<MagicBehavior>`、提供器程序集名。NumericId 在映射分配前为 0，注册表封存时通过 internal `WithNumericId` 复制生成最终实例。

`MagicDefinitionBuilder` 的公开方法固定为：

```csharp
public MagicDefinitionBuilder(string id);
public MagicDefinitionBuilder SetBase(MagicBaseParameters value);
public MagicDefinitionBuilder SetBehaviorFactory(Func<MagicBehavior> value);
public MagicDefinitionBuilder SetProviderAssembly(string value);
public MagicDefinition Build();
```

每项只能设置一次，缺项、重复设置、空工厂返回值或非法基本参数均抛 `InvalidOperationException`。不再有 `SetGraph`。

`MagicRegistry` 公开 `TryGet(string, out MagicDefinition)`、`TryGet(int, out MagicDefinition)`、`GetRequired(string)` 和只读 `Definitions`。字符串键使用 `StringComparer.Ordinal`，Definitions 按字符串 Id ordinal 排序。注册入口为 internal `AddUnsealed`，只由组件 Start 调用。

## 5. Code-behind 与阶段契约

核心类型固定为：

```csharp
public delegate MagicStageResult MagicStageCallback();

public enum MagicStageResultKind
{
    Stay,
    Transition,
    Complete
}

public readonly struct MagicStageResult
{
    public MagicStageResultKind Kind { get; }
    internal MagicStageCallback Next { get; }

    public static MagicStageResult Stay { get; }
    public static MagicStageResult Complete { get; }
    public static MagicStageResult TransitionTo(MagicStageCallback next);
}

public abstract class MagicBehavior
{
    protected MagicRuntimeContext Context { get; }
    protected abstract MagicStageCallback CreateInitialStage();
    protected virtual void OnDispose(MagicEndReason reason) { }
}
```

`TransitionTo(null)` 立即抛 `ArgumentNullException`。`MagicStageResult` 不公开可由作者任意填写的构造函数，避免非法 Kind/Next 组合。

运行规则固定如下：

1. 每个正式魔法实体由工厂创建一个 behavior；code-behind 字段只属于本次施法。
2. runtime 先绑定 Context，再恰好调用一次 `CreateInitialStage`。返回 null 时以 `InvalidStageTransition` 结束。
3. 每次 holder.run 只调用一次当前阶段。不得在同一 Tick 内递归执行新阶段。
4. `Stay` 保留当前回调与激活序号。
5. `Transition` 替换当前回调；新回调下个 Tick 执行。
6. `Complete` 以 `Completed` 结束。
7. `Context.End()` 在回调返回后优先处理，以 `ExplicitEnd` 结束并忽略返回值。
8. 阶段抛异常时记录定义、实例、阶段激活序号和回调方法，以 `CallbackException` 结束。
9. Dispose 先设置取消状态，再调用 `OnDispose` 一次。OnDispose 异常只记录，不阻止剩余清理。

分支、循环、计数、变量、中断和回跳全部由 C# 字段及控制流实现。框架不验证某个阶段最终是否完成；长期返回 `Stay` 是合法的持续场行为。

推荐 code-behind 形态：

```csharp
internal sealed partial class ExampleMagic
{
    private float age;

    protected override MagicStageCallback CreateInitialStage() => Prepare;

    private MagicStageResult Prepare()
    {
        // 创建攻击等一次性工作。
        return MagicStageResult.TransitionTo(Fly);
    }

    private MagicStageResult Fly()
    {
        age += Context.DeltaFrames;
        return age < 90f
            ? MagicStageResult.Stay
            : MagicStageResult.Complete;
    }
}
```

## 6. 数字 ID 与存档

自定义 MGKIND 固定使用 30000–49999。映射文件固定为 `BepInEx/config/Polaris/magic-id-map.json`：

```json
{
  "version": 1,
  "entries": {
    "example.fire_arrow": 34721
  }
}
```

分配算法固定如下：

1. 启动时读取全部历史 entries；未安装魔法的条目仍永久占用原数字。
2. 本次定义按字符串 ID ordinal 排序；已有条目直接复用。
3. 新 ID 使用 UTF-8 FNV-1a 32 位哈希，候选为 `30000 + hash % 20000`；冲突时向上循环线性探测。
4. 写入时按 ID ordinal 排序，使用 Newtonsoft.Json 两空格缩进、UTF-8 无 BOM、CRLF 和末尾换行。
5. 先写同目录 `.tmp` 并 `Flush(true)`，再使用 Move 或 Replace 原子替换；启动时删除上次遗留的 `.tmp`。
6. JSON 损坏、重复键、版本不符、数字越界、同一数字对应多个字符串、区间耗尽或 I/O 失败时 Fatal，不安装任何自定义 MKind。

MagicSelector 使用 ushort 保存 kind。字符串 ID 改名会获得新数字；旧映射继续保留，避免已有存档被另一个魔法接管。

## 7. 注册时序

`PolarisMagicComponent` 固定执行：

- Awake：验证全部补丁目标签名，再创建 Harmony `Polaris.Magic` 并安装补丁；不匹配则 Fatal。
- Start：扫描带 `MagicDefinitionProviderAttribute` 的提供器，构建并校验全部定义，分配数字 ID，封存注册表，登记名称解析，最后注入 MKind 和 holder。
- Update：不驱动魔法；正式实例由原版 MGContainer 更新。
- Shutdown：取消全部实例，清空容器表，解除补丁。

定义扫描按程序集全名、提供器类型全名排序。提供器必须是实现 `IMagicDefinitionProvider` 的非抽象类并有无参数构造函数。构造失败、Build 返回 null、定义非法或字符串 Id 重复均 Fatal，整批注册不产生部分结果。

写入原版前显式调用 `MKind.reloadKindDataScript`，确认原表存在 WHITEARROW 且 30000–49999 未被占用，并预检已登记容器的 OHoldFD。全部定义、数字映射和冲突预检成功后才按字符串 Id 顺序写入。

## 8. 原版接入补丁

| 目标 | 补丁 | 固定行为 |
| --- | --- | --- |
| MKind.reloadKindDataScript | Postfix | 把全部定义写入原表 |
| MKind.refineAllLanguageCache | Postfix | 恢复自定义标题回退缓存 |
| MDAT.initMagicItem | Prefix | 自定义 ID 完整接管，其他 kind 保持原路径 |
| MGContainer 构造函数 | Postfix | 保存容器并安装各自定义 kind 的 holder |
| MGContainer.initS | Prefix | 原版清池前取消该容器全部实例 |
| MGContainer.destruct | Prefix | 取消并移除该容器全部实例 |
| FEnum&lt;MGKIND&gt;.TryParse | Prefix | 按 Ordinal/OrdinalIgnoreCase 解析字符串 ID |
| MagicSelector.readBinaryFrom | Prefix | 断言注册表已封存且 MKind 已注入 |
| MagicSelector.writeBinaryTo | Prefix | 获得表超过 255 项时 Fatal 并中止写档 |

`CustomMKindInstaller` 显式映射九个基本参数，并把原版 `def_aim` 固定为 `NORMAL`。未定义字段固定为 flip=false、knockback_len=0、icon_scale=1、icon_index=0、snd_load=null；小图标回退 WHITEARROW，标题回退字符串 ID。

`CustomMagicInitializer` 复现原版自定义入口所需的重置、CHANTED、准备态、`MKind.initMagicS`、IMMEDIATE holder 安装和谜题 MP 规则。框架不自动创建 Atk0、Ray 或 Notifier，作者在阶段回调中组织表现与命中。

## 9. Holder 与生命周期

每个 MGContainer、每个自定义 kind 创建一个 `PolarisMagicHolder`，不跨容器共享。组件以对象引用为键保存容器及 holder 表；容器初始化、销毁和组件 Shutdown 都复制活动实例列表后逐个 Dispose。

holder.initFunc 从定义工厂创建 behavior，创建 `MagicRuntimeInstance` 并写入 `MagicItem.Other`，设置 `input_null_to_other_when_quit = true`，再安装 run/draw 委托。发现同一 MagicItem.id 尚有旧实例时，先以 `ExternalKill` 结束旧实例。

holder.run 调用 `instance.Tick(fcnt)`；未结束返回 true，完成或错误返回 false。holder.draw 固定返回 true，框架不绘制表现。

Tick 固定顺序：

1. `TickIndex` 加一并写入 `DeltaFrames`。
2. 若已请求结束，执行清理并返回 false。
3. 调用一次当前阶段回调。
4. 再次检查结束请求，然后解释 Stay、Transition 或 Complete。

不存在图执行预算、即时节点连跑、数据求值或变量槽位。

## 10. 运行时上下文

`MagicRuntimeContext` 公开：

```csharp
public long InstanceId { get; }
public MagicDefinition Definition { get; }
public MagicEntity Self { get; }
public GameCharacter Caster { get; }
public GamePlayer Player { get; }
public GameMap Map { get; }
public long TickIndex { get; }
public float DeltaFrames { get; }
public bool IsCancellationRequested { get; }
public void End();
```

`MagicEntity` 包装当前 MagicItem，不公开原版类型。它固定公开 Position、FollowCasterCenter、ElapsedFrames、Phase 和 ManaBudget。基础读写规则沿用原版字段语义，ManaBudget 写入负值时钳制为 0。

Context 通过 internal `AttachContext` 恰好绑定一次；绑定前访问抛 `InvalidOperationException`，作者构造函数不得使用它。`End()` 只设置请求标记，不在作者调用栈内销毁对象。

资源、文案、粒子、物品、攻击和其他世界能力不进入定义类型；code-behind 从 Context 取得实例包装器，并调用对应 Polaris 公共 API。

PolarisMagic 不定义 Aim 子系统、瞄准枚举、操作句柄或上下文快捷方法。方向、目标和输入读取由 code-behind 按需调用通用游戏 API；PolarisMagic 不替作者维护这类操作的状态。

## 11. 错误与诊断

作者 XML、重复字符串 ID、数字映射损坏和补丁签名不匹配属于启动期致命错误，调用 `PolarisAPI.Errors.Fatal` 并禁止注册自定义魔法。

阶段异常、空首阶段和非法转移属于实例错误，调用 `PolarisAPI.Errors.Report`，只结束当前实例。

`MagicEndReason` 固定包含 Completed、ExplicitEnd、ExternalKill、CallbackException、InvalidStageTransition 和 InternalError。重复结束与重复 Dispose 直接返回。

诊断固定包含字符串 ID、数字 ID、提供器程序集、运行实例 ID 和回调方法显示名；不存在的字段写 null。不再包含图节点 ID、端口或回调索引。

## 12. 测试与验收

新增 PolarisMagic.Tests，目标 net8.0，使用 xUnit。Authoring、注册表、数字映射和阶段 runtime 通过可替换原版适配接口测试；Harmony 补丁用反射签名测试。

验收必须覆盖：

- 只有 `.pmagic`、`.pmagic.g.cs`、`.pmagic.cs` 的最小魔法可以注册和结束。
- 生成产物不引用 `.pmstate`、Graph、Node、Port、Connection 或 VariableRef 类型。
- 首阶段只创建一次；每 Tick 只调用一个阶段；Transition 的目标从下一 Tick 开始。
- Stay、Transition、Complete、Context.End、空阶段、非法转移和异常语义正确。
- 同一魔法两个并发实例的字段和当前阶段完全隔离。
- 阶段切换、完成、异常、外部击杀和清图都只清理一次。
- 公开 API 与生成产物中不存在 `MagicAim*`、`BeginAim` 或专用瞄准枚举。
- 数字映射在安装顺序变化、移除模组和重新加入后保持稳定。
- MKind、MDAT、MGContainer、FEnum&lt;MGKIND&gt; 与 MagicSelector 的目标签名匹配当前游戏程序集。

仓库级搜索必须确认除迁移说明外不存在 `.pmstate`、`MagicGraph*`、`MagicNode*`、`StateFlow`、`InterruptFlow`、`MagicVariableRef` 和 `SetGraph` 的实现或生成契约。
