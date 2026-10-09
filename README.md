# 圣穹幽墟 · Sky Sanctum: The Hollow

> 一款基于 **Unreal Engine 5.6** 的双人局域网联机剧情向动作解谜游戏。
> 两名猎魔人受委托进入尘封百年的黑石修道院，调查全员失踪惨案，在畸变横行的绝境中解谜求生，层层揭开教会秘辛。

**第三人称越肩视角** · **双人协作** · **密室压迫感** · **宗教恐怖氛围** · **UE 写实暗黑画质**

---

## 目录

- [项目概览](#项目概览)
- [世界观与核心体验](#世界观与核心体验)
- [关卡流程](#关卡流程)
- [技术架构总览](#技术架构总览)
- [核心系统实现](#核心系统实现)
  - [1. 局域网联机与房间系统](#1-局域网联机与房间系统)
  - [2. 角色与双武器系统](#2-角色与双武器系统)
  - [3. 战斗与伤害系统](#3-战斗与伤害系统)
  - [4. 物品与拾取交互](#4-物品与拾取交互)
  - [5. 对话触发与解谜机制](#5-对话触发与解谜机制)
  - [6. 怪物 AI 系统](#6-怪物-ai-系统)
  - [7. 关卡推进与等待机制](#7-关卡推进与等待机制)
  - [8. UI 与 HUD](#8-ui-与-hud)
- [性能优化实战](#性能优化实战)
- [工程目录结构](#工程目录结构)
- [开发环境](#开发环境)
- [第三方资产说明](#第三方资产说明)

---

## 项目概览

| 项目 | 内容 |
| --- | --- |
| 项目名 | 圣穹幽墟 / Sky Sanctum: The Hollow |
| 引擎版本 | Unreal Engine 5.6（DX12 / SM6） |
| 游戏类型 | 双人协作 · 剧情向 · 动作解谜 · 恐怖 |
| 视角 | 第三人称越肩（Over-the-Shoulder） |
| 联机模式 | 局域网 / IP 直连，2 人合作（Listen Server） |
| 关卡数量 | 4 关（复用 3 张地图） |
| 开发方式 | 全蓝图（Blueprint）+ Enhanced Input，C++ 仅保留模板模块 |
| 渲染管线 | Lumen 动态全局光照 + 虚拟阴影贴图（VSM）+ World Partition |
| 目标帧率 | 打包后 50+ FPS（优化前 10 FPS） |

> **说明**：项目采用纯蓝图开发，`Source/` 下仅有 UE 模板生成的基础模块，全部玩法逻辑（AI、战斗、联机、UI、关卡流程）均以蓝图资产实现，因此仓库体积与资产组织是本项目的核心信息载体。

---

## 世界观与核心体验

**背景**：与世隔绝、尘封百年的黑石修道院。

**剧情**：两名猎魔人受委托前往调查修道院全员失踪的惨案。随着探索深入，他们发现这里已被「畸变」占据，而教会的秘辛隐藏在一本本日记、一张张药剂配方之后。

**核心体验设计**：
- **密闭空间压迫感** —— 修道院回廊、地牢、墓地的封闭地形，配合动态光源与声音设计制造心理压力
- **双人协作战斗** —— 敌人数值按两人设计，一人拉仇恨、一人输出，近战与远程需要分工
- **宗教恐怖氛围** —— 低吼、倒地死亡、氛围音效与暗黑写实画质共同营造

---

## 关卡流程

4 个关卡复用 3 张地图，通过服务器端流程串联：

```
              ┌─────────────────────────────────────────────┐
              │  启动关  StartBlack / 主菜单 MainMenu        │
              │  （片头 CG · 搜索会话 / 输入 IP · 创建房间）  │
              └────────────────────┬────────────────────────┘
                                   │ 服务器 OpenLevel ?listen
                                   ▼
   ① 修道院门口 ····································· Content/abbey.umap
   ② 教堂 Cathedral ·············· Content/Cathedral/Maps/DemoMap/Level001
   ③ 修道院回廊与墓地 ······························· Content/abbey.umap
   ④ 地牢 Dungeon ································ Content/Dungeon/Levels/Level002
                                   │
                                   ▼
                        通关结算 / 性能分析
```

| 关卡 | 地图资产 | 关卡特征 |
| --- | --- | --- |
| ① 修道院门口 | `abbey.umap` | 室外开场，引导玩家进入建筑，教学式节奏 |
| ② 教堂 | `Cathedral/Maps/DemoMap/Level001.umap` | 高大空间 + 彩色玻璃光照，与密闭地牢形成对比 |
| ③ 修道院回廊与墓地 | `abbey.umap`（复用） | 回廊、墓地，解谜与探索比重上升 |
| ④ 地牢 | `Dungeon/Levels/Level002.umap` | 密闭压迫感最强，怪物密度最高 |

**关卡衔接方式**：每个关卡的终点放置 `BP_JoinLevel` 触发箱。两名玩家**全部进入触发区**后，由**拥有权威的服务器**执行 `OpenLevel(..., Options="?listen")` 加载下一关，客户端自动跟随重连 —— 详见 [关卡推进与等待机制](#7-关卡推进与等待机制)。

---

## 技术架构总览

```
┌──────────────────────────────────────────────────────────────────────┐
│                          网络层 (Network Layer)                      │
│   OnlineSubsystem = Null  +  OnlineSubsystemUtils.IpNetDriver        │
│   Listen Server 权威模型  │  会话搜索  │  IP 直连  │  属性复制/RepNotify │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
        ┌───────────────────────┼───────────────────────┐
        ▼                       ▼                       ▼
┌────────────────┐    ┌────────────────────┐    ┌──────────────────┐
│   玩家侧        │    │      世界侧         │    │      AI 侧        │
├────────────────┤    ├────────────────────┤    ├──────────────────┤
│ BP_MyPlayer    │    │ BP_GameMode_xiang  │    │ BT_AI_Tree       │
│  ・AC_Damage   │    │ BP_GameState       │    │ BB_AI_Data       │
│  ・AC_Bow      │    │ BP_PlayerState     │    │ BPAC_KZQ         │
│  E_WeaponState │    │ BP_JoinLevel       │    │  (AIPerception)  │
│  摆荡/瞄准/近战 │    │ 关卡推进 / 队伍管理 │    │ BTService / Task │
├────────────────┤    ├────────────────────┤    ├──────────────────┤
│   物品/交互侧   │    │     UI 侧           │    │  BP_BaseMonster  │
├────────────────┤    ├────────────────────┤    │   └ NPC01        │
│ BP_BaseItem    │    │ W_MyPlayerHUD      │    │   └ Boss1        │
│  └ 血瓶/钥匙    │    │ W_MinMap (小地图)   │    │   └ Child03      │
│  └ 油壶/纸条    │    │ W_ServerSession    │    │ 攻击/受击/死亡动画 │
│  └ 骸骨碎片     │    │ W_FindIP           │    │ 蒙太奇数据表      │
│ BP_Chufa (对话) │    │ WBP_Notice / 过场   │    │ BPW_NPCHealth    │
└────────────────┘    └────────────────────┘    └──────────────────┘
                                │
                                ▼
        ┌──────────────────────────────────────────────────┐
        │  持久化层  BP_MyGameInstance                      │
        │  跨关卡保存：当前关卡 / 玩家人数 / 死亡状态 /      │
        │             打靶计数 / 关卡进度标记                │
        └──────────────────────────────────────────────────┘
```

---

## 核心系统实现

### 1. 局域网联机与房间系统

**网络方案**：项目未使用 Steam 等在线子系统，而是通过 `OnlineSubsystem = Null` + IP 直连实现**纯局域网联机**，避免第三方依赖，部署即用。

```ini
; Config/DefaultEngine.ini
[OnlineSubsystem]
DefaultPlatformService=Null

[/Script/Engine.GameEngine]
+NetDriverDefinitions=(DefName="GameNetDriver",
    DriverClassName="OnlineSubsystemUtils.IpNetDriver",
    DriverClassNameFallback="OnlineSubsystemUtils.IpNetDriver")
```

**两种加入方式**：

| 方式 | 实现资产 | 说明 |
| --- | --- | --- |
| 搜索会话 | `Widget/W_ServerSession`、`_Li/WBP/WBP_02/WBP_FindRoom` | 遍历局域网内已广播的会话，列表展示后一键加入 |
| 输入 IP | `Widget/W_FindIP` | 手动输入主机 IP 直连，用于跨网段/调试 |

**服务器权威模型**：
- 房主以 **Listen Server** 身份运行（`?listen`），同时是玩家之一
- 关卡切换、物品归属判定、伤害结算等关键逻辑全部走 **Authority 校验**
- 表现层（动画、特效、UI）通过 **Multicast / RepNotify** 同步到各客户端

**同步要点**：早期版本的最大难点是 NPC 相关的 UI、动画与接口在客户端表现不一致。解决方案是把 NPC 的感知结果与状态枚举（`NPC_State`）作为**复制属性**下发，动画蒙太奇通过 `Multicast` 触发，客户端只负责播放表现而不做逻辑判定。

---

### 2. 角色与双武器系统

**角色资产**：`Player/BP_MyPlayer_1`、`xiang/BP_Player1`

**双武器切换（Q / E）**：

每个角色携带**近战刀**与**弓箭**两套武器，通过 `E_WeaponState` 枚举驱动整体状态机：

```
E_WeaponState
   ├─ 刀  ──► ApplyWeaponStateVisual()  → 骨骼网格/挂点切换 + 近战动画层
   └─ 弓  ──► ApplyWeaponStateVisual()  → 持弓姿态 + 瞄准动画层
              ▲
              └── CanSwitchWeapon() 校验（攻击/受击/摆荡中禁止切换）
                        │
              OnRep_WeaponState()  ──► 客户端同步表现
```

| 机制 | 关键实现 |
| --- | --- |
| 状态校验 | `CanSwitchWeapon` 在切换前检查是否处于攻击、受击、摆荡等互斥状态 |
| 网络同步 | `OnRep_WeaponState` 复制回调，保证两端武器外观一致 |
| 表现切换 | `ApplyWeaponStateVisual` 统一处理模型显隐与动画蓝图切换 |

**越肩瞄准系统**：
- `IsAiming` 状态驱动相机从肩后拉近（`AimFOV` 插值）
- `AimTimeLine` 时间轴控制相机过渡曲线，避免瞬切造成眩晕
- `AimOffset`（AO_Aim / AO_Aim_Up / AO_Aim_Down / AO_Aim_middle）实现瞄准状态下上半身独立朝向
- `OnClientAimCameraBegin` 保证只有本地玩家触发相机变焦

**弓与箭**：`Bow/AC_Bow`（弓箭组件）+ `Bow/BP_Bow`（弓 Actor）+ `Arrow/BP_Arrow`（箭矢）
- `E_BowState` 枚举管理 拉弓 → 半拉 → 满拉 → 过拉（`Bow_HalfDraw_Aim` / `Bow_HalfToOverDraw_Aim`）的动画与蓄力
- 箭矢拖尾使用 `ArrowTrail/FX/NS_ArrowTrail_*` 系列 Niagara 特效

**摆荡 / 抓钩机制**：`Background/BP_SwingPoint` + 角色内的 `CheckSwingPoint`

| 函数 | 职责 |
| --- | --- |
| `CheckSwingPoint` | 检测角色附近可抓取的摆荡点 |
| `CalculateSwingForce` | 依据距离与相对速度换算摆荡力度 |
| `CanSwing` | 摆荡可用性判定（冷却、状态互斥） |
| `OnRep_Swinging` / `OnRep_SV_CanSwing` | 复制回调，解决客户端摆荡拉扯抖动问题 |

> 摆荡拉扯抖动曾在联机下非常明显。修复思路是让**服务器判定摆荡状态与力度**，客户端仅做插值表现，避免两端各自模拟导致的位置冲突。

**其他移动能力**：`IA_Roll`（翻滚）、`IA_SlowWalk`（慢走，用于解谜/潜行）、`IA_Jump`

---

### 3. 战斗与伤害系统

**伤害组件**：`Player/AC_Damage` —— 挂载在角色上的独立 ActorComponent

| 成员 | 作用 |
| --- | --- |
| `Health` / `MaxHealth` | 当前/最大生命值（复制） |
| `DamageAmount` / `DamageCauser` | 伤害数值与来源，用于仇恨与击杀归属 |
| `Dead` | 死亡状态标记 |
| `AddHealth` | 回血入口（血瓶调用） |
| `GetHealth` / `GetMaxHealth` | 供 UI 与 AI 查询 |

**近战系统**：
- `IA_Swing` 触发挥砍，通过 `Multicast_PlayMeleeAttack` 让所有客户端同步播放攻击蒙太奇
- `EnableSwordHit` / `DisableSwordHit` 精确控制**攻击判定窗口** —— 只在挥砍动作的有效帧内开启碰撞，避免「挥空气也能打中」
- 自定义碰撞通道 `SwingPoint`（`ECC_GameTraceChannel1`，默认 `Block`）专门用于武器轨迹检测

**远程系统**：`IA_Aim`（瞄准）+ `IA_Fire`（射击），箭矢以物理模拟飞行，命中后结算伤害

**受击反馈链路**：
```
命中 → AC_Damage 扣血 → Is_Struck 置位 → 播放受击蒙太奇
                                  │
                                  └─► 血量归零 → Dying 蒙太奇 → 死亡销毁通知
```

---

### 4. 物品与拾取交互

**统一的物品基类**：`_Li/Item/BP_BaseItem`

所有可拾取物继承自 `BP_BaseItem`，通过 **`Execute_Pickup_Logic` 事件**派发各自效果，实现「一套拾取框架 + 多种物品行为」：

| 物品 | 资产 | 效果 |
| --- | --- | --- |
| 血瓶 | `BP_AddBlood` | 调用 `AddHealth` 回血（`Blood_Vial11` 模型） |
| 钥匙 | `BP_King` | 解谜关键道具（`SM_Key_01`） |
| 油壶 | `BP_Oil` | 放置类道具（`MotorOil_low`），配合场景机关 |
| 纸条 / 日记 | `BP_Paper` | 触发 `Client_ShowPaperUI`，向玩家展示文字内容 |
| 骸骨碎片 | `BP_Bones` | 收集品（`Client_Bones` 通知） |
| 其他 | `BP_Chu` / `BP_FastW` | 关卡专用交互物 |

**先到先得的所有权机制**：
- `Owner` / `OwnerClass` 记录物品归属者，**复制到所有客户端**
- 一旦被玩家 A 拾取，玩家 B 无法再拾取，且 B 端会看到物品消失
- `TargetItem` 字段用于「对目标物品使用」的二段交互（例如把油壶放到陶罐里）

**交互提示**：`HUD_InteractPrompt` 在角色靠近可交互物时弹出提示 UI；拾取反馈通过 `BPW_Item` / `BPW_Paper` 呈现。

---

### 5. 对话触发与解谜机制

**对话触发箱**：`_Li/Talk/BP_Chufa`

- 基于 `ActorBeginOverlap` 的触发盒，玩家进入即触发剧情对话
- **各自独立触发** —— 两名玩家分别进入各自的触发盒，互不影响，保证每个人都能看到剧情，不会因为一人先走过而跳过
- 配合 `WBP_Dis` 对话 UI 与 `Talk` / `Talk1` 对话数据

**解谜设计**：沿主线串联多个环境谜题，道具与场景一一对应：

```
  第一本日记（触发机关起点）──► 揭示线索
          │
          ▼
  第二本日记（记载药剂配方）──► 解密核心
          │
          ▼
  油壶 ──► 放置于角落陶罐 ──► 绞盘（木轮）──► 银钥匙（合成获得）
          │
          ▼
  炼金桌（房间中央）──► 血瓶（位于囚笼旁）
```

- **道具-场景强绑定**：`BP_Oil` 需要放到指定陶罐位置才生效，而非简单使用
- **线索递进**：日记 → 配方 → 钥匙，玩家需要两人协作分散寻找
- **小地图标记**：解谜相关的交互物会以图标形式显示在小地图上（见下节）

---

### 6. 怪物 AI 系统

**AI 架构**：Behavior Tree + Blackboard + AIPerception

```
        ┌──────────────── BPAC_KZQ（AIPerceptionComponent）────────────────┐
        │  AISense_Sight（视觉）  │  OnPerceptionUpdated  │  ActorPerception │
        │  ActorPerceptionUpdatedDelegate                                   │
        └───────────────────────────────┬───────────────────────────────────┘
                                        │ 写入感知结果
                                        ▼
        ┌──────────────── BB_AI_Data（黑板）──────────────────────────────┐
        │  BP_Actor          当前目标 Actor      Is_Sight     是否看见玩家  │
        │  NPC_State         行为状态枚举        Is_Struck    是否受击      │
        │  Is_Attacking      是否攻击中          Is_Dealth    是否死亡      │
        │  Patrol_Index      巡逻点索引          Is_Reverse   反向巡逻      │
        │  Patrol_Location   巡逻目标点          Is_S_Accessible 点可达性   │
        │  GlobalBox_Location 全局巡逻区域       SightTrack_Location 追踪点  │
        │  Random_Location / Range_Location / Range_Randius 随机与范围搜索  │
        │  Distance          与目标距离          SelfActor    自身          │
        └───────────────────────────────┬───────────────────────────────────┘
                                        │ 读取
                                        ▼
        ┌───────────────── BT_AI_Tree（行为树）───────────────────────────┐
        │  Service:  BTService_DistanceCheck（距离检测）                  │
        │            BTService_SetSpeed（按状态调移动速度）                │
        │  Task:     BTTask_TrackingPlayer（追踪玩家）                     │
        │            BTTask_JoinAttackState（进入攻击状态）                │
        │            BTTask_SwitchEU（切换行为状态枚举）                   │
        │  Decorator: BTDecorator_Blackboard / BTDecorator_Loop           │
        └─────────────────────────────────────────────────────────────────┘
```

**核心机制**：

| 机制 | 实现 |
| --- | --- |
| **视听感知** | `BPAC_KZQ` 挂载 `AISense_Sight`，感知结果实时写入黑板 `Is_Sight`、`SightTrack_Location` |
| **动态索敌** | 感知到玩家后写入 `BP_Actor` 与 `Distance`；`BTTask_TrackingPlayer` 持续更新目标位置；失去视野后转向 `SightTrack_Location` 做一段时间追击 |
| **脱战巡逻** | 目标丢失且超时后，`NPC_State` 切回巡逻态，`BTTask_SwitchEU` 切换状态并回到 `Patrol_Location` 巡逻 |
| **受击仇恨** | 被攻击时 `Is_Struck` 置位，强制转向攻击者，保证「打了就会还手」 |
| **攻击状态机** | `BTTask_JoinAttackState` 进入攻击后置 `Is_Attacking`，避免移动与攻击逻辑互相打断 |
| **死亡处理** | `Is_Dealth` 置位后停止行为树，播放 Dying 蒙太奇并广播销毁通知给所有客户端 |

**巡逻点搜索策略**（`_Li/AI/Task/`）—— 为不同关卡地形提供多种选点算法：

| 任务 | 策略 |
| --- | --- |
| `Fixed_Find` | 固定顺序遍历 |
| `Fixed_PositiveIndex` / `Fixed_ReverseIndex` | 正向 / 反向索引巡逻 |
| `Random_Find` | 区域内随机选点 |
| `Range_Find` | 以自身为圆心的范围选点（半径由 `Range_Randius` 控制） |
| `GlobalBox_Find` | 在全局包围盒（`GlobalBox_Location`）内选点 |
| `BTS_Is_Accessible` | 可达性校验，剔除寻路不可达的点 |

> **可达性校验**是本 AI 的实用细节：随机选点很容易选到墙里或跨楼层的位置，`BTS_Is_Accessible` 会在执行前过滤掉不可达点，避免怪物卡在寻路失败状态抖动。

**怪物种类**：以 `BP_BaseMonster` 为基类派生出三档敌人

| 分档 | 蓝图 | 骨架资产 |
| --- | --- | --- |
| 小怪 | `BP_NPC01` | `JV_Alien02`（Alien02） |
| Boss | `BP_Boss1` | `JV_NZ01`（NecroZombie01） |
| 精英 | `BP_BaseMonster_Child03` | `JV_1D03`（Cyborg02） |

**动画与表现**：
- `ABP_NPC` 动画蓝图统一管理移动、攻击、受击、死亡四类状态
- `DataTable_NPCMontage` / `BPS_NPCMontage` 数据表驱动不同怪物的蒙太奇映射，新增怪物无需改动画蓝图
- `BPW_NPCHealth` 为怪物血条 Widget，受击时显示

**音效**：`_Li/Sound/怪物/` 下按怪物类型区分攻击、受伤、死亡音效（男人形怪 / 女人形怪 / 小怪 / 爬行类怪物 / 大怪咬食 / 低吼咆哮 / 倒地死亡），并配有环境氛围音（地牢 / 户外环境 / 诡异低沉氛围）。

---

### 7. 关卡推进与等待机制

**关卡切换核心**：`_Li/Game/BP_JoinLevel`

```
   ┌──────────────────────────────────────────────────────────────┐
   │  BP_JoinLevel（终点触发区，含 BoxComponent）                   │
   ├──────────────────────────────────────────────────────────────┤
   │                                                              │
   │  OnComponentBeginOverlap                                     │
   │      └─► 判断重叠者是否为玩家 Pawn                            │
   │          └─► PlayersInZone.AddUnique(PlayerController)       │
   │                                                              │
   │  OnComponentEndOverlap                                       │
   │      └─► PlayersInZone.RemoveItem(PlayerController)          │
   │                                                              │
   │  ▼ 每帧 / 定时检查                                            │
   │  PlayersInZone.Length == PlayerArray.Length ?                │
   │      ├─ 否 ─► 显示 Map_Help（等待提示 UI），停留等待           │
   │      └─ 是 ─► HasAuthority() 校验                             │
   │                  └─► OpenLevel(LevelName, Options="?listen")  │
   │                         └─► 新关卡以 Listen Server 启动        │
   │                              └─► 客户端自动跟随重连            │
   └──────────────────────────────────────────────────────────────┘
```

**设计要点**：
- **全员到齐才推进**：使用 `PlayersInZone` 数组而非单点检测，确保两名玩家都不会被落下
- **服务器独占执行**：`HasAuthority()` 校验保证只有主机执行 `OpenLevel`，避免客户端各自切关导致的世界不同步
- **等待反馈**：未集齐时显示 `Map_Help` 提示，玩家知道在等队友而不是卡住了
- **`?listen` 参数**：新关卡继续以 Listen Server 模式启动，网络架构跨关卡延续，客户端无需重新建房

**跨关卡状态持久化**：`BP_MyGameInstance`

关卡切换会销毁世界，因此所有需要跨关保留的数据都放在 GameInstance 中：

| 数据 | 用途 |
| --- | --- |
| `LevelName` | 当前关卡标识，驱动关卡流程判断 |
| `PlayerNum` / `PlayerNow` | 玩家人数与当前在线数，用于等待机制与难度调整 |
| `DeadMan` | 玩家死亡状态，跨关卡保存 |
| `AimCurrentNum` / `AimMaxNum` / `CalculateAimNum` | 打靶/射击计数与进度统计 |
| `L01_*` | 各关卡进度标记 |
| `Map_Actor` | 小地图图标数据，跨关卡复用 |

---

### 8. UI 与 HUD

**主菜单流程**：

```
W_MainMenu
   ├─ 开始游戏 ──► W_GameBegin ──► 创建房间（Listen Server）──► 进入关卡
   ├─ 加入游戏 ──► W_JoinGame
   │                 ├─► W_ServerSession / W_FindRoom  （搜索局域网会话）
   │                 └─► W_FindIP                      （输入 IP 直连）
   ├─ 游戏介绍 ──► W_Introduce
   ├─ 设置     ──► W_Settings
   └─ 反馈     ──► W_Bug
```

**游戏内 HUD**：
- `W_MyPlayerHUD` —— 主 HUD，整合血量、武器状态、交互提示
- `W_MinMap` —— **可开关小地图**，基于 `TextureRenderTarget2D` 实时渲染场景俯视图，并把场景中的交互物映射为图标
- `准心` / `Bow` / `Sword` —— 不同武器状态下的准心与武器状态图标
- 系统通知 `_Li/WBP/Level/WBP_Notice`；暂停菜单 `_Li/WBP/WBP_3/WBP_Pause`；死亡界面 `_Li/WBP/WBP_3/WBP_Died`

**小地图图标系统**（资产位于 `_Li/Map/`）：

| 图标 | 对应交互物 |
| --- | --- |
| 钥匙 / 轮子 | 关键道具与机关 |
| 日记 / 圣物碎片 | 剧情收集物 |
| 血包 / 血药 | 补给品 |
| 箱子 / 药锅 / 骸骨碎片 / 叉 | 场景交互物 |

实现上使用 `Actor_Icon` 标记场景中需要显示的 Actor，`WBP_Actor_Icon` 负责将其投影到小地图坐标。

**片头 CG**：`Movies/start CG.mp4` 通过 `NewMediaPlayer` / `NewMediaPlayer_Video` 在启动关 `StartBlack` 播放，配合 `WBP_Black` 黑屏过渡与 `WBP_StartBlack` 启动画面。

**彩蛋**：`Widget/W_Easter` —— 触发式彩蛋 UI（含彩蛋特效与称号赋予）。

---

## 性能优化实战

> 这是本项目最有价值的技术环节之一：打包版本开局帧率仅 **10 FPS 左右**，经过系统性排查与整改后**稳定提升至 50+ FPS**。

### 排查方法

使用 **Stat GPU** 抓取 GPU 各阶段耗时分布，结合 **Unreal Insights** 做 CPU/GPU 时间线分析，定位到帧率下降的三大主因：

```
Unreal Insights 时间线
   │
   ├─► GPU BasePass 耗时异常高 ──► 追查到动态光源重叠
   ├─► GPU ShadowDepths 耗时异常 ──► 追查到动态物体开启了静态阴影贴图
   └─► Nanite 相关 Pass 耗时异常 ──► 追查到透明材质开启了 Nanite
```

### 定位到的三类问题与整改

| # | 问题 | 原因分析 | 整改措施 |
| --- | --- | --- | --- |
| 1 | **动态光源重叠** | 关卡内多个点光源/聚光灯照射范围大量重叠，每个光源都产生独立的阴影与光照计算，BasePass 与光照开销随光源数线性增长 | 合并光照区域、减少重叠光源数量，将可静态化的光源改为静态光照，保留关键氛围光源为动态 |
| 2 | **动态物体开启静态阴影贴图** | 部分动态物体被错误地配置为投射静态阴影贴图，导致阴影贴图需要重建/无法正确缓存，ShadowDepths 阶段反复开销 | 关闭动态物体的静态阴影投射，改由虚拟阴影贴图（VSM）处理 |
| 3 | **透明材质开启 Nanite** | Nanite **不支持**透明材质，透明物体启用 Nanite 会导致回退到非 Nanite 路径，产生额外的材质分支与绘制开销 | 关闭透明物体的 Nanite，改为常规 LOD 网格 |
| 4 | **植被投射阴影** | 大量刷的草/植被投射阴影，阴影绘制调用数量庞大 | 关闭植被的阴影投射（`bCastShadow = false`） |

### 优化结果

| 指标 | 优化前 | 优化后 |
| --- | --- | --- |
| 开局帧率 | ~10 FPS | **50+ FPS** |
| 关卡加载 | 帧率骤降明显 | 平稳 |

**经验总结**：
- **动态光源是隐形杀手** —— 美术布光时容易为了氛围堆叠光源，而每个动态光源的阴影开销是实打实的。布光阶段就应该规划好哪些光源是静态的
- **Nanite 不是万能的** —— 透明材质与 Nanite 天然不兼容，盲目开启反而更慢
- **打包版本 ≠ 编辑器版本** —— 编辑器下的表现会掩盖很多问题，性能必须在**打包后**用 Stat GPU / Insights 实测

---

## 工程目录结构

```
TwoPersonArcheryBattle5-6-1/
├── Config/                        项目配置
│   ├── DefaultEngine.ini          渲染管线 / 网络（IpNetDriver）/ 碰撞通道 / 打包地图
│   ├── DefaultGame.ini            打包设置 / MapsToCook / 压缩（Oodle Kraken）
│   └── DefaultInput.ini           Enhanced Input 全局设置
│
├── Content/
│   ├── _Li/                       ★ 核心玩法资产
│   │   ├── AI/                    行为树、黑板、AI 感知组件、巡逻点搜索 Task
│   │   │   ├── BT_AI_Tree         怪物行为树
│   │   │   ├── BB_AI_Data         黑板数据
│   │   │   ├── BPAC_KZQ           AIPerceptionComponent（视听感知）
│   │   │   ├── EU_NPCState        NPC 状态枚举
│   │   │   └── Task/              自定义 BT Task / Service
│   │   ├── BP/                    怪物蓝图层级（01/02/03 三档 + Source 基类）
│   │   ├── Item/                  ★ 物品系统（BP_BaseItem 基类 + 各类道具 + 美术资产）
│   │   ├── Game/                  关卡流程（BP_JoinLevel / BP_Level001 / BP_Men 门 / Wall）
│   │   ├── Map/                   ★ 小地图系统（图标、渲染目标、WBP_Main_Map）
│   │   ├── Talk/                  对话触发与对话 UI（BP_Chufa / WBP_Dis）
│   │   ├── WBP/                   ★ 全套 UI（主菜单、HUD、过场、通知、彩蛋）
│   │   ├── Anim_assets/Anim_IK/   动画资源与 IK
│   │   ├── Monster_Assete/        怪物美术资产（Alien02 / NecroZombie01 / Cyborg02）
│   │   ├── Sound/                 音效（怪物分类音效 + 关卡氛围音）
│   │   ├── DataTable/             怪物蒙太奇数据表
│   │   └── Man/ Mesh/             角色服装与网格
│   │
│   ├── xiang/                     角色蓝图与动画（BP_Player1 / BP_GameMode_xiang / ABP）
│   ├── Player/                    玩家角色、伤害组件、动画蓝图、DamageInterface
│   ├── Input/                     Enhanced Input（IA_* 动作 + IMC_* 映射上下文）
│   ├── Widget/                    通用 UI 资产（小地图、HUD、设置、彩蛋、准心）
│   ├── MainMenu/                  主菜单关卡与 GameMode/Controller/Pawn
│   ├── Bow/ Arrow/ ArrowTrail/    弓箭武器、箭矢、拖尾特效
│   ├── Movies/                    片头 CG 视频
│   ├── material/ abbey_sharedassets/  地形与材质
│   ├── abbey.umap                 关卡①③ 地图（World Partition + HLOD）
│   ├── Cathedral/                 关卡② 教堂地图
│   ├── Dungeon/                   关卡④ 地牢地图与道具
│   ├── ThirdPerson/               模板角色与地图（基础工程）
│   ├── LevelPrototyping/          原型白模、门、跳板、靶子
│   └── __ExternalActors__/        World Partition 外部 Actor（abbey 地图）
│
├── Plugins/
│   ├── Puerts/                    TypeScript 脚本插件（预留，当前未使用）
│   └── UE5.4-AdvancedSessions-main/  Advanced Sessions 插件（会话系统备用）
│
├── Source/                        C++ 模块（UE 模板生成，玩法逻辑均在蓝图中）
└── TypeScript/                    Puerts 脚本目录（预留）
```

---

## 开发环境

| 项 | 要求 |
| --- | --- |
| 引擎 | Unreal Engine **5.6** |
| 图形 API | DirectX 12 / Shader Model 6 |
| IDE | Visual Studio 2022（含 UE 开发组件） |
| 平台 | Windows 10 / 11 |
| 渲染特性 | Lumen（动态 GI）、虚拟阴影贴图（VSM）、World Partition、Nanite（仅限不透明物体） |

**运行方式**：

1. 使用 UE 5.6 打开 `ArcheryBattle_5_6_1.uproject`
2. 编辑器启动地图为 `Content/_Li/WBP/StartBlack`（启动/主菜单关）
3. 联机测试：编辑器内将 **Play Number of Players 设为 2**，Net Mode 选 **Play As Listen Server**

**打包**：通过 `Config/DefaultGame.ini` 的 `MapsToCook` 指定打包所需地图：

```ini
MapsToCook=(FilePath="/Game/_Li/WBP/StartBlack")     ; 启动关
MapsToCook=(FilePath="/Game/Cathedral/Maps/DemoMap/Level001")  ; 教堂
MapsToCook=(FilePath="/Game/Dungeon/Levels/Level002")          ; 地牢
MapsToCook=(FilePath="/Game/abbey")                            ; 修道院（复用两关）
```

**局域网联机**：确保两台机器处于同一网段；主机创建房间后，客机通过「搜索会话」自动发现，或通过「输入 IP」直接填入主机内网 IP。

---

## 第三方资产说明

本项目的美术资产来自 Epic 官方示例与 Fab / 虚幻商城素材包，**全部玩法逻辑、AI、联机、UI 与关卡串联均为自主开发**。主要资产包：

| 资产包 | 用途 |
| --- | --- |
| Cathedral / Dungeon / Castle | 教堂、地牢、城堡场景模块 |
| Insane Character Pack 01 | 怪物模型（Alien02 / NecroZombie01 / Cyborg02 等） |
| Third Person Template | 基础角色与动画框架 |
| Mixamo / AssassinGirl | 角色动画与骨骼 |
| Realistic Starter VFX Pack Vol.2 | 通用 Niagara 特效 |
| Arrow Trail FX | 箭矢拖尾特效 |
| LevelPrototyping | 原型白模、门、跳板 |
| DirtTerrainPack / PC3D_BrackenFern | 地形与植被 |

---

<div align="center">

**圣穹幽墟 · Sky Sanctum: The Hollow**

*Unreal Engine 5.6 · Blueprint · 双人局域网联机 · 50+ FPS*

</div>
