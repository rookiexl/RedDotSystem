# Red Dot Change Log

## 2026-11-27

- 红点父子关系改为“本地绑定 + 统一派生有效单元”，静态注册在 `CompleteRegistration()` 后统一解析，父节点注册顺序不再影响汇总结果。
- 修复同一运算单元被多个子节点复用时，移除其中一个子节点会误删父级汇总的问题。
- 新增“子节点先注册”和“共享单元解绑”回归测试。
- `CompleteRegistration()` 增加配置校验报告，检查重复注册、父节点缺失、空叶子、单元/计算函数缺失和未使用单元。
- 运算单元计算和 UI 订阅回调增加异常隔离；计算异常保留最后一次有效值，不再阻断同批其他刷新。
- 自动配置校验默认只在 Editor 和 Development Build 执行，Release Player 可通过 `EnableAutomaticValidation` 手动开启。
- 订阅者改为数组存储，普通通知不再分配 `GetInvocationList`；校验空叶子检查和派生索引重建减少重复分配。
- 新增 `RedDotScope`：动态红点先暂存注册项，`Commit()` 时统一写入并只重建一次，`Dispose()` 时批量移除并清理 unit 结果缓存。
- 活动动态红点迁移到 Scope 生命周期，失败注册和处理器异常会回滚本次未完成节点。

## 2026-11-26

- 红点框架拆分为 `RedDot.Runtime`、`RedDot.Unity`、`RedDot.Editor`。
- 业务代码统一进入 `Game.Business`，公共基础进入 `Game.Core`。
- `RedDot.Runtime` 移除对 `Game.Core`、业务事件、业务 Model 和业务初始化器的依赖。
- 业务红点 ID、业务 Handler 基类、系统解锁适配和组合逻辑全部移入业务层。
- `RedDotWidget` 改为自管理订阅，`BaseUI` 不再感知红点生命周期。
- `GameLauncher` 改为 `GameStartupPipeline + GameModuleManager` 分阶段启动。
- 各业务模块自行注册红点，红点 Runtime 初始化、规则注册和首次计算已分离。
- 新增 `RedDot.Tests`，覆盖订阅释放、动态节点、模块注册阶段和程序集依赖守卫。

## 2026-11-25

- 红点订阅改为 `IRedDotService.Subscribe` + `IDisposable` 方式。
- `RedDotWidget` 独立管理 `OnEnable/OnDisable` 订阅。
- 移除 `BaseUI` 中的 `BindAllRedDotNames/UnbindAllRedDotNames`。
- 拆分 `CommonBtnWidget` 的文本、点击和红点配置职责。
- 完成 Unity BatchMode 编译、EditMode 测试和 PlayMode 场景冒烟验证。

## 当前启动流程

1. 初始化红点 Runtime。
2. 按顺序初始化业务模块及其 Model。
3. 各业务模块自行注册红点定义。
4. 完成注册后执行首次红点计算。
5. 主界面最后启动。

## 待办

- 详细优化项见 `Docs/RedDotOptimizationBacklog.md`。
