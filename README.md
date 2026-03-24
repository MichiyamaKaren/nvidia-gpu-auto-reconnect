# GPU Auto Reconnect

Windows 系统托盘工具，自动监测 NVIDIA GPU 性能状态（P-State），当检测到显卡卡在低性能模式（如 P8）且使用外接电源时，自动执行禁用/重新启用操作以恢复显卡性能。

## 功能

- 定期通过 `nvidia-smi` 查询 GPU P-State
- 检测到低性能状态 + 交流电源供电时自动重置 GPU（禁用 → 等待 → 启用）
- 连续多次检测确认后才触发重置，避免误操作
- 系统托盘图标实时显示状态（绿色=正常，黄色=暂停，红色=异常）
- 左键点击托盘图标打开设置界面
- 右键菜单：暂停/恢复监控、查看日志、退出
- 气泡通知 GPU 重置事件
- 支持开机自启（通过 Windows 任务计划）

## 可配置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| P-State 阈值 | P8 | 触发重置的 P-State（大于等于该值视为低性能） |
| 检测间隔 | 30 秒 | 每次查询 nvidia-smi 的间隔 |
| 连续检测次数 | 3 次 | 需连续 N 次检测到低性能才触发重置 |
| 重新启用延迟 | 10 秒 | 禁用 GPU 后等待多久再重新启用 |
| 自动重置 | 开启 | 是否启用自动重置功能 |
| 开机自启 | 关闭 | 是否随 Windows 启动 |

配置文件位置：`%APPDATA%\GpuAutoReconnect\settings.json`

## 前置要求

- Windows 10/11
- .NET 9.0+ 运行时
- NVIDIA 显卡 + 已安装驱动（nvidia-smi 可用）
- 需要管理员权限运行（禁用/启用设备需要）

## 构建

```bash
dotnet build src/GpuAutoReconnect/GpuAutoReconnect.csproj -c Release
```

## 运行

需要以管理员权限启动。程序启动后最小化到系统托盘。

## 日志

日志文件位置：`%APPDATA%\GpuAutoReconnect\logs\`
- 按日分割：`log_YYYYMMDD.txt`
- 崩溃日志：`crash.log`
