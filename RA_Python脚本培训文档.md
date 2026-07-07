# RazorEnhanced Python 脚本培训文档（阳光大陆-永夜再临官方QQ群：757710228）

本文由JACK整理，面向阳光大陆玩家编写 RA Python 脚本使用学习。

适用对象：

- 想写自动补给、制作、整理背包、战斗辅助、采集脚本的玩家。
- 已经会一点 Python，但不熟悉 RazorEnhanced API 的玩家。
- 会改宏，但想把宏升级为可循环、可判断、可自动处理异常脚本的玩家。

重要说明：

- RA 使用 IronPython，脚本里可以直接调用 `Player`、`Items`、`Mobiles`、`Target`、`Gumps`、`Journal`、`Misc` 等对象。
- 物品 ID、颜色、序列号一般用十六进制，例如 `0x0E21`、`0x40012345`。
- 大多数移动、使用物品、目标选择动作都要加 `Misc.Pause(300)` 到 `Misc.Pause(1000)`，否则服务端可能来不及响应。
- 脚本运行前尽量确认背包、材料箱、目标箱已经打开或可见。
- 下文示例以常见 UO/RA 语法为准，实际按钮 ID、物品 ID 要按服务器情况调整。

## 一、Python 脚本基础写法

### 1. 基本结构

用途：让脚本从 `main()` 开始执行，便于维护。

```python
# -*- coding: utf-8 -*-

def main():
    Misc.SendMessage("脚本启动", 68)

if __name__ == "__main__":
    main()
```

场景：所有正式脚本都建议用这种结构。

### 2. 循环

用途：持续执行采集、制作、整理、战斗检查。

```python
while not Player.IsGhost:
    Misc.SendMessage("循环中", 68)
    Misc.Pause(1000)
```

场景：自动挖矿、自动铁匠、自动补血。

### 3. 条件判断

```python
if Player.Hits < Player.HitsMax * 0.5:
    Misc.SendMessage("血量低，准备治疗", 33)
else:
    Misc.SendMessage("血量安全", 68)
```

场景：按血量喝药、按重量丢弃成品、按材料数量补货。

### 4. 列表

```python
BANDAGE_IDS = [0x0E21]
TRASH_IDS = [0x13EE, 0x1413]

if item.ItemID in TRASH_IDS:
    Items.Move(item.Serial, trash_serial, item.Amount)
```

场景：多个物品类型共用同一逻辑。

### 5. 函数

```python
def log(text, hue=68):
    Misc.SendMessage("[脚本] " + str(text), hue)

log("准备开始")
```

场景：统一输出、统一找物、统一补材料。

### 6. 异常保护

```python
try:
    Items.UseItem(0x40012345)
except Exception as ex:
    Misc.SendMessage("使用物品失败：" + str(ex), 33)
```

场景：脚本不确定目标是否存在时，避免直接崩溃。

## 二、常用对象模型

### 1. Item 物品对象常用属性

获得方式：

```python
item = Items.FindByID(0x0E21, -1, Player.Backpack.Serial, True)
```

常用属性：

| 属性 | 说明 | 示例 | 场景 |
|---|---|---|---|
| `item.Serial` | 物品唯一序列号 | `Items.UseItem(item.Serial)` | 使用、移动、设为目标 |
| `item.ItemID` / `item.Graphics` | 物品类型 ID | `if item.ItemID == 0x0E21:` | 判断绷带、矿石、成品 |
| `item.Color` / `item.Hue` | 颜色 | `if item.Hue == 0:` | 区分同类型不同颜色物品 |
| `item.Amount` | 堆叠数量 | `Items.Move(item, chest, item.Amount)` | 移动整堆材料 |
| `item.Name` | 名称 | `Misc.SendMessage(item.Name)` | 调试显示 |
| `item.Container` | 所在容器序列 | `if item.Container == Player.Backpack.Serial:` | 判断是否在背包 |
| `item.RootContainer` | 根容器 | `if item.RootContainer == Player.Backpack.Serial:` | 判断是否属于背包子包 |
| `item.Contains` | 容器内物品列表 | `for x in bag.Contains:` | 遍历箱子/背包 |
| `item.IsContainer` | 是否容器 | `if item.IsContainer:` | 递归搜索子包 |
| `item.IsCorpse` | 是否尸体 | `if corpse.IsCorpse:` | 自动开尸体 |
| `item.OnGround` | 是否在地上 | `if item.OnGround:` | 捡地面物品 |
| `item.Position` | 坐标 | `item.Position.X` | 地面距离判断 |
| `item.Movable` | 是否可移动 | `if item.Movable:` | 自动捡/整理 |
| `item.Weight` | 重量 | `total += item.Weight` | 背包重量统计 |
| `item.Durability` / `MaxDurability` | 耐久 | `if item.Durability < 10:` | 装备耐久提醒 |
| `item.Properties` | OPL 属性 | `Items.GetPropStringList(item)` | 读取装备词条 |

### 2. Mobile 生物对象常用属性

获得方式：

```python
mob = Mobiles.FindBySerial(0x00001234)
```

常用属性：

| 属性 | 说明 | 示例 | 场景 |
|---|---|---|---|
| `mob.Serial` | 生物序列号 | `Player.Attack(mob.Serial)` | 攻击、治疗、跟随 |
| `mob.Name` | 名称 | `if "Jack" in mob.Name:` | 找指定玩家/NPC |
| `mob.Hits` / `HitsMax` | 当前/最大血量 | `if mob.Hits < mob.HitsMax:` | 自动治疗 |
| `mob.Mana` / `ManaMax` | 魔法 | `if mob.Mana < 10:` | 判断施法状态 |
| `mob.Stam` / `StamMax` | 体力 | `if mob.Stam < 20:` | 判断行动能力 |
| `mob.Notoriety` | 声望颜色 | `if mob.Notoriety in [3,4,5,6]:` | 战斗筛选敌人 |
| `mob.Position` | 坐标 | `mob.Position.X` | 距离/走位 |
| `mob.Visible` | 是否可见 | `if mob.Visible:` | 找目标 |
| `mob.Poisoned` | 是否中毒 | `if mob.Poisoned:` | 解毒 |
| `mob.Paralized` | 是否麻痹 | `if mob.Paralized:` | 状态判断 |
| `mob.WarMode` | 是否战斗模式 | `if mob.WarMode:` | 战斗脚本 |
| `mob.Backpack` | 背包对象 | `mob.Backpack.Serial` | NPC/尸体容器 |
| `mob.GetItemOnLayer("RightHand")` | 获取装备层物品 | `weapon = mob.GetItemOnLayer("RightHand")` | 判断武器 |

### 3. Player 玩家对象常用属性

`Player` 代表自己，不需要查找。

| 属性 | 说明 | 示例 | 场景 |
|---|---|---|---|
| `Player.Serial` | 自己序列号 | `Target.TargetExecute(Player.Serial)` | 对自己施法 |
| `Player.Name` | 角色名 | `Misc.SendMessage(Player.Name)` | 输出调试 |
| `Player.Backpack` | 背包 Item | `Player.Backpack.Serial` | 找背包物品 |
| `Player.Bank` | 银行箱 | `Player.Bank.Serial` | 银行整理 |
| `Player.Hits/HitsMax` | 血量 | `if Player.Hits < 60:` | 自动治疗 |
| `Player.Mana/ManaMax` | 蓝量 | `if Player.Mana < 20:` | 冥想/喝药 |
| `Player.Stam/StamMax` | 体力 | `if Player.Stam < 20:` | 喝体力药 |
| `Player.Weight/MaxWeight` | 重量 | `if Player.Weight > 380:` | 丢弃或回城 |
| `Player.Position` | 当前位置 | `Player.Position.X` | 路径、地图判断 |
| `Player.Map` | 地图编号 | `if Player.Map == 0:` | 地图逻辑 |
| `Player.WarMode` | 战斗模式 | `Player.SetWarMode(True)` | 自动战斗 |
| `Player.IsGhost` | 是否死亡 | `while not Player.IsGhost:` | 循环停止条件 |
| `Player.Poisoned` | 是否中毒 | `if Player.Poisoned:` | 解毒 |
| `Player.Buffs` | Buff 名称列表 | `"Consecrate Weapon" in Player.Buffs` | 判断状态 |
| `Player.Mount` | 坐骑 | `if Player.Mount:` | 骑乘判断 |

## 三、常见脚本场景模板

### 1. 背包找物品并使用

```python
BANDAGE = 0x0E21

bandage = Items.FindByID(BANDAGE, -1, Player.Backpack.Serial, True)
if bandage:
    Items.UseItem(bandage.Serial)
    if Target.WaitForTarget(2000, False):
        Target.Self()
else:
    Misc.SendMessage("背包没有绷带", 33)
```

场景：绷带治疗、使用工具、吃食物。

### 2. 从材料箱补材料

```python
INGOT = 0x1BF2
CHEST = 0x40012345

count = Items.BackpackCount(INGOT, -1)
if count < 50:
    ingot = Items.FindByID(INGOT, -1, CHEST, True)
    if ingot:
        Items.Move(ingot.Serial, Player.Backpack.Serial, 500)
        Misc.Pause(800)
```

场景：铁匠、裁缝、木匠自动补材料。

### 3. 遍历容器内容

```python
def get_container_items(container_serial, recursive=True):
    bag = Items.FindBySerial(container_serial)
    if bag is None:
        return []

    result = []
    for item in bag.Contains:
        result.append(item)
        if recursive and item.IsContainer:
            result.extend(get_container_items(item.Serial, True))
    return result

for item in get_container_items(Player.Backpack.Serial):
    Misc.SendMessage("物品：0x%X x%d" % (item.ItemID, item.Amount), 68)
```

场景：整理背包、丢弃成品、统计材料。

### 4. 选择目标箱

```python
Misc.SendMessage("请选择材料箱", 68)
chest = Target.PromptTarget("请选择材料箱", 68)
if chest > 0:
    Items.UseItem(chest)
    Misc.Pause(800)
```

场景：脚本启动时绑定材料箱、垃圾桶、目标容器。

### 5. 读取 Journal 判断结果

```python
Journal.Clear()
Items.UseItem(tool.Serial)
Misc.Pause(500)

if Journal.WaitJournal(["你制作了", "You create"], 3000):
    Misc.SendMessage("制作成功", 68)
elif Journal.Search("材料不足"):
    Misc.SendMessage("材料不足", 33)
```

场景：制作成功/失败判断、技能使用结果判断。

### 6. Gump 制作按钮

```python
Items.UseItem(tool.Serial)
if Gumps.WaitForGump(0, 5000):
    gid = Gumps.CurrentGump()
    Gumps.SendAction(gid, 21)
    Misc.Pause(800)
```

场景：制作上一项、点击菜单按钮。

### 7. 自动攻击最近敌人

```python
f = Mobiles.Filter()
f.Enabled = True
f.RangeMax = 10
f.Notorieties.Add(3)  # 灰
f.Notorieties.Add(4)  # 犯罪
f.Notorieties.Add(5)  # 敌对
f.Notorieties.Add(6)  # 红名

enemy = Mobiles.Select(Mobiles.ApplyFilter(f), "Nearest")
if enemy:
    Player.Attack(enemy)
```

场景：自动战斗辅助。

### 8. 定时器防止动作过快

```python
if not Timer.Check("heal"):
    Timer.Create("heal", 5000)
    Misc.SendMessage("5秒内不重复治疗", 68)
```

场景：喝药冷却、技能冷却、喊话间隔。

## 四、接口速查表

说明：

- `serial` 可以是序列号整数，也可以是对象。
- `color=-1` 表示任意颜色。
- `container=-1` 表示不限容器，`Player.Backpack.Serial` 表示自己背包。
- `recursive=True` 表示递归搜索子容器。
- `delay` 单位一般是毫秒。

### Items 物品接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Items.ApplyFilter(filter)` | 按过滤器返回物品列表 | `items = Items.ApplyFilter(f)` | 批量找地面物品、尸体、背包物品 |
| `Items.BackpackCount(itemid, color)` | 统计背包内指定物品数量 | `n = Items.BackpackCount(0x1BF2, -1)` | 判断材料是否不足 |
| `Items.ContainerCount(serial, itemid, color, recursive)` | 统计容器内数量 | `n = Items.ContainerCount(chest, 0x1BF2, -1, True)` | 材料箱库存统计 |
| `Items.FindByID(itemid, color, container, recursive)` | 找第一个指定类型物品 | `bandage = Items.FindByID(0x0E21, -1, Player.Backpack.Serial, True)` | 找工具、材料、药水 |
| `Items.FindAllByID(itemids, color, container, range, considerIgnoreList)` | 找多个类型的物品列表 | `lst = Items.FindAllByID([0x0F7A,0x0F7B], -1, -1, 2, True)` | 地面捡多种材料 |
| `Items.FindByName(name, color, container, range, considerIgnoreList)` | 按名称找物品 | `rune = Items.FindByName("rune", -1, Player.Backpack.Serial, 0, True)` | 找符石、特殊物品 |
| `Items.FindBySerial(serial)` | 按序列找物品 | `bag = Items.FindBySerial(0x40012345)` | 操作固定箱子 |
| `Items.UseItem(itemSerial)` | 双击使用物品 | `Items.UseItem(horn.Serial)` | 使用工具、开箱、吃药 |
| `Items.UseItem(itemSerial, targetSerial, wait)` | 使用物品并指定目标 | `Items.UseItem(tool, ore, True)` | 工具对材料使用 |
| `Items.UseItemByID(itemid, color)` | 按类型使用背包物品 | `Items.UseItemByID(0x0F0E, -1)` | 喝药 |
| `Items.Move(source, destination, amount)` | 移动物品到容器 | `Items.Move(item.Serial, chest, item.Amount)` | 整理背包 |
| `Items.Move(source, destination, amount, x, y)` | 移动到容器指定格子 | `Items.Move(item, bag, 1, 20, 40)` | 排列物品 |
| `Items.MoveOnGround(source, amount, x, y, z)` | 丢到地面坐标 | `Items.MoveOnGround(item, 1, Player.Position.X, Player.Position.Y, Player.Position.Z)` | 丢垃圾到地上 |
| `Items.DropItemGroundSelf(item, amount, direction)` | 丢到自己附近地面 | `Items.DropItemGroundSelf(item, 1)` | 快速丢弃 |
| `Items.Lift(item, amount)` | 拿起物品到手上 | `Items.Lift(item, 1)` | 高级拖放逻辑 |
| `Items.DropFromHand(item, container)` | 将手上物品放入容器 | `Items.DropFromHand(item, bag)` | 配合 Lift 使用 |
| `Items.WaitForContents(bag, delay)` | 等待容器内容加载 | `Items.WaitForContents(chest, 2000)` | 开箱后读取内容 |
| `Items.OpenAt(serial, x, y)` | 在指定屏幕位置打开容器 | `Items.OpenAt(chest, 100, 100)` | 固定箱子窗口位置 |
| `Items.OpenContainerAt(bag, x, y)` | 打开容器对象到指定位置 | `Items.OpenContainerAt(bag, 100, 100)` | 整理多个箱子 |
| `Items.Close(item)` | 关闭容器 | `Items.Close(chest)` | 清理界面 |
| `Items.SingleClick(item)` | 单击物品显示名称 | `Items.SingleClick(item)` | 触发名称/属性更新 |
| `Items.WaitForProps(item, delay)` | 等待属性加载 | `Items.WaitForProps(item, 1000)` | 读取装备属性前 |
| `Items.GetProperties(serial, delay)` | 获取属性列表 | `props = Items.GetProperties(item.Serial, 1000)` | 装备鉴定 |
| `Items.GetPropStringList(serial)` | 获取属性字符串列表 | `lines = Items.GetPropStringList(item)` | 查词条 |
| `Items.GetPropStringByIndex(serial, index)` | 获取第几行属性 | `name = Items.GetPropStringByIndex(item, 0)` | 读取名称行 |
| `Items.GetPropValue(serial, name)` | 按属性名取数值 | `lmc = Items.GetPropValue(item, "lower mana cost")` | 装备评分 |
| `Items.GetPropValueString(serial, name)` | 按属性名取字符串 | `v = Items.GetPropValueString(item.Serial, "durability")` | 文本属性 |
| `Items.GetImage(itemID, hue)` | 获取物品图片 | `img = Items.GetImage(0x0E21, 0)` | 工具窗体显示图标 |
| `Items.GetWeaponAbility(itemId)` | 获取武器特攻 | `a = Items.GetWeaponAbility(weapon.ItemID)` | 战斗辅助 |
| `Items.ContextExist(item, name)` | 检查右键菜单项 | `idx = Items.ContextExist(item, "Open")` | 上下文菜单操作 |
| `Items.Message(item, hue, message)` | 在物品头顶显示文字 | `Items.Message(item, 68, "目标")` | 调试标记 |
| `Items.Color(serial, color)` / `SetColor` | 本地改显示色 | `Items.SetColor(item.Serial, 33)` | 高亮物品 |
| `Items.Hide(item)` | 本地隐藏物品 | `Items.Hide(item)` | 清理视野 |
| `Items.IgnoreTypes(itemIdList)` | 忽略一组类型 | `Items.IgnoreTypes([0x0EED])` | 扫地过滤 |
| `Items.Select(items, selector)` | 从列表选一个 | `Items.Select(items, "Nearest")` | 从候选物中选最近/最优 |
| `Items.ChangeDyeingTubColor(dyes, tub, color)` | 修改染缸颜色 | `Items.ChangeDyeingTubColor(dyes, tub, 1150)` | 染色脚本 |

Items.Filter 常用属性：

| 属性 | 作用 | 示例 | 场景 |
|---|---|---|---|
| `Enabled` | 是否启用过滤 | `f.Enabled = True` | 必须开启才按条件过滤 |
| `Graphics` | 物品类型列表 | `f.Graphics.Add(0x1BF2)` | 找铁锭 |
| `Hues` | 颜色列表 | `f.Hues.Add(0)` | 找指定颜色 |
| `Serials` | 序列号列表 | `f.Serials.Add(chest)` | 指定目标 |
| `RangeMin/RangeMax` | 距离范围 | `f.RangeMax = 2` | 找身边地面物品 |
| `OnGround` | 是否地上 | `f.OnGround = 1` | 捡地面物品 |
| `IsContainer` | 是否容器 | `f.IsContainer = 1` | 找箱子/尸体 |
| `IsCorpse` | 是否尸体 | `f.IsCorpse = 1` | 自动开尸体 |
| `Movable` | 是否可移动 | `f.Movable = 1` | 捡可移动物品 |
| `Name` | 名称正则 | `f.Name = "ingot"` | 按名称找 |
| `CheckIgnoreObject` | 排除 Ignore | `f.CheckIgnoreObject = True` | 避免重复目标 |

### Mobiles 生物接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Mobiles.ApplyFilter(filter)` | 按过滤器找生物列表 | `mobs = Mobiles.ApplyFilter(f)` | 找敌人、找队友、找 NPC |
| `Mobiles.FindBySerial(serial)` | 按序列找生物 | `mob = Mobiles.FindBySerial(s)` | 固定目标 |
| `Mobiles.FindMobile(graphic, notoriety, rangemax, selector, highlight)` | 快速找生物 | `m = Mobiles.FindMobile(-1, [6], 10, "Nearest", True)` | 找最近红名 |
| `Mobiles.Select(mobiles, selector)` | 从列表选一个 | `m = Mobiles.Select(mobs, "Weakest")` | 选血少敌人/队友 |
| `Mobiles.SingleClick(mobile)` | 单击生物 | `Mobiles.SingleClick(m)` | 更新名字 |
| `Mobiles.UseMobile(mobile)` | 双击生物 | `Mobiles.UseMobile(npc)` | 打开 NPC 交互 |
| `Mobiles.Message(mobile, hue, msg, wait)` | 在生物头顶显示消息 | `Mobiles.Message(m, 33, "敌人", False)` | 调试标记 |
| `Mobiles.WaitForProps(mobile, delay)` | 等待属性 | `Mobiles.WaitForProps(m, 1000)` | 读取血量/属性 |
| `Mobiles.WaitForStats(mobile, delay)` | 等待状态 | `Mobiles.WaitForStats(m, 1000)` | 治疗前刷新血量 |
| `Mobiles.GetPropStringList(mobile)` | 获取属性文字 | `lines = Mobiles.GetPropStringList(m)` | 读取 OPL |
| `Mobiles.GetPropValue(mobile, name)` | 获取属性数值 | `v = Mobiles.GetPropValue(m, "strength")` | 判断 NPC/怪属性 |
| `Mobiles.ContextExist(mobile, name, showContext)` | 查右键菜单项 | `idx = Mobiles.ContextExist(npc, "Train", True)` | NPC 菜单 |
| `Mobiles.GetTargetingFilter(name)` | 读取 RA 目标列表过滤器 | `f = Mobiles.GetTargetingFilter("敌人")` | 使用已有目标列表 |
| `Mobiles.GetTrackingInfo()` | 获取追踪信息 | `info = Mobiles.GetTrackingInfo()` | 追踪脚本 |

Mobiles.Filter 常用属性：

| 属性 | 作用 | 示例 | 场景 |
|---|---|---|---|
| `Enabled` | 启用过滤 | `f.Enabled = True` | 必填 |
| `RangeMax` | 最大距离 | `f.RangeMax = 10` | 找附近敌人 |
| `Notorieties` | 声望列表 | `f.Notorieties.Add(6)` | 红名/灰名过滤 |
| `Serials` | 指定序列 | `f.Serials.Add(serial)` | 固定对象 |
| `Bodies/Graphics` | 身体/图形 ID | `f.Bodies.Add(0x0190)` | 找特定怪 |
| `Name` | 名称匹配 | `f.Name = "Jordan"` | 找 NPC/玩家 |
| `Friend` | 是否好友 | `f.Friend = 1` | 找队友 |
| `IsGhost` | 是否幽灵 | `f.IsGhost = 0` | 排除死人 |
| `Poisoned` | 是否中毒 | `f.Poisoned = 1` | 找中毒队友 |
| `Paralized` | 是否麻痹 | `f.Paralized = 1` | 找被控目标 |
| `Warmode` | 战斗模式 | `f.Warmode = 1` | 找战斗目标 |
| `CheckLineOfSight` | 视线检查 | `f.CheckLineOfSight = True` | 避免墙后目标 |
| `IgnorePets` | 忽略宠物 | `f.IgnorePets = True` | 战斗选怪 |

### Player 玩家接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Player.Area()` / `Zone()` | 当前区域/区域名 | `Misc.SendMessage(Player.Area())` | 地点判断 |
| `Player.Attack(serial)` | 攻击目标 | `Player.Attack(enemy)` | 战斗脚本 |
| `Player.AttackLast()` | 攻击上次目标 | `Player.AttackLast()` | 快速反击 |
| `Player.AttackType(graphics, range, selector, color, notoriety)` | 按类型攻击 | `Player.AttackType(-1, 10, "Nearest", [], [6])` | 自动攻击红名 |
| `Player.SetWarMode(flag)` | 设置战斗模式 | `Player.SetWarMode(True)` | 开战/收武器 |
| `Player.BuffsExist(name, guess)` | 判断 Buff | `Player.BuffsExist("Consecrate Weapon", True)` | 补 Buff |
| `Player.BuffTime(name)` | Buff 剩余时间 | `Player.BuffTime("Enemy Of One")` | 控制补技能时间 |
| `Player.GetBuffInfo(name, guess)` | 获取 Buff 信息 | `info = Player.GetBuffInfo("Poison", True)` | 状态脚本 |
| `Player.UseSkill(skill, target, wait)` | 使用技能，可带目标 | `Player.UseSkill("Anatomy", enemy, True)` | 技能训练 |
| `Player.UseSkillOnly(skill, wait)` | 只使用技能 | `Player.UseSkillOnly("Meditation", True)` | 冥想 |
| `Player.GetSkillValue(name)` | 显示技能值 | `Player.GetSkillValue("Blacksmithy")` | 技能脚本判断 |
| `Player.GetRealSkillValue(name)` | 真实技能值 | `Player.GetRealSkillValue("Magery")` | 忽略装备加成 |
| `Player.GetSkillCap(name)` | 技能上限 | `Player.GetSkillCap("Tactics")` | 技能训练停止 |
| `Player.SetSkillStatus(name, status)` | 锁/升/降技能 | `Player.SetSkillStatus("Focus", 0)` | 技能管理 |
| `Player.GetStatStatus(name)` | 获取属性箭头 | `Player.GetStatStatus("str")` | 属性管理 |
| `Player.SetStatStatus(name, status)` | 设置属性箭头 | `Player.SetStatStatus("dex", 1)` | 属性训练 |
| `Player.CheckLayer(layer)` | 检查装备层 | `Player.CheckLayer("RightHand")` | 判断是否拿武器 |
| `Player.GetItemOnLayer(layer)` | 获取装备 | `weapon = Player.GetItemOnLayer("RightHand")` | 耐久检查 |
| `Player.EquipItem(item)` | 装备物品 | `Player.EquipItem(weapon)` | 自动换装 |
| `Player.UnEquipItemByLayer(layer, wait)` | 脱装备 | `Player.UnEquipItemByLayer("RightHand", True)` | 自动脱武器 |
| `Player.EquipLastWeapon()` | 装备上次武器 | `Player.EquipLastWeapon()` | 战斗切换 |
| `Player.DistanceTo(target)` | 距离目标 | `Player.DistanceTo(enemy)` | 判断是否靠近 |
| `Player.InRange(entity, range)` | 判断范围 | `Player.InRange(enemy, 2)` | 近战距离 |
| `Player.PathFindTo(x, y, ...)` | 寻路到坐标 | `Player.PathFindTo(1000, 1000, 5, True)` | 自动移动 |
| `Player.Walk(direction)` | 走一步 | `Player.Walk("North")` | 精细移动 |
| `Player.Run(direction)` | 跑一步 | `Player.Run("East")` | 快速移动 |
| `Player.ToggleAlwaysRun()` | 切换始终跑步 | `Player.ToggleAlwaysRun()` | 移动设置 |
| `Player.ChatSay(color, msg)` | 普通说话 | `Player.ChatSay(68, "hello")` | 自动喊话 |
| `Player.ChatYell(color, msg)` | 大喊 | `Player.ChatYell(33, "help")` | 求救/提示 |
| `Player.ChatWhisper(color, msg)` | 悄声 | `Player.ChatWhisper(68, "hi")` | 特定脚本 |
| `Player.ChatGuild(msg)` | 公会频道 | `Player.ChatGuild("集合")` | 团队通知 |
| `Player.ChatParty(msg, serial)` | 队伍频道 | `Player.ChatParty("治疗我", 0)` | 队伍脚本 |
| `Player.ChatAlliance(msg)` | 联盟频道 | `Player.ChatAlliance("支援")` | 联盟通知 |
| `Player.HeadMessage(color, msg)` | 自己头顶消息 | `Player.HeadMessage(68, "完成")` | 状态提示 |
| `Player.MapSay(msg)` | 地图说话 | `Player.MapSay("标记")` | 地图标记 |
| `Player.OpenPaperDoll()` | 打开纸娃娃 | `Player.OpenPaperDoll()` | 装备检查 |
| `Player.GuildButton()` / `QuestButton()` | 打开按钮 | `Player.QuestButton()` | 游戏界面交互 |
| `Player.InvokeVirtue(virtue)` | 使用美德 | `Player.InvokeVirtue("Honor")` | 美德技能 |
| `Player.WeaponPrimarySA()` | 使用主武器特攻 | `Player.WeaponPrimarySA()` | 战斗 |
| `Player.WeaponSecondarySA()` | 使用副武器特攻 | `Player.WeaponSecondarySA()` | 战斗 |
| `Player.WeaponStunSA()` | 使用眩晕 | `Player.WeaponStunSA()` | PvP |
| `Player.WeaponDisarmSA()` | 使用缴械 | `Player.WeaponDisarmSA()` | PvP |
| `Player.WeaponClearSA()` | 清除特攻 | `Player.WeaponClearSA()` | 状态复位 |
| `Player.SpellIsEnabled(spell)` | 法术是否可用 | `Player.SpellIsEnabled("Heal")` | 施法前判断 |
| `Player.SumAttribute(name)` | 统计装备属性 | `Player.SumAttribute("Lower Mana Cost")` | 装备评分 |
| `Player.PartyInvite()` | 邀请队伍 | `Player.PartyInvite()` | 队伍脚本 |
| `Player.PartyAccept(serial, force)` | 接受邀请 | `Player.PartyAccept(serial, True)` | 自动入队 |
| `Player.LeaveParty(force)` | 离队 | `Player.LeaveParty(True)` | 队伍管理 |
| `Player.PartyCanLoot(flag)` | 队伍可拾取 | `Player.PartyCanLoot(True)` | 队伍权限 |
| `Player.KickMember(serial)` | 踢出队伍 | `Player.KickMember(serial)` | 队长管理 |
| `Player.ClearCorpseList()` | 清空尸体列表 | `Player.ClearCorpseList()` | 打怪拾取 |
| `Player.SetStaticMount(serial)` | 设置静态坐骑 | `Player.SetStaticMount(mount)` | 坐骑显示 |
| `Player.Fly(status)` | 飞行状态 | `Player.Fly(True)` | 特殊服务器 |
| `Player.TrackingArrow(x, y, display, target)` | 追踪箭头 | `Player.TrackingArrow(1000,1000,True,0)` | 导航 |

### Target 目标接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Target.PromptTarget(message, color)` | 弹出选择目标 | `s = Target.PromptTarget("选箱子", 68)` | 脚本绑定箱子 |
| `Target.PromptGroundTarget(message, color)` | 选择地面坐标 | `p = Target.PromptGroundTarget("选地面", 68)` | 采集/移动 |
| `Target.WaitForTarget(delay, noshow)` | 等待目标光标 | `Target.WaitForTarget(2000, False)` | 使用物品后等目标 |
| `Target.WaitForTargetOrFizzle(delay, noshow)` | 等目标或施法失败 | `Target.WaitForTargetOrFizzle(3000, False)` | 施法 |
| `Target.TargetExecute(serial)` | 对对象执行目标 | `Target.TargetExecute(enemy)` | 技能/法术目标 |
| `Target.TargetExecute(x, y, z, staticID)` | 对坐标执行目标 | `Target.TargetExecute(x,y,z,0)` | 采集地面 |
| `Target.Self()` | 目标自己 | `Target.Self()` | 自疗 |
| `Target.Last()` | 目标上次目标 | `Target.Last()` | 重复攻击/施法 |
| `Target.SetLast(serial, wait)` | 设置上次目标 | `Target.SetLast(enemy, True)` | 连续施法 |
| `Target.GetLast()` | 获取上次目标 | `s = Target.GetLast()` | 保存目标 |
| `Target.ClearLast()` | 清除上次目标 | `Target.ClearLast()` | 目标复位 |
| `Target.GetLastAttack()` | 获取上次攻击目标 | `s = Target.GetLastAttack()` | 战斗 |
| `Target.ClearLastAttack()` | 清除上次攻击 | `Target.ClearLastAttack()` | 战斗复位 |
| `Target.HasTarget(flag)` | 是否有目标光标 | `Target.HasTarget("Any")` | 防止误点 |
| `Target.Cancel()` | 取消当前目标 | `Target.Cancel()` | 异常恢复 |
| `Target.ClearQueue()` | 清空目标队列 | `Target.ClearQueue()` | 目标队列复位 |
| `Target.LastQueued()` | 队列上次目标 | `Target.LastQueued()` | 连续动作 |
| `Target.SelfQueued()` | 队列目标自己 | `Target.SelfQueued()` | 连续自疗 |
| `Target.ClearLastandQueue()` | 清上次和队列 | `Target.ClearLastandQueue()` | 脚本开始前 |
| `Target.LastUsedObject()` | 上次使用物品 | `s = Target.LastUsedObject()` | 重复工具 |
| `Target.TargetResource(item, resource)` | 使用资源目标 | `Target.TargetResource(tool, "ore")` | 挖矿/伐木 |
| `Target.TargetType(graphic, color, range, selector, notoriety)` | 按类型选目标 | `Target.TargetType(0x0190,-1,10,"Nearest",[6])` | 战斗/技能 |
| `Target.TargetExecuteRelative(mobile, offset)` | 相对目标坐标 | `Target.TargetExecuteRelative(enemy, 1)` | 地面法术 |
| `Target.GetTargetFromList(name)` | 从 RA 目标列表取目标 | `m = Target.GetTargetFromList("敌人")` | 使用已有列表 |
| `Target.PerformTargetFromList(name)` | 执行目标列表选择 | `Target.PerformTargetFromList("敌人")` | 目标宏 |
| `Target.AttackTargetFromList(name)` | 攻击目标列表对象 | `Target.AttackTargetFromList("敌人")` | 战斗 |
| `Target.SetLastTargetFromList(name)` | 设置列表对象为 last | `Target.SetLastTargetFromList("治疗目标")` | 治疗/攻击 |

### Gumps 界面接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Gumps.WaitForGump(gumpIDs, delay)` | 等待界面 | `Gumps.WaitForGump(0, 5000)` | 制作菜单、NPC 菜单 |
| `Gumps.CurrentGump()` | 当前 Gump ID | `gid = Gumps.CurrentGump()` | 后续点击 |
| `Gumps.HasGump(gumpId)` | 是否存在界面 | `Gumps.HasGump()` | 判断菜单是否打开 |
| `Gumps.IsValid(gumpId)` | Gump 是否有效 | `Gumps.IsValid(gid)` | 防止失效 |
| `Gumps.SendAction(gumpid, buttonid)` | 点击按钮 | `Gumps.SendAction(gid, 21)` | 制作上一项 |
| `Gumps.SendAdvancedAction(...)` | 发送按钮、开关、文本 | `Gumps.SendAdvancedAction(gid, 1, [2], [1], ["abc"])` | 复杂表单 |
| `Gumps.CloseGump(gumpid)` | 关闭界面 | `Gumps.CloseGump(gid)` | 清理界面 |
| `Gumps.ResetGump()` | 重置 Gump 状态 | `Gumps.ResetGump()` | 脚本开始前 |
| `Gumps.AllGumpIDs()` | 所有 Gump ID | `ids = Gumps.AllGumpIDs()` | 调试 |
| `Gumps.GetGumpText(gumpid)` | 获取文本列表 | `txt = Gumps.GetGumpText(gid)` | 判断菜单内容 |
| `Gumps.GetGumpRawText(gumpid)` | 原始文本 | `raw = Gumps.GetGumpRawText(gid)` | 调试 |
| `Gumps.GetGumpRawLayout(gumpid)` | 原始布局 | `layout = Gumps.GetGumpRawLayout(gid)` | 找按钮 ID |
| `Gumps.GetGumpRawData(gumpid)` | 原始数据 | `data = Gumps.GetGumpRawData(gid)` | 高级调试 |
| `Gumps.GetLine(gumpid, line)` | 取指定行 | `line = Gumps.GetLine(gid, 0)` | 菜单文字判断 |
| `Gumps.GetLineList(gumpid, dataOnly)` | 取行列表 | `lines = Gumps.GetLineList(gid, True)` | 菜单解析 |
| `Gumps.LastGumpGetLine(line)` | 上个 Gump 指定行 | `Gumps.LastGumpGetLine(0)` | 简单读取 |
| `Gumps.LastGumpGetLineList()` | 上个 Gump 行列表 | `lines = Gumps.LastGumpGetLineList()` | 制作菜单预览 |
| `Gumps.LastGumpRawLayout()` | 上个 Gump 布局 | `Gumps.LastGumpRawLayout()` | 找按钮 |
| `Gumps.LastGumpTextExist(text)` | 上个 Gump 是否有文本 | `Gumps.LastGumpTextExist("铁匠")` | 判断菜单类型 |
| `Gumps.LastGumpTextExistByLine(line, text)` | 指定行查文本 | `Gumps.LastGumpTextExistByLine(2, "修理")` | 精确判断 |
| `Gumps.LastGumpTile()` | 获取 tile 列表 | `tiles = Gumps.LastGumpTile()` | 高级解析 |
| `Gumps.GetResolvedStringPieces(gumpid)` | 解析文本片段 | `pieces = Gumps.GetResolvedStringPieces(gid)` | 本地化文本 |
| `Gumps.GetTextByID(gd, id)` | 从 GumpData 取文本 | `Gumps.GetTextByID(gd, 1)` | 自定义 Gump |
| `Gumps.CreateGump(movable, closable, disposable, resizeable)` | 创建自定义窗口 | `gd = Gumps.CreateGump(True, True, True, False)` | 脚本 UI |
| `Gumps.SendGump(...)` | 发送自定义 Gump | `Gumps.SendGump(gd, Player.Serial, 100, 100)` | 脚本面板 |
| `Gumps.AddPage(gd, page)` | 添加页 | `Gumps.AddPage(gd, 0)` | 自定义窗口 |
| `Gumps.AddBackground(gd,x,y,w,h,id)` | 背景 | `Gumps.AddBackground(gd,0,0,200,100,9270)` | UI |
| `Gumps.AddAlphaRegion(gd,x,y,w,h)` | 半透明区 | `Gumps.AddAlphaRegion(gd,0,0,200,100)` | UI |
| `Gumps.AddLabel(gd,x,y,hue,text)` | 标签文字 | `Gumps.AddLabel(gd,20,20,68,"状态")` | UI |
| `Gumps.AddLabelCropped(...)` | 裁剪文字 | `Gumps.AddLabelCropped(gd,10,10,100,20,68,"很长文字")` | UI |
| `Gumps.AddHtml(...)` | HTML 文本 | `Gumps.AddHtml(gd,10,10,180,80,"<basefont color=#FFFFFF>说明",False,True)` | UI |
| `Gumps.AddHtmlLocalized(...)` | 本地化 HTML | `Gumps.AddHtmlLocalized(gd,10,10,100,20,1000000,"",0)` | UI |
| `Gumps.AddButton(...)` | 按钮 | `Gumps.AddButton(gd,20,60,4005,4007,1,1,0)` | UI |
| `Gumps.AddCheck(...)` | 复选框 | `Gumps.AddCheck(gd,10,10,210,211,False,1)` | UI |
| `Gumps.AddRadio(...)` | 单选 | `Gumps.AddRadio(gd,10,30,210,211,True,2)` | UI |
| `Gumps.AddTextEntry(...)` | 输入框 | `Gumps.AddTextEntry(gd,10,50,120,20,68,1,"")` | UI |
| `Gumps.AddImage(...)` | 图片 | `Gumps.AddImage(gd,10,10,0x0EED)` | UI |
| `Gumps.AddImageTiled(...)` | 平铺图片 | `Gumps.AddImageTiled(gd,0,0,200,20,3004)` | UI |
| `Gumps.AddImageTiledButton(...)` | 平铺按钮 | `Gumps.AddImageTiledButton(gd,...)` | UI |
| `Gumps.AddItem(...)` | 物品图 | `Gumps.AddItem(gd,10,10,0x0E21)` | UI |
| `Gumps.AddTooltip(...)` | 提示 | `Gumps.AddTooltip(gd, "点击开始")` | UI |
| `Gumps.AddGroup(gd, group)` | 控件组 | `Gumps.AddGroup(gd, 1)` | 单选分组 |
| `Gumps.AddSpriteImage(...)` | 精灵裁剪图 | `Gumps.AddSpriteImage(gd,0,0,100,0,0,20,20)` | 高级 UI |

### Journal 日志接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Journal.Clear(text)` | 清空日志或移除指定文本 | `Journal.Clear()` | 动作前清日志 |
| `Journal.Search(text)` | 搜索文本 | `Journal.Search("成功")` | 判断结果 |
| `Journal.SearchByColor(text, color)` | 按颜色搜索 | `Journal.SearchByColor("警告", 33)` | 系统消息 |
| `Journal.SearchByName(text, name)` | 按发言人搜索 | `Journal.SearchByName("hello", "Jack")` | 聊天监听 |
| `Journal.SearchByType(text, type)` | 按类型搜索 | `Journal.SearchByType("攻击", "System")` | 系统消息 |
| `Journal.WaitJournal(msgs, delay)` | 等待文本出现 | `Journal.WaitJournal(["成功","失败"], 3000)` | 制作/施法结果 |
| `Journal.WaitByName(name, delay)` | 等待某人发言 | `Journal.WaitByName("Jack", 5000)` | 监听玩家 |
| `Journal.FilterText(text)` | 添加过滤文本 | `Journal.FilterText("spam")` | 减少干扰 |
| `Journal.RemoveFilterText(text)` | 移除过滤 | `Journal.RemoveFilterText("spam")` | 恢复 |
| `Journal.GetLineText(text, addname)` | 取匹配行 | `Journal.GetLineText("成功", True)` | 读取完整消息 |
| `Journal.GetTextByColor(color, addname)` | 按颜色取日志 | `Journal.GetTextByColor(33, True)` | 警告消息 |
| `Journal.GetTextByName(name, addname)` | 按名称取日志 | `Journal.GetTextByName("Jack", True)` | 聊天记录 |
| `Journal.GetTextBySerial(serial, addname)` | 按序列取日志 | `Journal.GetTextBySerial(Player.Serial, True)` | 自己消息 |
| `Journal.GetTextByType(type, addname)` | 按类型取日志 | `Journal.GetTextByType("System", True)` | 系统消息 |
| `Journal.GetSpeechName()` | 获取发言者列表 | `names = Journal.GetSpeechName()` | 聊天监听 |
| `Journal.GetJournalEntry(after)` | 获取日志对象 | `entries = Journal.GetJournalEntry(0)` | 高级记录 |

### Misc 杂项接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Misc.SendMessage(msg, color, wait)` | RA 输出消息 | `Misc.SendMessage("开始", 68)` | 调试/提示 |
| `Misc.Pause(millisec)` | 暂停毫秒 | `Misc.Pause(800)` | 等待服务器 |
| `Misc.Beep()` | 系统提示音 | `Misc.Beep()` | 异常提醒 |
| `Misc.Resync()` | 重新同步 | `Misc.Resync()` | 卡顿恢复 |
| `Misc.Disconnect()` | 断开连接 | `Misc.Disconnect()` | 危险保护 |
| `Misc.FocusUOWindow()` | 聚焦游戏窗口 | `Misc.FocusUOWindow()` | 鼠标键盘脚本 |
| `Misc.CaptureNow()` | 截图 | `path = Misc.CaptureNow()` | 记录异常 |
| `Misc.RazorDirectory()` | RA 目录 | `Misc.RazorDirectory()` | 文件路径 |
| `Misc.ConfigDirectory()` | 配置目录 | `Misc.ConfigDirectory()` | 保存配置 |
| `Misc.DataDirectory()` | 数据目录 | `Misc.DataDirectory()` | 读写数据 |
| `Misc.ScriptDirectory()` | 脚本目录 | `Misc.ScriptDirectory()` | 找同目录配置 |
| `Misc.CurrentScriptDirectory()` | 当前脚本目录 | `Misc.CurrentScriptDirectory()` | 配置文件 |
| `Misc.ScriptCurrent(fullpath)` | 当前脚本名 | `Misc.ScriptCurrent(True)` | 日志记录 |
| `Misc.ScriptRun(scriptfile)` | 启动脚本 | `Misc.ScriptRun("辅助.py")` | 多脚本协作 |
| `Misc.ScriptStop(scriptfile)` | 停止脚本 | `Misc.ScriptStop("辅助.py")` | 控制脚本 |
| `Misc.ScriptStopAll(skipCurrent)` | 停止全部脚本 | `Misc.ScriptStopAll(True)` | 紧急停止 |
| `Misc.ScriptStatus(scriptfile)` | 脚本是否运行 | `Misc.ScriptStatus("辅助.py")` | 避免重复启动 |
| `Misc.ScriptSuspend(scriptfile)` | 暂停脚本 | `Misc.ScriptSuspend("辅助.py")` | 临时暂停 |
| `Misc.ScriptResume(scriptfile)` | 恢复脚本 | `Misc.ScriptResume("辅助.py")` | 恢复 |
| `Misc.ScriptIsSuspended(scriptfile)` | 是否暂停 | `Misc.ScriptIsSuspended("辅助.py")` | 状态判断 |
| `Misc.SetSharedValue(name, value)` | 保存共享变量 | `Misc.SetSharedValue("box", chest)` | 多脚本共享 |
| `Misc.ReadSharedValue(name)` | 读取共享变量 | `chest = Misc.ReadSharedValue("box")` | 多脚本共享 |
| `Misc.CheckSharedValue(name)` | 是否存在共享变量 | `Misc.CheckSharedValue("box")` | 判断配置 |
| `Misc.RemoveSharedValue(name)` | 删除共享变量 | `Misc.RemoveSharedValue("box")` | 清理 |
| `Misc.AllSharedValue()` | 列出共享变量 | `Misc.AllSharedValue()` | 调试 |
| `Misc.IgnoreObject(serial)` | 忽略对象 | `Misc.IgnoreObject(item)` | 避免重复拾取 |
| `Misc.UnIgnoreObject(serial)` | 取消忽略 | `Misc.UnIgnoreObject(item)` | 恢复 |
| `Misc.CheckIgnoreObject(serial)` | 是否忽略 | `Misc.CheckIgnoreObject(item)` | 过滤 |
| `Misc.ClearIgnore()` | 清空忽略 | `Misc.ClearIgnore()` | 脚本开始 |
| `Misc.ClearDragQueue()` | 清空拖动物品队列 | `Misc.ClearDragQueue()` | 卡拖放恢复 |
| `Misc.CloseBackpack()` | 关闭背包 | `Misc.CloseBackpack()` | 清界面 |
| `Misc.CloseMenu()` | 关闭菜单 | `Misc.CloseMenu()` | 菜单恢复 |
| `Misc.HasMenu()` | 是否有菜单 | `Misc.HasMenu()` | NPC 菜单 |
| `Misc.WaitForMenu(delay)` | 等待菜单 | `Misc.WaitForMenu(2000)` | NPC 菜单 |
| `Misc.GetMenuTitle()` | 菜单标题 | `Misc.GetMenuTitle()` | 判断菜单 |
| `Misc.MenuContain(text)` | 菜单含文本 | `Misc.MenuContain("购买")` | NPC 菜单 |
| `Misc.MenuResponse(text)` | 选择菜单项 | `Misc.MenuResponse("购买")` | NPC 交互 |
| `Misc.ContextReply(serial, response)` | 回复右键菜单 | `Misc.ContextReply(npc, 1)` | 上下文菜单 |
| `Misc.WaitForContext(obj, delay, show)` | 等待右键菜单 | `ctx = Misc.WaitForContext(npc, 2000, True)` | NPC/物品菜单 |
| `Misc.UseContextMenu(serial, choice, delay)` | 使用右键菜单文字 | `Misc.UseContextMenu(npc, "Open", 2000)` | 快速交互 |
| `Misc.HasPrompt()` | 是否有输入提示 | `Misc.HasPrompt()` | NPC 输入 |
| `Misc.WaitForPrompt(delay)` | 等待输入提示 | `Misc.WaitForPrompt(2000)` | 输入文字 |
| `Misc.ResponsePrompt(text)` | 回复输入提示 | `Misc.ResponsePrompt("100")` | 输入数量 |
| `Misc.CancelPrompt()` | 取消输入 | `Misc.CancelPrompt()` | 异常恢复 |
| `Misc.ResetPrompt()` | 重置输入状态 | `Misc.ResetPrompt()` | 脚本开始 |
| `Misc.HasQueryString()` | 是否有确认框 | `Misc.HasQueryString()` | 交易/提示 |
| `Misc.WaitForQueryString(delay)` | 等待确认框 | `Misc.WaitForQueryString(3000)` | 确认 |
| `Misc.QueryStringResponse(ok, response)` | 回复确认框 | `Misc.QueryStringResponse(True, "")` | 自动确认 |
| `Misc.Distance(x1,y1,x2,y2)` | 距离 | `Misc.Distance(1,1,5,5)` | 坐标判断 |
| `Misc.DistanceSqrt(p1,p2)` | 精确距离 | `Misc.DistanceSqrt(Player.Position, item.Position)` | 距离计算 |
| `Misc.GetWindowSize()` | 游戏窗口大小 | `Misc.GetWindowSize()` | 鼠标脚本 |
| `Misc.MouseLocation()` | 鼠标位置 | `Misc.MouseLocation()` | 调试 |
| `Misc.MouseMove(x,y)` | 移动鼠标 | `Misc.MouseMove(100,100)` | UI 自动化 |
| `Misc.LeftMouseClick(x,y,clientCoords)` | 左键点击 | `Misc.LeftMouseClick(100,100,True)` | UI 自动化 |
| `Misc.RightMouseClick(x,y,clientCoords)` | 右键点击 | `Misc.RightMouseClick(100,100,True)` | UI 自动化 |
| `Misc.SendToClient(keys)` | 发送按键/命令 | `Misc.SendToClient("text")` | 高级操作 |
| `Misc.OpenPaperdoll()` | 打开纸娃娃 | `Misc.OpenPaperdoll()` | 查看装备 |
| `Misc.NextContPosition(x,y)` | 设置下个容器位置 | `Misc.NextContPosition(100,100)` | 开箱布局 |
| `Misc.GetContPosition()` | 获取容器位置 | `Misc.GetContPosition()` | UI 布局 |
| `Misc.ChangeProfile(name)` | 切换 RA 配置 | `Misc.ChangeProfile("default")` | 多角色配置 |
| `Misc.ShardName()` | 当前服务器名 | `Misc.ShardName()` | 多服务器脚本 |
| `Misc.IsItem(serial)` | 是否物品 | `Misc.IsItem(s)` | 目标判断 |
| `Misc.IsMobile(serial)` | 是否生物 | `Misc.IsMobile(s)` | 目标判断 |
| `Misc.GetMapInfo(serial)` | 地图信息 | `Misc.GetMapInfo(mapItem)` | 地图脚本 |
| `Misc.PetRename(serial, name)` | 宠物改名 | `Misc.PetRename(pet, "小白")` | 宠物管理 |
| `Misc.PlaySound(sound,x,y,z)` | 播放声音 | `Misc.PlaySound(0x005B, Player.Position.X, Player.Position.Y, Player.Position.Z)` | 提醒 |
| `Misc.AppendToFile(file,line)` | 追加文件 | `Misc.AppendToFile("log.txt", "hello")` | 记录日志 |
| `Misc.AppendNotDupToFile(file,line)` | 不重复追加 | `Misc.AppendNotDupToFile("ids.txt", str(serial))` | 记录目标 |
| `Misc.RemoveLineInFile(file,line)` | 删除文件行 | `Misc.RemoveLineInFile("ids.txt", str(serial))` | 清配置 |
| `Misc.DeleteFile(file)` | 删除文件 | `Misc.DeleteFile("tmp.txt")` | 清理 |
| `Misc.ExportPythonAPI(path, pretty)` | 导出 API | `Misc.ExportPythonAPI("api.json", True)` | 文档生成 |
| `Misc.FilterSeason(enable, flag)` | 季节过滤 | `Misc.FilterSeason(True, 0)` | 画面调整 |
| `Misc.NoRunStealthStatus()` | 潜行禁止跑状态 | `Misc.NoRunStealthStatus()` | 潜行脚本 |
| `Misc.NoRunStealthToggle(enable)` | 潜行禁止跑开关 | `Misc.NoRunStealthToggle(True)` | 潜行脚本 |
| `Misc.LastHotKey()` | 上次热键 | `hk = Misc.LastHotKey()` | 热键脚本 |
| `Misc.Inspect()` | 检查对象 | `Misc.Inspect()` | 调试 |
| `Misc.NoOperation()` | 空操作 | `Misc.NoOperation()` | 占位 |

### Spells 法术接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Spells.Cast(name, target, wait, waitAfter)` | 通用施法 | `Spells.Cast("Heal", Player.Serial, True, 1000)` | 简单施法 |
| `Spells.CastMagery(name, target, wait, waitAfter)` | 魔法技能 | `Spells.CastMagery("Greater Heal", Player.Serial, True, 1000)` | 治疗 |
| `Spells.CastChivalry(name, target, wait, waitAfter)` | 骑士道 | `Spells.CastChivalry("Consecrate Weapon", Player.Serial)` | 战士 Buff |
| `Spells.CastNecro(name, target, wait, waitAfter)` | 死灵 | `Spells.CastNecro("Curse Weapon", Player.Serial)` | 战斗 |
| `Spells.CastBushido(name, wait, waitAfter)` | 武士道 | `Spells.CastBushido("Confidence", True, 1000)` | 战斗恢复 |
| `Spells.CastNinjitsu(name, target, wait, waitAfter)` | 忍术 | `Spells.CastNinjitsu("Animal Form", Player.Serial)` | 变身 |
| `Spells.CastSpellweaving(name, target, wait, waitAfter)` | 编织 | `Spells.CastSpellweaving("Gift of Renewal", Player.Serial)` | Buff |
| `Spells.CastMysticism(name, target, wait, waitAfter)` | 神秘术 | `Spells.CastMysticism("Healing Stone", Player.Serial)` | 辅助 |
| `Spells.CastMastery(name, target, wait, waitAfter)` | 精通 | `Spells.CastMastery("Onslaught", enemy)` | 战斗 |
| `Spells.CastCleric(name, target, wait, waitAfter)` | Cleric 扩展 | `Spells.CastCleric("Heal", Player.Serial)` | 自定义服 |
| `Spells.CastDruid(name, target, wait, waitAfter)` | Druid 扩展 | `Spells.CastDruid("Leaf Whirlwind", enemy)` | 自定义服 |
| `Spells.CastLastSpell(target, wait)` | 重复上次法术 | `Spells.CastLastSpell(enemy, True)` | 连续攻击 |
| `Spells.CastLastSpellLastTarget()` | 上次法术打上次目标 | `Spells.CastLastSpellLastTarget()` | 战斗循环 |
| `Spells.Interrupt()` | 打断施法 | `Spells.Interrupt()` | 危险中断 |
| `Spells.ExportSpellsToJson()` | 导出法术 | `Spells.ExportSpellsToJson()` | 文档/调试 |

### Timer 定时器接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Timer.Create(name, delay, message)` | 创建定时器 | `Timer.Create("pot", 10000)` | 药水冷却 |
| `Timer.Check(name)` | 定时器是否结束 | `if not Timer.Check("pot"):` | 防止重复动作 |
| `Timer.Remaining(name)` | 剩余毫秒 | `left = Timer.Remaining("pot")` | 显示冷却 |

### Statics 地图静态接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `Statics.GetLandID(x,y,map)` | 地面 ID | `id = Statics.GetLandID(x,y,Player.Map)` | 判断地形 |
| `Statics.GetLandZ(x,y,map)` | 地面 Z | `z = Statics.GetLandZ(x,y,Player.Map)` | 坐标目标 |
| `Statics.GetLandName(id)` | 地面名称 | `Statics.GetLandName(id)` | 调试 |
| `Statics.GetLandFlag(id, flag)` | 地面 flag | `Statics.GetLandFlag(id, "Wet")` | 判断水面 |
| `Statics.GetStaticsTileInfo(x,y,map)` | 坐标静态列表 | `tiles = Statics.GetStaticsTileInfo(x,y,Player.Map)` | 采集/找门 |
| `Statics.GetStaticsLandInfo(x,y,map)` | 坐标地面信息 | `info = Statics.GetStaticsLandInfo(x,y,Player.Map)` | 地图判断 |
| `Statics.GetItemData(staticID)` | 物品静态数据 | `data = Statics.GetItemData(0x0E21)` | 物品资料 |
| `Statics.GetTileName(id)` | tile 名称 | `Statics.GetTileName(0x0E21)` | 调试 |
| `Statics.GetTileHeight(id)` | tile 高度 | `Statics.GetTileHeight(0x0E21)` | 走路/放置 |
| `Statics.GetTileFlag(id, flag)` | tile flag | `Statics.GetTileFlag(0x0E21, "Container")` | 类型判断 |
| `Statics.CheckDeedHouse(x,y)` | 检查房屋 deed | `Statics.CheckDeedHouse(x,y)` | 房屋相关 |

### PathFinding 寻路接口

| 方法 | 作用 | 示例 | 常见场景 |
|---|---|---|---|
| `PathFinding.Route()` | 路径配置对象 | `r = PathFinding.Route()` | 高级寻路 |
| `PathFinding.Go(route)` | 按 Route 走 | `PathFinding.Go(r)` | 自动移动 |
| `PathFinding.PathFindTo(x,y,z)` | 寻路到点 | `PathFinding.PathFindTo(1000,1000,0)` | 走到目标 |
| `PathFinding.GetPath(x,y,ignoremob)` | 获取路径点 | `path = PathFinding.GetPath(1000,1000,True)` | 自定义行走 |
| `PathFinding.RunPath(path, timeout, debug, resync)` | 跑完整路径 | `PathFinding.RunPath(path, 20, False, True)` | 自动跑图 |
| `PathFinding.WalkPath(path, timeout, debug, resync)` | 走完整路径 | `PathFinding.WalkPath(path, 20, False, True)` | 潜行/慢走 |
| `PathFinding.Tile(x,y)` | 构造路径点 | `t = PathFinding.Tile(1000,1000)` | 手动路线 |

Route 常用属性：`X`、`Y`、`Run`、`MaxRetry`、`StopIfStuck`、`IgnoreMobile`、`UseResync`、`Timeout`、`DebugMessage`。

### 代理/列表接口

| 类 | 方法 | 作用 | 示例 | 场景 |
|---|---|---|---|---|
| `Organizer` | `ChangeList(name)` | 切换整理列表 | `Organizer.ChangeList("矿石")` | 整理物品 |
| `Organizer` | `RunOnce(name, source, dest, delay)` | 执行一次整理 | `Organizer.RunOnce("矿石", src, dst, 600)` | 箱子整理 |
| `Organizer` | `FStart()` / `FStop()` / `Status()` | 启停/状态 | `Organizer.FStart()` | 持续整理 |
| `Restock` | `ChangeList(name)` | 切换补货列表 | `Restock.ChangeList("战士")` | 补药 |
| `Restock` | `RunOnce(name, source, dest, delay)` | 执行一次补货 | `Restock.RunOnce("战士", chest, Player.Backpack.Serial, 600)` | 从箱子补包 |
| `Restock` | `FStart()` / `FStop()` / `Status()` | 启停/状态 | `Restock.Status()` | 自动补货 |
| `Scavenger` | `ChangeList(name)` | 切换拾取列表 | `Scavenger.ChangeList("矿石")` | 自动拾取 |
| `Scavenger` | `Start()` / `Stop()` / `Status()` | 启停/状态 | `Scavenger.Start()` | 地面拾取 |
| `Scavenger` | `RunOnce(list, millisec, filter)` | 执行一次拾取 | `Scavenger.RunOnce(lst, 600, f)` | 指定过滤拾取 |
| `Scavenger` | `GetScavengerBag()` | 获取拾取包 | `bag = Scavenger.GetScavengerBag()` | 调试 |
| `Scavenger` | `ResetIgnore()` | 重置忽略 | `Scavenger.ResetIgnore()` | 重新扫描 |
| `AutoLoot` | `ChangeList(name)` | 切换拾尸列表 | `AutoLoot.ChangeList("默认")` | 打怪拾取 |
| `AutoLoot` | `Start()` / `Stop()` / `Status()` | 启停/状态 | `AutoLoot.Start()` | 自动拾尸 |
| `AutoLoot` | `RunOnce(name, millisec, filter)` | 执行一次拾尸 | `AutoLoot.RunOnce("默认", 800, f)` | 单次清尸 |
| `AutoLoot` | `GetList(name, wantMinusOnes)` | 获取列表 | `lst = AutoLoot.GetList("默认", False)` | 高级自定义 |
| `AutoLoot` | `GetLootBag()` | 获取拾取包 | `bag = AutoLoot.GetLootBag()` | 调试 |
| `AutoLoot` | `SetNoOpenCorpse(noOpen)` | 是否不开尸体 | `AutoLoot.SetNoOpenCorpse(True)` | 只捡已开尸体 |
| `AutoLoot` | `ResetIgnore()` | 清忽略 | `AutoLoot.ResetIgnore()` | 重新清尸 |
| `Dress` | `ChangeList(name)` | 切换换装列表 | `Dress.ChangeList("战斗")` | 换装 |
| `Dress` | `DressFStart()` / `DressFStop()` / `DressStatus()` | 穿装启停/状态 | `Dress.DressFStart()` | 一键穿装 |
| `Dress` | `UnDressFStart()` / `UnDressFStop()` / `UnDressStatus()` | 脱装启停/状态 | `Dress.UnDressFStart()` | 一键脱装 |
| `BandageHeal` | `Start()` / `Stop()` / `Status()` | 绷带治疗代理 | `BandageHeal.Start()` | 使用 RA 内置治疗 |
| `Friend` | `ChangeList(name)` | 切换好友列表 | `Friend.ChangeList("队友")` | 治疗/过滤 |
| `Friend` | `AddFriendTarget()` | 目标添加好友 | `Friend.AddFriendTarget()` | 建立列表 |
| `Friend` | `AddPlayer(list,name,serial)` | 添加玩家 | `Friend.AddPlayer("队友","Jack",serial)` | 脚本维护列表 |
| `Friend` | `GetList(name)` | 获取好友序列 | `ids = Friend.GetList("队友")` | 队友过滤 |
| `Friend` | `IsFriend(serial)` | 是否好友 | `Friend.IsFriend(m.Serial)` | 不攻击队友 |
| `Friend` | `RemoveFriend(list, serial)` | 删除好友 | `Friend.RemoveFriend("队友", serial)` | 列表维护 |
| `DPSMeter` | `Start()` / `Pause()` / `Stop()` / `Status()` | DPS 统计启停 | `DPSMeter.Start()` | 输出统计 |
| `DPSMeter` | `GetDamage(serial)` | 获取伤害 | `DPSMeter.GetDamage(enemy)` | 伤害统计 |

### Vendor 商人接口

| 方法 | 作用 | 示例 | 场景 |
|---|---|---|---|
| `Vendor.Buy(vendorSerial, itemID, amount, maxPrice)` | 自动购买 | `Vendor.Buy(vendor.Serial, 0x0E21, 100, 5)` | 买绷带/材料 |
| `Vendor.BuyList(vendorSerial)` | 获取购买列表 | `lst = Vendor.BuyList(vendor.Serial)` | 查看 NPC 售卖 |
| `Vendor.LastBuyList` | 上次购买列表 | `Vendor.LastBuyList` | 调试 |
| `Vendor.LastSellList` | 上次出售列表 | `Vendor.LastSellList` | 调试 |
| `Vendor.LastVendor` | 上次商人 | `Vendor.LastVendor` | 连续交易 |

## 五、完整示例

### 示例 1：自动绷带自己

```python
# -*- coding: utf-8 -*-
BANDAGE = 0x0E21

def main():
    while not Player.IsGhost:
        if Player.Hits < Player.HitsMax:
            bandage = Items.FindByID(BANDAGE, -1, Player.Backpack.Serial, True)
            if bandage:
                Items.UseItem(bandage.Serial)
                if Target.WaitForTarget(2000, False):
                    Target.Self()
                    Misc.SendMessage("使用绷带", 68)
                    Misc.Pause(11000)
            else:
                Misc.SendMessage("没有绷带", 33)
                Misc.Pause(3000)
        Misc.Pause(500)

if __name__ == "__main__":
    main()
```

### 示例 2：自动丢弃指定成品

```python
TRASH = 0x40012345
DUMP_IDS = [0x13EE, 0x1413]

def each_container_item(container_serial, recursive=True):
    bag = Items.FindBySerial(container_serial)
    if bag is None:
        return []

    result = []
    for item in bag.Contains:
        result.append(item)
        if recursive and item.IsContainer:
            result.extend(each_container_item(item.Serial, True))
    return result

for item in each_container_item(Player.Backpack.Serial):
    if item.ItemID in DUMP_IDS:
        Items.Move(item.Serial, TRASH, item.Amount)
        Misc.Pause(600)
```

### 示例 3：找最近敌人并攻击

```python
def find_enemy():
    f = Mobiles.Filter()
    f.Enabled = True
    f.RangeMax = 10
    f.Notorieties.Add(3)
    f.Notorieties.Add(4)
    f.Notorieties.Add(5)
    f.Notorieties.Add(6)
    f.CheckLineOfSight = True
    return Mobiles.Select(Mobiles.ApplyFilter(f), "Nearest")

enemy = find_enemy()
if enemy:
    Player.SetWarMode(True)
    Player.Attack(enemy)
```

### 示例 4：制作菜单点击上一项

```python
TOOL = 0x13E3
MAKE_LAST = 21

tool = Items.FindByID(TOOL, -1, Player.Backpack.Serial, True)
if tool:
    Items.UseItem(tool.Serial)
    if Gumps.WaitForGump(0, 5000):
        gid = Gumps.CurrentGump()
        Gumps.SendAction(gid, MAKE_LAST)
        Misc.Pause(800)
```

## 六、排错清单

### 1. `object has no attribute`

原因：方法名不存在或版本不支持。

错误例子：

```python
Items.GetItems(Player.Backpack.Serial)
```

修法：

```python
bag = Items.FindBySerial(Player.Backpack.Serial)
for item in bag.Contains:
    Misc.SendMessage(item.Name)
```

### 2. 找不到物品

检查：

- 容器是否打开。
- 物品颜色是否写错，颜色不确定用 `-1`。
- 是否需要递归 `True`。
- 物品是否在子包里。

### 3. Gump 按钮没反应

检查：

- `Gumps.WaitForGump(0, 5000)` 是否成功。
- `Gumps.CurrentGump()` 是否取到正确 ID。
- 按钮 ID 是否是当前服务器菜单的按钮。
- 动作后是否加了 `Misc.Pause(500-1000)`。

### 4. 移动物品失败

检查：

- 目标容器是否打开或可达。
- `amount` 是否大于物品数量。
- 角色是否超重。
- 服务端是否限制距离。

### 5. 中文乱码

脚本第一行加：

```python
# -*- coding: utf-8 -*-
```

编辑器保存为 UTF-8。

## 七、推荐脚本写法习惯

1. 所有常量写在脚本开头。
2. 所有序列号用 `0xXXXXXXXX` 格式。
3. 每个动作后加 `Misc.Pause()`。
4. 写 `log()` 函数统一输出。
5. 用 `Target.PromptTarget()` 让玩家选择箱子，少写死序列号。
6. 循环条件一定包含 `while not Player.IsGhost:`。
7. 查找物品前打开容器并等待。
8. 读取 Gump 前先 `Gumps.WaitForGump()`。
9. 脚本报错先看最后一行：通常就是不存在的方法、空对象或参数错。
10. 不确定对象是否存在时先判断 `if item is not None:`。

