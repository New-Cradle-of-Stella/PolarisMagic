# PolarisMagic 原型框架草案

## 1. 三文件模型

一项魔法由三个同名文件组成：

```text
ExampleMagic.pmagic
ExampleMagic.pmagic.g.cs
ExampleMagic.pmagic.cs
```

| 文件 | 职责 | 修改规则 |
| --- | --- | --- |
| `.pmagic` | 魔法的静态定义与其他文件的归属根 | 用户通过 PolarisTools 编辑 |
| `.pmagic.g.cs` | 生成静态参数、behavior 工厂与注册代码 | PolarisTools 可整体覆盖，用户不修改 |
| `.pmagic.cs` | 实现运行时字段、阶段回调和世界交互 | 用户维护，PolarisTools 不覆盖已有内容 |

不再存在 `.pmstate`。PolarisMagic 不保存或执行状态图，也不提供节点、端口、连线、图变量、条件中断节点或图编辑器。分支、循环、中断条件和阶段间共享数据全部使用普通 C# 表达。

`.pmagic.g.cs` 与 `.pmagic.cs` 组成同一个 partial class。生成部分声明该类继承 `MagicBehavior`；code-behind 只实现首阶段选择、阶段方法和可选的清理回调。PolarisTools 只在 `.pmagic.cs` 不存在时创建最小桩，不根据阶段方法的增删改写用户代码。

上述格式、生成契约和运行时均属于独立的 `PolarisMagic` 组件项目。Polaris 聚合仓库只负责装载组件，PolarisTools 负责编辑静态定义与生成；魔法定义不作为 PolarisCore 内部功能维护。

## 2. `.pmagic` 静态属性

`.pmagic` 使用 XML 保存，只描述与原版 `MKind` 对应的静态参数。攻击、表现、阶段和单次施法状态均由 `.pmagic.cs` 通过 PolarisMagic 运行时 API 实现。

### 2.1 必需属性

| 属性 | 类型 | 约束与含义 |
| --- | --- | --- |
| `Id` | `String` | 全局唯一，必须匹配 `^[a-z][a-z0-9_.-]{2,127}$` |
| `MpCost` | `Int` | 大于等于 0；基础 MP 消耗 |
| `CastTime` | `Int` | 大于等于 0；基础咏唱时间 |
| `MpCrystalizeRatio` | `Float` | 0 到 1；基础 MP 结晶比例 |
| `NeutralCrystalizeRatio` | `Float` | 0 到 1；返还魔力中的中立比例 |

`MpCost` 和 `CastTime` 即使为 0 也必须显式声明。必需属性缺失或越界时，PolarisTools 报告构建错误。

作者不填写原版 `MGKIND` 的数字 ID。PolarisMagic 在注册时为字符串 `Id` 分配 30000–49999 范围内的数字 ID，并持久化到 `BepInEx/config/Polaris/magic-id-map.json`。已有字符串始终复用原数字；删除定义后历史条目仍占用该数字。code-behind 和模组间引用只使用字符串 `Id`。

### 2.2 可选属性

| 属性 | 类型 | 默认值 | 含义 |
| --- | --- | ---: | --- |
| `PrepareTime` | `Float` | 14 | 咏唱完成到正式释放的准备时间 |
| `ManaDrainLock` | `Float` | 5 | 施法后的基础 Mana drain lock |
| `ProjectilePower` | `Int` | 100 | 与其他投射物交互时的基础强度 |
| `ShotgunRatio` | `Float` | 1.5 | 转换为原版霰弹/近战时的倍率 |
| `SuperArmorTiredTime` | `Float` | 0 | 写入基础攻击数据的超级护甲疲劳时间 |

生成定义时必须显式写入可选属性的最终值，不依赖 CLR 默认值。

### 2.3 自定义静态属性

魔法可以在 `.pmagic` 中声明自己的静态属性。PolarisTools 负责读取、校验并在 `.pmagic.g.cs` 中生成只读投影。

自定义静态属性不能保存单次施法的计时、计数、位置或目标；这些数据直接保存为 code-behind 实例字段。每次正式施法创建一个 behavior 实例，因此字段天然互相隔离。

### 2.4 不属于静态定义的内容

- 图标、声音等资源由 PolarisRes 动态提供。
- 标题和描述等文案由 PolarisLang 动态提供。
- `knockback_len`、HP/MP 伤害、属性、异常状态、攻击次数和命中锁由 code-behind 创建攻击时设置。
- Ray、Notifier、移动轨迹和持续场参数由 code-behind 通过运行时 API 创建和维护。
- 魔法获得状态、grade 和当前选择位置属于玩家存档实例数据。
- 法杖、角色状态、食物、谜题和全局倍率修正由运行时计算。

## 3. 回调即阶段

阶段不是数据节点或可注册对象，而是 `MagicBehavior` 实例上的一个 C# 回调。运行时只保存当前回调，不理解阶段名称，也不分析阶段之间的拓扑。

核心契约为：

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

调用规则固定如下：

- behavior 与 `Context` 绑定后，运行时恰好调用一次 `CreateInitialStage`；返回 `null` 是运行错误。
- 每个 Tick 只调用一次当前阶段回调。阶段切换后，新回调从下一个 Tick 开始执行，不在同帧递归调用。
- `Stay` 保留当前回调。
- `TransitionTo(next)` 要求 `next` 非空；运行时保存新回调，由下一 Tick 开始执行。
- `Complete` 以 `Completed` 正常结束当前实例。
- 阶段回调抛异常时，只以 `CallbackException` 结束当前实例并报告，不影响其他实例。
- `Context.End()` 可从阶段内请求 `ExplicitEnd`；它优先于本次返回值。
- 外部击杀、清图、组件关闭和正常完成最终都进入同一个幂等清理入口，`OnDispose` 对每个实例最多调用一次。

阶段方法可以是实例方法、lambda 或返回兼容委托的方法，但推荐使用命名实例方法，便于日志定位：

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
        // 移动、命中和表现由普通 C# 组合。
        return age < 90f
            ? MagicStageResult.Stay
            : MagicStageResult.Complete;
    }
}
```

条件分支直接使用 `if` / `switch`，循环状态直接使用字段，中断直接在阶段回调开头检查并返回 `TransitionTo` 或 `Complete`。框架不提供第二套表达方式，也不从 code-behind 反向生成流程图。

## 4. 运行时上下文

PolarisMagic 为 code-behind 提供统一的 `MagicRuntimeContext`。它至少公开定义、当前魔法实体、施法者、玩家、地图、`TickIndex`、`DeltaFrames`、取消状态和显式结束入口。底层是否转接 PolarisGameAPI、原版对象或其他组件属于实现细节。

`Context` 在 behavior 绑定前不可访问；作者构造函数不得读取它。资源、文案、粒子、攻击、物品和其他世界能力都由阶段回调从 `Context` 或 Polaris 公共组件 API 调用，不进入 `.pmagic` 格式。

code-behind 字段可以直接保存基础值和运行时句柄。句柄不进入 `.pmagic` 或玩家存档，并在使用时检查有效性。实例结束后，`Context.IsCancellationRequested` 为 `true`，随后调用 `OnDispose`。

PolarisMagic 不提供专用 Aim 类型、操作句柄或 `BeginAim` 入口。方向、目标与输入读取如有需要，由 code-behind 直接调用通用游戏运行时 API，生命周期也由作者代码显式管理。

## 5. 生成与校验边界

PolarisTools 只校验 `.pmagic` 的 XML 结构、静态属性、生成类名和生成代码。它不扫描阶段方法、不要求阶段 ID，也不验证 C# 控制流最终能否结束。

编译器负责检查 `CreateInitialStage` 是否实现以及阶段方法是否符合委托签名；运行时负责检查空首阶段、空转移目标和回调异常。由于每 Tick 只调用一个回调，不再需要图环校验、数据环校验或单帧节点步数预算。
