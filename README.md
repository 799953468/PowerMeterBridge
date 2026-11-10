# PowerMeterBridge

控制台程序，启动时从命令行传入刷新间隔秒数，定时读取辰域智控充值页里的
`剩余电量`、`剩余金额`，并通过 MQTT 自动注册到 Home Assistant。

## 功能

- 定时抓取目标网页
- 发布 Home Assistant MQTT Discovery 配置
- 发布 5 个传感器
  - `剩余电量`
  - `剩余金额`
  - `累计用电`
  - `累计花费`
  - `实时功率` ⚡ (根据用电量和时间差自动计算)
- 本地持久化累计值
  - 因为网页只给”剩余值”，所以程序会根据每次轮询之间的下降量，累加出
    `累计用电` 和 `累计花费`
  - 如果中途充值导致剩余值上升，程序会把它当作充值，不会冲掉累计值

## 项目结构

```
PowerMeterBridge/
├── src/
│   ├── Configuration/      # 配置相关
│   │   └── AppConfig.cs
│   ├── Models/             # 数据模型
│   │   └── BridgeState.cs
│   └── Services/           # 业务服务
│       ├── BridgeStateStore.cs
│       ├── CnyiotPageClient.cs
│       └── HomeAssistantMqttPublisher.cs
├── Program.cs              # 程序入口
├── appsettings.json.example
└── data/                   # 运行时数据
    └── bridge-state.json
```

## 运行前修改

编辑 `appsettings.json`，至少改这几项：

- `Mqtt.Host`
- `Mqtt.Port`
- `Mqtt.Username`
- `Mqtt.Password`

如果你要改 Home Assistant 的主题前缀，也可以改：

- `HomeAssistant.DiscoveryPrefix`
- `HomeAssistant.BaseTopic`

## 运行

第一个参数必须传 `刷新间隔秒数`。

```bash
dotnet run --project PowerMeterBridge -- 60
```

如果要显式指定配置文件，放在第二个参数：

```bash
dotnet run --project PowerMeterBridge -- 60 appsettings.json
```

## Home Assistant 中的使用方式

程序启动后会自动创建以下实体：

- `剩余电量` - 当前剩余电量
- `剩余金额` - 当前剩余金额
- `累计用电` - 自程序启动以来的累计用电量
- `累计花费` - 自程序启动以来的累计花费
- `实时功率` - 根据两次读数间的电量差计算出的平均功率

其中最关键的是 `累计用电`，它适合接到 Home Assistant 的 Energy Dashboard
里作为能耗累计传感器使用。

## 状态文件

默认状态文件位置：

```text
data/bridge-state.json
```

如果你迁移机器或重装程序，记得一起保留这个文件，否则 `累计用电` 和
`累计花费` 会从 0 重新开始。

