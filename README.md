# PolarisMagic

Polaris 的自定义魔法能力组件。依赖同级 `PolarisCore` 与 `PolarisRes`，并由
[Polaris](https://github.com/New-Cradle-of-Stella/Polaris) 聚合仓库作为 Git submodule 引用。

一项魔法只有三个文件：`.pmagic`（静态参数）、`.pmagic.g.cs`（生成的注册代码）、`.pmagic.cs`（作者实现）。
作者写的是**一个 `RunAsync`**：一次施法只调用一次，这个 Task 存活期间魔法就在运行；它不管以什么方式退出
（正常完成、取消、抛异常）都代表魔法立即结束，收尾由 PolarisMagic 负责。没有图、没有阶段、
没有逐 Tick 回调，也没有 `Context.End` 或 `OnDispose`。

新建魔法时 `.pmagic.cs` 里只有一个空白 Task——起手不是一段需要先读懂再删掉的示例：

```csharp
private Task RunAsync(MagicRuntimeContext context, CancellationToken cancellationToken)
{
    return Task.CompletedTask;
}
```

原样构建就是一个"生成即结束"的合法魔法。作者往里写多少东西，魔法就活多久。

## 设计文档

- `doc/design/PolarisMagic-设计规范.md`：静态定义、单 Task code-behind 与中间层边界。
- `doc/design/PolarisMagic-实现方案.md`：运行时实现与兼容方案。

工具侧（VS 扩展）没有单独的规格文档：`.pmagic` 编辑器、单文件生成器、code-behind 同步与项目项协调
都在 `PolarisTools/Magic/` 下，理由写在各文件的类注释里。

## 代码结构

    Authoring/     .pmagic 文档模型、名字规则、code-behind 签名契约
                   （纯 BCL；PolarisTools 以 Compile Link 链接同一份文件）
    Definition/    生成代码消费的契约：MagicDefinition / Builder / 提供器特性 / MagicTaskCallback
    Runtime/       中间层：时钟、同步上下文、等待表、实例、Context、取消泵、
                   MagicObject 与图片/特效句柄
    Game/          原版接入：数字 ID 分配与持久化、FEnum 双向解析、MKind 注入、
                   holder 安装、实例总表
    Game/Patch/    Harmony 补丁（MDAT / MKind / MGContainer / MagicSelector / MagicItem）
    MagicAPI.cs    模组用的公开门面：授予/收回魔法、登记特效规格、查注册结果

## 模组需要主动做的两件事

魔法本身的注册不需要模组调用任何东西——生成的提供器带特性，PolarisMagic 启动时扫描到它。
注册时机必须早于读档，所以不能依赖模组代码跑得够早。

需要模组自己调的只有：

1. `MagicAPI.RegisterEffect(id, spec)`：登记 `AttachEffect` 用的精灵表特效规格。
2. `MagicAPI.Grant(magicId)`：把魔法授予玩家。自定义魔法**默认不解锁**——注册了就自动出现在魔法菜单里，
   会让玩家在装了模组的存档里看到一堆自己没学过的东西。

## 数字 ID 与存档

玩家存档里存的是 `ushort` 数字 ID，不是字符串 Id。PolarisMagic 在 30000–39999 区间分配数字 ID 并持久化到
`BepInEx/Polaris/magic-ids.txt`；这份映射**只增不改**——编辑或重排它等于悄悄改写玩家已经学会的东西。
