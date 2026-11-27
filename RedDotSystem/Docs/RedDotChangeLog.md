# Red Dot Change Log

## 2026-11-27

- 红点父子关系改为“本地绑定 + 统一派生有效单元”，静态注册在 `CompleteRegistration()` 后统一解析，父节点注册顺序不再影响汇总结果。
- 修复同一运算单元被多个子节点复用时，移除其中一个子节点会误删父级汇总的问题。
- 新增“子节点先注册”和“共享单元解绑”回归测试。

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
