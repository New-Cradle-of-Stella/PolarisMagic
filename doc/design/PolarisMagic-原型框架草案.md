# PolarisMagic 原型框架草案

## 1. 四文件模型

一项魔法由四个同名文件组成：

```text
ExampleMagic.pmagic
ExampleMagic.pmstate
ExampleMagic.pmagic.g.cs
ExampleMagic.pmagic.cs
```

| 文件 | 职责 | 修改规则 |
| --- | --- | --- |
| `.pmagic` | 唯一魔法定义文件和其他文件的归属根 | 用户通过 PolarisTools 编辑 |
| `.pmstate` | 必需的状态机图，保存节点、连线和回调绑定 | 用户通过 PolarisTools 编辑 |
| `.pmagic.g.cs` | 聚合前两者，生成定义、状态机、回调接线和注册代码 | PolarisTools 可整体覆盖，用户不修改 |
| `.pmagic.cs` | 实现状态、转换、条件和中断回调的 code-behind | 用户维护，PolarisTools 不整体覆盖 |

一个 `.pmagic` 必须拥有一个 `.pmstate`；当前原型中，一个 `.pmstate` 只属于一个 `.pmagic`，不能独立注册。

`.pmagic.g.cs` 与 `.pmagic.cs` 组成同一个 partial class。PolarisTools 只在 `.pmagic.cs` 不存在时创建它；保存状态图时可以追加缺失的回调桩，但不得修改已有方法，也不得因节点删除而自动删除用户代码。

## 2. `.pmagic` 基本属性

`.pmagic` 使用 XML 保存。本节只定义与原版 `MKind` 对应的基本属性；攻击、预瞄和状态机行为另行定义。

### 2.1 必需属性

| 属性 | 类型 | 约束与含义 |
| --- | --- | --- |
| `Id` | `String` | 非空且全局唯一的稳定身份，建议包含模组命名空间；不是显示名称 |
| `MpCost` | `Int` | 大于等于 0；基础 MP 消耗，对应原版 `reduce_mp` |
| `CastTime` | `Int` | 大于等于 0；基础咏唱时间，对应原版 `casttime` |
| `MpCrystalizeRatio` | `Float` | 0 到 1；基础 MP 结晶比例 |
| `NeutralCrystalizeRatio` | `Float` | 0 到 1；返还魔力中的中立比例 |

`MpCost` 和 `CastTime` 即使为 0 也必须显式声明。所有必需属性缺失时，PolarisTools 均报告构建错误。

作者不填写原版 `MGKIND` 的数字 ID。PolarisMagic 在注册时为字符串 `Id` 分配运行时数字 ID，并维护映射。code-behind、状态图和模组间引用只使用字符串 `Id`。需要进入原版存档时，数字映射的恢复方式另行定义。

### 2.2 可选属性

| 属性 | 类型 | 默认值 | 含义 |
| --- | --- | ---: | --- |
| `PrepareTime` | `Float` | 14 | 咏唱完成到正式释放的准备时间 |
| `ManaDrainLock` | `Float` | 5 | 施法后的基础 Mana drain lock |
| `ProjectilePower` | `Int` | 100 | 与其他投射物交互时的基础强度 |
| `ShotgunRatio` | `Float` | 1.5 | 转换为原版霰弹/近战时的倍率 |
| `SuperArmorTiredTime` | `Float` | 0 | 写入基础攻击数据的超级护甲疲劳时间 |
| `DefaultAim` | 枚举 | `NORMAL` | 玩家首次获得魔法时建议使用的选择方向 |

PolarisMagic 生成定义时显式写入可选属性的最终值，不依赖游戏字段的 CLR 默认值。

### 2.3 自定义静态属性

魔法可以在 `.pmagic` XML 中直接声明自己的静态属性。PolarisTools 负责读取、校验并在 `.pmagic.g.cs` 中生成只读投影。

自定义静态属性不能保存单次施法的计时、计数、位置、目标等可变数据；这些数据属于运行时实例。

### 2.4 不属于基本属性的内容

- 图标、声音等资源由现有 PolarisRes 动态提供。
- 标题和描述等文案由现有 PolarisLang 动态提供。
- 原版 `flip` 只用于选择器图标翻转，随表现层处理。
- `knockback_len` 归入后续攻击定义；当前原版 `_magic_kind` 解析器也不会读取它。
- HP/MP 伤害、属性、异常状态、攻击次数和命中锁属于攻击定义。
- Ray、Notifier、移动轨迹和持续场参数属于状态机行为或后续专门定义。
- 魔法获得状态、grade 和当前选择位置属于玩家存档实例数据。
- 法杖、角色状态、食物、谜题和全局倍率修正由运行时计算。

## 3. `.pmstate` 最小模型

每张 `.pmstate` 固定且仅有三个不可删除、复制或改变身份的系统节点：

| 节点 | 作用 |
| --- | --- |
| `Create` | 唯一入口；施法完成并创建正式运行实例后从这里开始 |
| `AnyState` | 全局中断源；不表示实际执行状态，只负责把条件中断挂接到任意当前状态 |
| `End` | 唯一合法终点；进入后结束本次魔法 |

最小合法状态机为：

```text
AnyState   （未连接）
Create → End
```

`AnyState` 可以不连接，因此最小模型仍表示施法完成后立即结束，不产生任何自定义效果。

以下规则属于构建期硬约束，违反时与缺少 `.pmagic` 必需属性一样报错，并禁止生成可注册定义：

- `Create` 必须有状态流输出。
- 从 `Create` 可达的每条状态流都必须最终以 `End` 结尾。
- 不允许存在从 `Create` 可达但无法继续到达 `End` 的分支或无出口普通节点。
- 允许状态流形成环，但从 `Create` 可达的每个环都必须存在一条离开该环并最终到达 `End` 的逃离路径；没有逃离路径的环属于非法死环。
- `AnyState` 引出的每条中断路径必须直接到达 `End`，或接入一条从 `Create` 可达且已经验证能够到达 `End` 的现有状态流；接入后仍遵守相同的分支、成环与逃离路径规则。
- `End` 不允许有状态流输出。

## 4. 端口与连线

每个端口同时具有方向和类型。连线只允许：

```text
相同类型的 Output → Input
```

`Input → Output`、同方向连接、不同类型连接和隐式类型转换均为非法。PolarisTools 在编辑时阻止，并在构建时复检。

普通数据输入端口最多接受一条连线。`StateFlow` 输入端口允许多条状态流汇入，以支持中断流程接回现有流程；只有实际连接到同一输入端口才算合流，画布上的连线交叉不产生任何语义。

### 4.1 当前端口类型

| 类型 | 作用 |
| --- | --- |
| `StateFlow` | 表示节点执行顺序 |
| `InterruptFlow` | `AnyState` 专用的中断连接；只能接入 `ConditionalInterrupt` |
| `Float` | 浮点数 |
| `Int` | 整数 |
| `Bool` | 布尔值 |
| `String` | 字符串 |
| `Vector2` | 二维数值结构，对应 `GameVector2` |
| `Target` | PolarisMagic 的可空角色句柄，封装 `GameCharacter` |
| `Map` | PolarisMagic 的可空地图句柄，封装 `GameMap` |
| `AudioPlayback` | PolarisMagic 的可空音频播放句柄，封装 `GameAudioPlayback` |
| `Item` | PolarisMagic 的可空物品定义句柄，封装 `GameItem` |
| `Storage` | PolarisMagic 的可空物品栏句柄，封装 `GameStorage` |
| `Drop` | 可空的物品掉落结果，封装 `GameDrop` |
| `VariableRef` | 变量引用；引用自身携带所指变量的值类型 |

`StateFlow` 和 `InterruptFlow` 均不能与数据端口连接，也不能互相连接。未使用的数据输出可以不连接，不影响状态流合法性。

`VariableRef<T>` 表示端口仍属于 `VariableRef` 类型，但引用携带的实际值类型必须是 `T`。类型参数只用于编辑器和构建校验，不产生隐式转换。

系统节点端口为：

| 节点 | 端口 | 方向 | 类型 |
| --- | --- | --- | --- |
| `Create` | `Flow` | `Output` | `StateFlow` |
| `AnyState` | `Interrupts` | `Output` | `InterruptFlow` |
| `End` | `Flow` | `Input` | `StateFlow` |

`AnyState.Interrupts` 允许连接多个 `ConditionalInterrupt.AnyState` 输入，除此之外不能连接任何节点或端口。

### 4.2 Game API 结构封装

`.pmstate` 不直接暴露或序列化 PolarisGameAPI 的实例对象。PolarisMagic 为当前需要进入状态图的返回结构定义受控类型：

| 图类型 | 封装来源 | 语义 |
| --- | --- | --- |
| `Vector2` | `GameVector2` | 不可变值；包含 `X`、`Y`，默认值为 `(0, 0)` |
| `Target` | `GameCharacter`、`GamePlayer`、`GameEnemy` | 场上角色句柄；保留玩家/敌人具体种类，可为 `null`，切图或对象回收后可能失效 |
| `Map` | `GameMap` | 地图句柄；地图关闭后失效 |
| `AudioPlayback` | `GameAudioPlayback` | 单次播放句柄；播放结束或被停止后失效 |
| `Item` | `GameItem` | 物品定义句柄；按稳定键解析，一局游戏内持续有效 |
| `Storage` | `GameStorage` | 主物品栏、贵重品栏或住宅仓库的句柄；玩家或存档切换后可能失效 |
| `Drop` | `GameDrop` | 一次掉落结果；包含 `Item`、数量、品级和 `Vector2` 位置，本身不是可操作的场上实例 |

句柄只在当前游戏进程与魔法运行期间有效，不进入 `.pmagic`、`.pmstate` 或玩家存档。句柄内部持有 Game API 包装器而非原版原生对象；每次使用前由 PolarisMagic 检查 `IsValid`。`null` 或失效句柄进入写节点时按无效果处理，进入读取节点时产生默认输出。

这些结构可以作为 `VariableRef<T>` 的实际值类型。`Dereference`、`Assign` 和 `Equals` 负责读取、转存和比较；句柄的 `Equals` 按所封装实例身份比较，`Vector2` 按两个分量比较。其他 PolarisGameAPI 返回结构只有在对应能力正式加入节点目录时才新增图类型。

## 5. 预制节点目录

预制节点由 PolarisMagic 实现，作者只在 `.pmstate` 中使用和配置。

| 分类 | 节点 |
| --- | --- |
| `游戏交互 / 瞄准与目标` | `Aim`、`GetPlayer`、`FindCharacter`、`FindEnemy`、`ReadTarget` |
| `游戏交互 / 角色移动` | `Teleport`、`MoveBy`、`SetVelocity`、`SetFacing` |
| `游戏交互 / 生命与魔力` | `HealHp`、`HealMp`、`DamageHp`、`DamageMp` |
| `游戏交互 / 敌人` | `ApplyEnemyDamage`、`AddKnockback` |
| `游戏交互 / 输入` | `ReadInput`、`ReadInputDirection`、`ReadMousePosition` |
| `游戏交互 / 世界查询` | `ReadWorldState`、`HasWeather`、`ReadMapState`、`IsTargetInCamera` |
| `游戏交互 / 音频` | `PlaySound`、`StopSound`、`PauseSound`、`IsSoundPlaying`、`SetSoundAisac` |
| `游戏交互 / 物品` | `ResolveItem`、`ReadItem` |
| `游戏交互 / 物品栏` | `GetStorage`、`CountItem`、`CanAddItem`、`AddItem`、`ReduceItem`、`UseItem`、`DropItem` |
| `游戏交互 / 自定义` | `CSharpCallback` |
| `变量 / 基础` | `Variable`、`Dereference`、`Assign` |
| `变量 / 数值运算` | `Add`、`Subtract`、`Multiply`、`Divide`、`Remainder` |
| `变量 / 比较运算符` | `LessThan`、`GreaterThan`、`LessThanOrEqual`、`GreaterThanOrEqual`、`Equals` |
| `变量 / 逻辑运算符` | `And`、`Or`、`Xor`、`Not` |
| `数值运算 / Int` | `Add`、`Subtract`、`Multiply`、`Divide`、`Remainder` |
| `数值运算 / Float` | `Add`、`Subtract`、`Multiply`、`Divide`、`Remainder` |
| `比较运算符 / Int` | `LessThan`、`GreaterThan`、`LessThanOrEqual`、`GreaterThanOrEqual` |
| `比较运算符 / Float` | `LessThan`、`GreaterThan`、`LessThanOrEqual`、`GreaterThanOrEqual` |
| `比较运算符 / 任意` | `Equals` |
| `逻辑运算符 / Bool` | `And`、`Or`、`Xor`、`Not` |
| `数据结构 / Vector2` | `MakeVector2`、`SplitVector2` |
| `数据结构 / 句柄` | `IsHandleValid` |
| `数据结构 / Drop` | `SplitDrop` |
| `流程控制 / 分支与跳转` | `If`、`Select`、`Label`、`Jump` |
| `流程控制 / 中断` | `ConditionalInterrupt` |

分类只用于 PolarisTools 的节点目录。

### 5.1 游戏交互

本节新增的游戏交互节点以现有 `PolarisAPI.Game` 为实现边界。除 `CSharpCallback` 外，本节节点均具有固定的 `In: StateFlow` 与 `Out: StateFlow`，在状态流经过时执行一次；下表只列数据端口和节点配置。

读取节点在执行时生成一次快照。写入节点收到 `null`、已失效的 `Target` 或不符合要求的目标种类时不产生效果，并以各输出类型的默认值继续状态流，不把 `InvalidGameInstanceException` 传播到状态机。

所有游戏交互节点的数据输出均为类型化变量引用 `VariableRef<T>`，不直接输出基础值或结构值。PolarisMagic 在魔法运行实例创建时为每个输出端口创建一个节点私有变量并写入该类型的默认值；节点执行时更新变量，输出端口始终暴露同一个稳定引用。普通数据节点若要使用其中的值，必须先经过 `Dereference`；变量节点可以直接接收该引用。

#### 瞄准与目标

##### `Aim`

`Aim` 执行一次瞄准：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 进入节点 |
| `Out` | `Output` | `StateFlow` | 瞄准完成后继续 |
| `Direction` | `Output` | `VariableRef<Float>` | 本次选定方向的变量引用 |
| `Target` | `Output` | `VariableRef<Target>` | 本次选中目标的变量引用，其值允许为 `null` |

`Target` 引用中的值不为 `null` 表示选中了实际目标；值为 `null` 表示没有目标被选中，玩家随机选择了一个方向，该方向仍写入 `Direction` 引用。

`Direction` 的数值规范、目标搜索方式和配置参数暂不定义。

其余目标节点为：

| 节点 | 数据输入 | 数据输出 | 含义 |
| --- | --- | --- | --- |
| `GetPlayer` | 无 | `Target: VariableRef<Target>` | 取得当前玩家；玩家不在场时写入 `null` |
| `FindCharacter` | `Key: String` | `Target: VariableRef<Target>` | 按当前地图中的移动对象名称查找任意角色 |
| `FindEnemy` | `Key: String` | `Target: VariableRef<Target>` | 按名称查找敌人；对象不是敌人时写入 `null` |
| `ReadTarget` | `Target: Target` | `Position/Velocity/Size: VariableRef<Vector2>`、`Hp/MaxHp: VariableRef<Int>`、`Mp/MaxMp: VariableRef<Int>`、`Alive: VariableRef<Bool>` | 一次读取目标的角色公共状态 |

#### 角色移动

| 节点 | 数据输入 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `Teleport` | `Target: Target`、`Position: Vector2` | 无 | `GameCharacter.Teleport`；硬设位置，不检查碰撞 |
| `MoveBy` | `Target: Target`、`Delta: Vector2`、`CheckFoot: Bool` | `Moved: VariableRef<Bool>` | `GameCharacter.MoveBy`；可选择是否走带碰撞位移 |
| `SetVelocity` | `Target: Target`、`Velocity: Vector2` | 无 | `GameCharacter.SetVelocity` |
| `SetFacing` | `Target: Target`、`Right: Bool`、`ForceSprite: Bool` | 无 | `GameCharacter.SetFacing` |

#### 生命与魔力

| 节点 | 数据输入 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `HealHp` | `Target: Target`、`Amount: Int` | 无 | `GameCharacter.HealHp` |
| `HealMp` | `Target: Target`、`Amount: Int` | 无 | `GameCharacter.HealMp` |
| `DamageHp` | `Target: Target`、`Amount: Int`、`Force: Bool` | `ActualDamage: VariableRef<Int>` | `GameCharacter.DamageHp` |
| `DamageMp` | `Target: Target`、`Amount: Int`、`Force: Bool` | `ActualDamage: VariableRef<Int>` | `GameCharacter.DamageMp` |

`Amount` 必须大于 0；否则节点不产生效果。`Force` 默认为 `false`，普通魔法不应默认绕过游戏的抗性、护盾或无敌判定。

#### 敌人

| 节点 | 数据输入 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `ApplyEnemyDamage` | `Target: Target`、`HpDamage/MpDamage: Int`、`Force: Bool` | `ActualHpDamage: VariableRef<Int>` | `GameEnemy.ApplyDamage`；目标必须是敌人 |
| `AddKnockback` | `Target: Target`、`Velocity: Float`、`FromRight: Bool` | 无 | `GameEnemy.AddKnockback`；沿游戏自身的抗击退通道处理 |

目标不是 `GameEnemy` 时，这两个节点不生效。`Velocity` 必须大于等于 0；`FromRight` 表示攻击来自目标右侧，因此目标向左击退。

#### 输入

| 节点 | 数据输入或配置 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `ReadInput` | 配置 `Action: GameInputAction`；输入 `BufferFrames: Int`、`HeldFrames: Int` | `Held/Pressed/Released: VariableRef<Bool>` | 同一次更新中读取 `IsHeld`、`WasPressed`、`WasReleased` |
| `ReadInputDirection` | 无 | `Direction: VariableRef<Vector2>` | 读取合成方向，两个分量均为 -1、0 或 1 |
| `ReadMousePosition` | 无 | `Position: VariableRef<Vector2>` | 读取游戏 GUI 坐标系中的鼠标屏幕坐标 |

`Action` 使用 PolarisTools 下拉框配置，不以裸整数进入端口。`BufferFrames` 默认 1，`HeldFrames` 默认 0。

#### 世界查询

| 节点 | 数据输入或配置 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `ReadWorldState` | 无 | `Night: VariableRef<Bool>`、`DangerLevel: VariableRef<Float>`、`DangerMeter: VariableRef<Int>` | 读取当前日夜与危险度快照 |
| `HasWeather` | 配置 `Weather: GameWeather` | `Active: VariableRef<Bool>` | 判断指定天气当前是否生效 |
| `ReadMapState` | 无 | `Map: VariableRef<Map>`、`Key: VariableRef<String>`、`Time: VariableRef<Float>`、`Dark: VariableRef<Bool>`、`MousePosition: VariableRef<Vector2>` | 读取当前地图及鼠标地图坐标；没有地图时写入默认值 |
| `IsTargetInCamera` | `Target: Target`、`MarginPixels: Float` | `Visible: VariableRef<Bool>` | 使用目标位置与碰撞尺寸判断其是否在相机范围内 |

`Weather` 使用 PolarisTools 下拉框配置。世界查询节点只读取状态，不修改天气、危险度或地图。

#### 音频

| 节点 | 数据输入 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `PlaySound` | `Cue: String`、`Loop: Bool` | `Playback: VariableRef<AudioPlayback>` | 调用 `PolarisAPI.Game.Audio.Play`；失败时写入 `null` |
| `StopSound` | `Playback: AudioPlayback` | 无 | 停止该播放实例 |
| `PauseSound` | `Playback: AudioPlayback`、`Paused: Bool` | 无 | 暂停或恢复该播放实例 |
| `IsSoundPlaying` | `Playback: AudioPlayback` | `Playing: VariableRef<Bool>` | 判断该播放实例是否仍在播放 |
| `SetSoundAisac` | `Playback: AudioPlayback`、`Control: String`、`Value: Float` | 无 | 设置该播放实例的 AISAC 控制值 |

音频节点不保存资源引用，`Cue` 在运行时提供，资源仍由 PolarisRes 管理。`AudioPlayback` 失效后的停止、暂停和查询均按安全空操作或 `false` 处理。

#### 物品

| 节点 | 数据输入 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `ResolveItem` | `Key: String` | `Item: VariableRef<Item>` | 调用 `PolarisAPI.Game.Items.Resolve`；查无物品时写入 `null` |
| `ReadItem` | `Item: Item` | `Key: VariableRef<String>`、`Id/Price/StackLimit: VariableRef<Int>`、`Value: VariableRef<Float>`、`CategoryBits: VariableRef<Int>`、`Usable/Precious/Food/Tool/Bomb: VariableRef<Bool>` | 读取 `GameItem` 已公开的物品定义属性 |

`CategoryBits` 保留 `GameItemCategory` 的位标志数值，不把它误当成单选分类。节点不输出本地化名称；自定义文案继续由 PolarisLang 负责。

#### 物品栏

| 节点 | 数据输入或配置 | 数据输出 | 对应能力 |
| --- | --- | --- | --- |
| `GetStorage` | 配置 `Kind: Main/Precious/House` | `Storage: VariableRef<Storage>` | 取得主物品栏、贵重品栏或住宅仓库 |
| `CountItem` | `Storage: Storage`、`Item: Item`、`Grade: Int` | `Count: VariableRef<Int>` | `GameStorage.Count` |
| `CanAddItem` | `Storage: Storage`、`Item: Item`、`Count/Grade: Int` | `CanAdd: VariableRef<Bool>` | `GameStorage.CanAdd` |
| `AddItem` | `Storage: Storage`、`Item: Item`、`Count/Grade: Int` | `Added: VariableRef<Int>` | `GameStorage.Add`；输出实际加入数量 |
| `ReduceItem` | `Storage: Storage`、`Item: Item`、`Count/Grade: Int` | `Reduced: VariableRef<Bool>` | `GameStorage.Reduce`；数量不足时完全不扣除 |
| `UseItem` | `Storage: Storage`、`Item: Item`、`Grade: Int` | `ResultCode: VariableRef<Int>` | `GameStorage.Use`；结果码含义由物品决定 |
| `DropItem` | `Storage: Storage`、`Item: Item`、`Count/Grade: Int` | `Drop: VariableRef<Drop>` | `GameStorage.Drop`；失败时写入 `null` |

品级规则沿用 Game API：常规品级为 0–4；`CountItem.Grade` 与 `ReduceItem.Grade` 允许使用 -1 表示不分品级。数量必须大于 0。`AddItem` 可能部分成功，调用方必须使用 `Added` 结算；`ReduceItem` 保持全有或全无。

物品栏写操作在节点执行时立即提交，不随之后的中断或魔法结束自动回滚。`Clear` 会一次清空整个容器，因此不提供预制节点。

#### 自定义：`CSharpCallback`

`CSharpCallback`（C# 回调）把状态流接入 `.pmagic.cs` 中的自定义更新方法。状态流进入节点后，PolarisMagic 在每个魔法更新周期调用一次对应方法，直到方法返回 `true`，随后把输出参数写入对应变量并从 `Out` 继续。

固定端口为：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始执行回调 |
| `Out` | `Output` | `StateFlow` | 回调返回 `true` 后继续 |

节点配置包含：

| 配置 | 规则 |
| --- | --- |
| `Id` | 非空且在当前 `.pmstate` 图中唯一；必须是合法的 C# 标识符片段，并作为生成方法名的稳定组成部分 |
| `Inputs` | 可配置数量、顺序、名称和类型的输入参数列表 |
| `Outputs` | 可配置数量、顺序、名称和类型的输出参数列表 |
| `ReturnType` | 固定为 `Bool`；表示本节点本次更新后是否完成 |

每个输入配置生成一个同名数据输入端口；每个输出配置生成一个节点私有变量，并以 `VariableRef<T>` 端口暴露其引用。参数名称必须在当前节点内唯一，并且能够作为合法的 C# 参数名；输入与输出不能重名。输入参数可选 `Int`、`Float`、`Bool`、`String`、`Vector2`、`Target`、`Map`、`AudioPlayback`、`Item`、`Storage`、`Drop` 和 `VariableRef`；输出值可选择除 `VariableRef` 外的上述数据类型。`StateFlow` 与 `InterruptFlow` 均不能配置成方法参数。

PolarisTools 按端口列表顺序生成方法签名。输入端口生成普通参数；输出配置在 C# 方法中仍生成实际值类型的 `out` 参数，返回后再由节点写入对应变量。固定状态流端口不进入方法签名。方法返回值固定为 `bool`：

```csharp
private bool Callback_<Id>(InputType input, out OutputType output)
```

首次保存包含该节点的状态图时，PolarisTools 在 `.pmagic.cs` 中创建对应方法桩。生成的方法桩会为每个 `out` 参数写入默认值并返回 `true`，使尚未实现的回调默认立即完成。已有方法及方法体不会被自动覆盖；如果修改了节点 `Id`、参数名称、顺序或类型，导致现有方法签名不匹配，PolarisTools 报告错误并提示显式同步。

调用规则如下：

- 状态流首次进入 `In` 时，节点进入运行状态并立即执行一次回调。
- 返回 `false` 表示本次更新尚未完成；状态流停留在该节点，下一个魔法更新周期再次调用同一方法。
- 返回 `true` 表示执行完成；本次调用产生的全部 `out` 参数写入对应的节点私有变量，随后沿 `Out` 继续。
- 返回 `false` 时产生的 `out` 参数会被丢弃，各输出变量保持原值，下一次调用会重新产生。
- `bool` 返回值是节点内部的完成信号，不生成数据输出端口，也不能参与连线。

输入值在每次调用前从输入端口读取。若输入来源在节点停留期间没有改变，多次调用会得到相同值；需要跨更新保存的计时、阶段和其他内部状态由当前魔法运行实例或 code-behind 字段维护。

#### 当前不预制的 Game API

以下能力虽然已经存在于 `PolarisAPI.Game`，但暂不做成魔法节点：

| 能力 | 原因 |
| --- | --- |
| 地图切换、天气与危险度修改 | 改变全局世界状态，不适合作为普通魔法的默认积木 |
| 菜单、事件与任务 | 属于 UI 或剧情流程，生命周期与单次魔法不一致 |
| 货币 | 会修改持久化经济状态，暂不作为普通魔法积木 |
| BGM 与全局音量 | 作用域通常超出单次魔法，不与单次 `AudioPlayback` 句柄绑定 |
| 玩家或敌人的 `ChangeState` | 属于绕过正常迁移条件的高权限操作 |
| Game API 回调注册 | `.pmstate` 已经负责单次魔法的执行顺序，注册句柄的释放周期需要另行设计 |
| 资源加载与本地化 | 分别继续由 PolarisRes 与 PolarisLang 负责 |

这些能力仍可在 `CSharpCallback` 中通过 PolarisGameAPI 使用。当前 PolarisGameAPI 尚未提供粒子、贴图表现、投射物、Ray 或命中盒创建能力，因此本轮不为它们虚构预制节点。

### 5.2 变量

`变量` 是独立的一级分类，其下分为 `基础`、`数值运算`、`比较运算符` 和 `逻辑运算符` 四个二级分类。所有专门读取或修改 `VariableRef` 的变量节点均收纳在此，不再分散到其他运算符一级分类中。

#### `Variable`

`Variable` 创建一个具有固定值类型的变量，并输出该变量的引用。

作者在节点上选择 `Int`、`Float`、`Bool` 或 `String` 类型。节点端口为：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始创建并初始化变量 |
| `Out` | `Output` | `StateFlow` | 变量初始化完成后继续 |
| `InitialValue` | `Input` | 当前选择的值类型 | 可选的初始值输入 |
| `Ref` | `Output` | `VariableRef` | 输出变量引用 |

变量必须具有初始值，来源二选一：

- 在节点上设置与当前类型一致的默认值。
- 连接 `InitialValue`，由输入提供初始值；此时输入优先于默认值。

变量只在状态流进入 `In` 时创建并写入初始值，随后从 `Out` 继续。切换变量类型时，PolarisTools 自动切换 `InitialValue` 的端口类型和默认值编辑器，并断开 `InitialValue` 上已有的连线。`Ref` 仍为 `VariableRef`，但其携带的变量值类型随之改变。

`Variable` 仍只允许作者声明上述四种基础类型。游戏交互节点可以另外持有 `Vector2`、`Target`、`Map`、`AudioPlayback`、`Item`、`Storage` 或 `Drop` 类型的私有输出变量，但这些类型不会加入 `Variable` 的手动声明列表。

#### `Dereference`

`Dereference` 读取变量引用当前指向的值。

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始读取变量 |
| `Out` | `Output` | `StateFlow` | 读取完成后继续 |
| `Ref` | `Input` | `VariableRef` | 要读取的变量引用 |
| `Value` | `Output` | 动态类型 | 输出变量当前值 |

`Value` 的类型由接入 `Ref` 的变量决定，可以是四种基础类型，也可以是游戏交互节点持有的任一结构或句柄类型。

`Dereference` 只在状态流进入 `In` 时读取当前值，随后从 `Out` 继续。当接入的变量类型发生改变，或 `Ref` 改接到另一种类型的变量时，PolarisTools 同步改变 `Value` 端口类型，并断开 `Value` 上已有的全部连线。变量引用连线本身保持为 `VariableRef → VariableRef`。

`VariableRef` 不能直接接入数值运算节点；必须先通过 `Dereference` 取得实际值。

#### `Assign`

`Assign` 将新值写入已有变量，并通过状态流确定赋值时机。

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始赋值 |
| `Out` | `Output` | `StateFlow` | 赋值完成后继续 |
| `Variable` | `Input` | `VariableRef` | 要写入的变量 |
| `Value` | `Input` | 动态类型 | 要写入的新值 |

连接 `VariableRef` 后，`Value` 自动切换为该变量的实际值类型。赋值支持当前已定义的全部数据类型，但只允许写入完全相同的类型，不进行隐式转换。

变量类型发生变化，或 `Variable` 改接到另一种类型的变量时，PolarisTools 同步切换 `Value` 端口并断开其已有连线。若要把另一个变量的值写入当前变量，必须先通过 `Dereference` 读取，再将读取结果连接到 `Value`。

### 5.3 数值运算

普通数值运算在 `数值运算` 一级分类下拆成 `Int` 和 `Float` 两个二级分类；直接修改变量的版本则位于 `变量 / 数值运算`：

```text
数值运算
├─ Int
│  ├─ Add
│  ├─ Subtract
│  ├─ Multiply
│  ├─ Divide
│  └─ Remainder
└─ Float
   ├─ Add
   ├─ Subtract
   ├─ Multiply
   ├─ Divide
   └─ Remainder

变量
└─ 数值运算
   ├─ Add
   ├─ Subtract
   ├─ Multiply
   ├─ Divide
   └─ Remainder
```

`Int` 和 `Float` 各自拥有独立节点，不提供可以在两者之间切换的通用运算块。这两类节点没有 `StateFlow`，端口类型由所属二级分类固定：

| 端口 | 方向 | 类型 |
| --- | --- | --- |
| `A` | `Input` | 所属分类的 `Int` 或 `Float` |
| `B` | `Input` | 与 `A` 相同 |
| `Result` | `Output` | 与 `A` 相同 |

`A`、`B` 均为必需输入。

| 节点 | 结果 |
| --- | --- |
| `Add` | `A + B` |
| `Subtract` | `A - B` |
| `Multiply` | `A × B` |
| `Divide` | `A ÷ B` |
| `Remainder` | `A` 对 `B` 求余 |

`String` 和 `Bool` 不属于数值类型，不能连接到任何数值运算节点。PolarisTools 在编辑时拒绝该连接；如果加载的 `.pmstate` 中存在这种连接，则报告构建错误，因为这不是预期行为。

除数为 0 的规则暂不定义。

#### 变量运算

`变量 / 数值运算` 中的节点直接修改 `VariableRef` 指向的数值变量。它们具有状态流，以确定多次变量修改的执行顺序：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始变量运算 |
| `Out` | `Output` | `StateFlow` | 运算完成后继续 |
| `Variable` | `Input` | `VariableRef` | 要修改的变量 |
| `Value` | `Input` | 动态 `Int` 或 `Float` | 与变量进行运算的值 |
| `Result` | `Output` | 与变量值类型相同 | 修改后的变量值 |

连接 `VariableRef` 后，`Value` 和 `Result` 自动切换为该变量的 `Int` 或 `Float` 类型。类型变化时，这两个动态端口上已有的连线全部断开。

`Add`、`Subtract`、`Multiply`、`Divide`、`Remainder` 分别把变量当前值作为左操作数，把 `Value` 作为右操作数，并将结果写回变量。

除 `Int` 和 `Float` 外，其他类型变量都不能连接到变量数值运算节点。PolarisTools 在编辑时拒绝；如果 `.pmstate` 中存在这种连接，则报告构建错误。

### 5.4 比较运算符

`Int`、`Float` 二级目录中的比较节点没有 `StateFlow`，且不允许混合输入类型：

| 端口 | 方向 | 类型 |
| --- | --- | --- |
| `A` | `Input` | 所属分类的 `Int` 或 `Float` |
| `B` | `Input` | 与 `A` 相同 |
| `Result` | `Output` | `Bool` |

| 节点 | 结果 |
| --- | --- |
| `LessThan` | `A` 小于 `B` |
| `GreaterThan` | `A` 大于 `B` |
| `LessThanOrEqual` | `A` 小于等于 `B` |
| `GreaterThanOrEqual` | `A` 大于等于 `B` |

`A`、`B` 均为必需输入。变量必须先通过 `Dereference` 取得实际值，才能连接到 `Int`、`Float` 比较节点。

#### 变量比较

`变量 / 比较运算符` 中的节点直接读取 `VariableRef` 指向的数值变量，并带有状态流以统一读取时机：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始比较 |
| `Out` | `Output` | `StateFlow` | 比较完成后继续 |
| `Variable` | `Input` | `VariableRef` | 作为左操作数的变量 |
| `Value` | `Input` | 动态 `Int` 或 `Float` | 右操作数 |
| `Result` | `Output` | `Bool` | 比较结果 |

`Value` 随变量值类型切换，切换时断开已有连线。变量比较不修改变量，只读取它在状态流经过时的当前值。

除 `Int` 和 `Float` 外，其他类型变量都不能连接到这些数值比较节点，否则构建报错。

#### 变量 `Equals`

`变量 / 比较运算符` 中的 `Equals` 以一个变量作为比较基准，并通过状态流确定读取时机：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始读取并比较 |
| `Out` | `Output` | `StateFlow` | 比较完成后继续 |
| `Variable` | `Input` | `VariableRef` | 作为左操作数的变量 |
| `Value` | `Input` | 动态操作数 | 右操作数 |
| `Result` | `Output` | `Bool` | 是否相等 |

连接 `Variable` 后，节点以该变量的实际值类型定型。`Value` 可以切换为同类型的普通值端口，或另一个 `VariableRef` 端口；使用另一个变量时，其实际值类型必须与 `Variable` 相同。当前已定义的全部数据类型均可使用该节点。

变量类型、`Value` 模式或比较类型改变时，PolarisTools 断开不兼容连线。不同实际值类型之间不进行隐式转换，非法组合在构建时报告错误。

#### 任意 `Equals`

`比较运算符 / 任意` 中的 `Equals` 只比较非变量数据，不接受 `VariableRef`，也不具有 `StateFlow`：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `A` | `Input` | 动态类型 | 左操作数 |
| `B` | `Input` | 与 `A` 相同 | 右操作数 |
| `Result` | `Output` | `Bool` | 是否相等 |

第一个接入的操作数在当前已定义的值或句柄类型中确定端口类型，另一个端口随之切换为完全相同的类型。两个输入均为必需输入；类型不同或接入 `VariableRef` 时报告错误。

比较类型改变时，PolarisTools 断开 `A`、`B` 上已有的不兼容连线。变量如需参与此节点，必须先通过 `Dereference` 取得普通值。

### 5.5 逻辑运算符

`逻辑运算符 / Bool` 中的节点没有 `StateFlow`，只接受并输出 `Bool`。

`And`、`Or`、`Xor` 具有统一端口：

| 端口 | 方向 | 类型 |
| --- | --- | --- |
| `A` | `Input` | `Bool` |
| `B` | `Input` | `Bool` |
| `Result` | `Output` | `Bool` |

| 节点 | 结果 |
| --- | --- |
| `And` | 逻辑与 |
| `Or` | 逻辑或 |
| `Xor` | 逻辑异或 |

`Not` 只有 `Value: Bool` 输入和 `Result: Bool` 输出，结果为输入值的逻辑非。

所有输入均为必需输入。变量必须先通过 `Dereference` 取得实际值，才能连接到 `Bool` 逻辑节点。

#### 变量逻辑运算

`变量 / 逻辑运算符` 中的节点直接修改 `VariableRef` 指向的 `Bool` 变量，并带有状态流：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `In` | `Input` | `StateFlow` | 开始逻辑运算 |
| `Out` | `Output` | `StateFlow` | 运算完成后继续 |
| `Variable` | `Input` | `VariableRef` | 要修改的 `Bool` 变量 |
| `Value` | `Input` | `Bool` | `And`、`Or`、`Xor` 的右操作数 |
| `Result` | `Output` | `Bool` | 修改后的变量值 |

`And`、`Or`、`Xor` 将变量当前值作为左操作数，把结果写回变量。`Not` 不具有 `Value` 端口，直接将变量当前值取反后写回。

只有 `Bool` 变量可以连接到变量逻辑运算节点；其他实际值类型都会导致构建错误。

### 5.6 数据结构

数据结构节点没有 `StateFlow`，只处理已经取得的值。

| 节点 | 输入 | 输出 | 规则 |
| --- | --- | --- | --- |
| `MakeVector2` | `X/Y: Float` | `Result: Vector2` | 由两个分量创建 `Vector2` |
| `SplitVector2` | `Value: Vector2` | `X/Y: Float` | 拆出两个分量 |
| `IsHandleValid` | `Value: Target/Map/AudioPlayback/Item/Storage` | `Result: Bool` | 判断句柄不为 `null` 且底层 Game API 实例仍然有效 |
| `SplitDrop` | `Value: Drop` | `Item: Item`、`Count/Grade: Int`、`Position: Vector2` | 拆出掉落结果；`null` 时输出默认值 |

`IsHandleValid.Value` 是动态端口，作者在当前已定义的句柄类型中选择一种；切换类型时断开已有连线。它不接受 `VariableRef`，句柄变量必须先经过 `Dereference`。`Drop` 是结果结构而非活实例句柄，不接入该节点。

### 5.7 流程控制

所有流程出口仍必须满足最终到达 `End` 的硬约束。

#### `If`

| 输入 | 输出 | 规则 |
| --- | --- | --- |
| `In: StateFlow`、`Condition: Bool` | `True: StateFlow`、`False: StateFlow` | 根据条件选择一个出口；条件和两个出口都必需 |

#### `Select`

| 输入 | 输出 | 规则 |
| --- | --- | --- |
| `In: StateFlow`、`Value: Int/String` | 多个 `Case: StateFlow`、一个 `Default: StateFlow` | 按值匹配分支；同一节点的 Case 值类型必须与 Value 一致；所有出口都必需 |

#### `Label` 与 `Jump`

- `Label` 有 `In: StateFlow` 和 `Out: StateFlow`，并声明图内唯一的稳定标签 ID。
- `Jump` 只有 `In: StateFlow`，通过配置引用目标标签 ID，不提供普通状态流输出。
- `Jump` 引用不存在的标签时构建失败。

#### `ConditionalInterrupt`

`ConditionalInterrupt`（条件中断）是 `AnyState` 唯一允许连接的节点。它没有普通 `StateFlow` 输入：

| 端口 | 方向 | 类型 | 含义 |
| --- | --- | --- | --- |
| `AnyState` | `Input` | `InterruptFlow` | 只能连接 `AnyState.Interrupts` |
| `Condition` | `Input` | `VariableRef<Bool>` | 持续监听的布尔变量引用 |
| `Out` | `Output` | `StateFlow` | 条件满足后转入的中断流程 |

`Condition` 必须直接连接实际值类型为 `Bool` 的 `VariableRef`。普通 `Bool`、逻辑运算产生的临时布尔值、节点常量和其他类型变量都不能连接；如需使用计算结果，必须先把结果写入一个 `Bool` 变量。

PolarisMagic 在每个魔法更新周期执行当前节点之前检查条件中断。条件值为 `true` 时，立即终止当前节点本轮执行，放弃其尚未提交的输出，并沿 `ConditionalInterrupt.Out` 进入中断流程。一次触发后，只有当条件变量至少被检查到一次 `false`，该中断才会重新启用，避免变量持续为 `true` 时每帧重复触发。

`ConditionalInterrupt.Out` 必须连接。其全部后续路径必须直接到达 `End`，或汇入一个从 `Create` 可达且其后续已经验证能够到达 `End` 的现有节点；悬空、中途终止或汇入非法流程时构建失败。`InterruptFlow` 不能由普通节点产生，也不能接入 `ConditionalInterrupt` 以外的节点。

构建器先验证并标记从 `Create` 可达且保证能够到达 `End` 的主流程节点，再分别遍历每条中断流程。遍历抵达 `End`，或抵达上述已标记的主流程节点，才算合法终点；仅仅汇入另一条尚未验证的中断流程不能绕过终点检查。中断流程中的每个分支都要独立满足该条件。
