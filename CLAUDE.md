# CLAUDE.md

## 项目概述

GPU Auto Reconnect — Windows 系统托盘应用，监控 NVIDIA GPU P-State 并在卡在低性能状态时自动重置设备。

## 技术栈

- C# / .NET 9.0 WinForms（`net9.0-windows`，`RollForward: LatestMajor`）
- NuGet 依赖：`System.Management`（WMI 查询 GPU 设备）
- 无其他外部依赖

## 项目结构

```
src/GpuAutoReconnect/
├── Program.cs                 # 入口，单实例 Mutex，全局异常处理
├── app.manifest               # UAC requireAdministrator
├── Models/
│   ├── PState.cs              # 枚举 P0-P12
│   └── AppSettings.cs         # 设置模型
├── Services/
│   ├── GpuMonitorService.cs   # 核心监控循环（System.Threading.Timer 单次触发模式）
│   ├── GpuDeviceService.cs    # WMI 发现 GPU + pnputil 禁用/启用
│   ├── PowerService.cs        # 交流电源检测
│   ├── SettingsService.cs     # JSON 设置持久化到 %AppData%
│   ├── LogService.cs          # 文件日志 + 内存缓冲
│   └── StartupService.cs      # schtasks 管理开机自启
└── UI/
    ├── TrayApplicationContext.cs  # 托盘图标 + 右键菜单，左键打开设置
    ├── SettingsForm.cs            # 设置窗口
    └── LogViewerForm.cs           # 日志查看窗口
```

## 构建和运行

```bash
# 构建
dotnet build src/GpuAutoReconnect/GpuAutoReconnect.csproj -c Release

# 运行（需要管理员权限）
dotnet run --project src/GpuAutoReconnect -c Release
```

## 关键设计决策

- **WMI 而非 pnputil 解析**来发现 GPU 设备：pnputil 输出受系统语言影响，WMI 属性名是语言无关的
- **Timer 单次触发/重新激活模式**：防止 nvidia-smi 超时或 GPU 重置期间回调重叠
- **连续检测计数器**（默认 3 次）：防止瞬态 P-State 波动触发不必要的重置
- **pnputil /disable-device + /force**：因为 GPU 被 Windows 视为关键设备，需要 /force 标志
- **Task Scheduler 而非注册表 Run 键**管理自启：避免管理员应用每次登录弹出 UAC 提示
- NVIDIA 供应商 ID：`VEN_10DE`（硬编码常量，不会变）

## 数据存储

- 设置：`%APPDATA%\GpuAutoReconnect\settings.json`
- 日志：`%APPDATA%\GpuAutoReconnect\logs\log_YYYYMMDD.txt`
- 崩溃日志：`%APPDATA%\GpuAutoReconnect\logs\crash.log`

## 注意事项

- 修改 UI 后需要先关闭正在运行的实例（托盘右键 Exit），否则 exe 被锁定无法构建
- nvidia-smi 查询超时设为 5 秒，pnputil 操作超时设为 30 秒
- 多 GPU 环境下取 nvidia-smi 输出的第一行
