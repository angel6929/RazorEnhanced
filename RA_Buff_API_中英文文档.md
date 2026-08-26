# Razor Enhanced 人物 Buff 状态 API 文档

本文档对应当前 Razor Enhanced 源码中的人物 Buff/Debuff 状态接口，适用于 Python、C# 等 Enhanced Script。文中的“Buff”同时包含增益、减益和人物状态。

## 1. 数据范围与工作原理

RA 监听服务器发送的 `0xDF` Buff/Debuff 数据包，并将当前人物的状态保存在 `World.Player.Buffs` 中。公开脚本接口位于 `RazorEnhanced.Player`。

当前限制：

- 只能读取当前人物 `Player` 的 Buff，不能读取任意 `Mobile` 的 Buff。
- Buff 必须先由服务器通过 `0xDF` 数据包发送，RA 才能识别。
- API 使用源码定义的英文名；中文名只用于界面显示和本文档对照，不能直接作为 API 参数。
- 尚未进入游戏、`World.Player` 不存在时，不应读取 `Player.Buffs` 或 `Player.BuffsInfo`。

## 2. API 快速参考

| API | 返回类型 | 说明 |
|---|---|---|
| `Player.Buffs` | `List[str]` | 当前人物所有活动 Buff 的英文名称 |
| `Player.BuffsInfo` | `List[BuffInfo]` | 当前人物所有活动 Buff 的详细信息 |
| `Player.BuffsExist(name, okayToGuess=True)` | `bool` | 判断指定 Buff 是否存在 |
| `Player.GetBuffInfo(name, okayToGuess=True)` | `BuffInfo` 或 `None` | 获取指定 Buff 的详细信息 |
| `Player.BuffTime(name)` | `int` | 获取指定 Buff 剩余时间，单位毫秒；找不到返回 `0` |

### 2.1 `Player.Buffs`

返回当前人物所有活动 Buff 的英文名列表。

```python
for name in Player.Buffs:
    Misc.SendMessage(name)
```

### 2.2 `Player.BuffsExist`

```python
if Player.BuffsExist("Poison"):
    Misc.SendMessage("人物处于中毒状态")

# 禁用近似名称猜测
if Player.BuffsExist("Mortal Strike", False):
    Misc.SendMessage("人物受到致命一击效果")
```

参数 `okayToGuess` 默认为 `True`。精确匹配失败后，RA 会尝试猜测最接近的英文 Buff 名称。关键自动化脚本建议传入 `False`，避免拼写错误匹配到错误状态。

### 2.3 `Player.GetBuffInfo`

```python
info = Player.GetBuffInfo("Poison", False)
if info is not None:
    Misc.SendMessage("名称: " + info.Name)
    Misc.SendMessage("说明: " + (info.Description or ""))
    Misc.SendMessage("剩余毫秒: " + str(max(0, info.Remaining)))
```

该方法也接受精确的源码枚举名，例如：

```python
info = Player.GetBuffInfo("MortalStrike", False)
```

枚举名匹配区分大小写，普通英文名称匹配不区分大小写。

### 2.4 `Player.BuffTime`

```python
remaining_ms = Player.BuffTime("Enemy Of One")
remaining_seconds = max(0, remaining_ms) // 1000
```

`BuffTime` 只按下表中的英文 API 名精确匹配，不执行枚举名解析或近似猜测。不存在时和剩余时间为零时都会返回 `0`。

### 2.5 `Player.BuffsInfo`

```python
for buff in Player.BuffsInfo:
    Misc.SendMessage(
        "{} | 图标={} | 剩余={}ms".format(
            buff.Name, buff.Icon, max(0, buff.Remaining)
        )
    )
```

## 3. `BuffInfo` 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `Name` | `str` | RA 映射后的英文 Buff 名称 |
| `Icon` | `int` | 服务器 Buff 图标编号 |
| `StartTime` | `long` | 开始时间的 .NET `DateTime.Ticks`，不是 Unix 时间戳 |
| `Duration` | `int` | 总持续时间，毫秒 |
| `Remaining` | `int` | 剩余时间，毫秒；过期后可能为负数 |
| `Elapsed` | `int` | 已经过的时间，毫秒 |
| `HasExpired` | `bool` | 有限时长 Buff 是否已经过期 |
| `Title` | `str` | 解析 Cliloc 后的标题，可能为空 |
| `Description` | `str` | 解析 Cliloc 后的说明，可能为空 |
| `ExtraInfo` | `str` | 解析 Cliloc 后的附加信息，可能为空 |
| `TitleCliloc` | `uint` | 标题 Cliloc 编号 |
| `TitleArgs` | `List[str]` | 标题 Cliloc 参数 |
| `DescriptionCliloc` | `uint` | 说明 Cliloc 编号 |
| `DescriptionArgs` | `List[str]` | 说明 Cliloc 参数 |
| `ExtraInfoCliloc` | `uint` | 附加信息 Cliloc 编号 |
| `ExtraInfoArgs` | `List[str]` | 附加信息 Cliloc 参数 |

持续时间为 `0` 的状态通常表示服务器没有提供倒计时。此时不要依靠 `Remaining` 判断状态是否存在，应使用 `Player.BuffsExist`。

## 4. 常用脚本模板

### 等待 Buff 出现

```python
timeout = 10000
elapsed = 0

while not Player.BuffsExist("Hiding", False) and elapsed < timeout:
    Misc.Pause(100)
    elapsed += 100

if Player.BuffsExist("Hiding", False):
    Misc.SendMessage("隐藏状态已生效")
else:
    Misc.SendMessage("等待隐藏状态超时")
```

### 等待 Buff 消失

```python
while Player.BuffsExist("Skill Use Delay", False):
    Misc.Pause(100)
```

### 根据剩余时间补状态

```python
buff_name = "Consecrate Weapon"
info = Player.GetBuffInfo(buff_name, False)

if info is None or (info.Duration > 0 and info.Remaining < 1500):
    Spells.CastChivalry("Consecrate Weapon")
```

### 输出当前全部状态

```python
for buff in Player.BuffsInfo:
    seconds = max(0, buff.Remaining) / 1000.0
    Misc.SendMessage(
        "{} | {} | {:.1f}s".format(buff.Name, buff.Title or "", seconds)
    )
```

## 5. Buff 中英文及枚举名完整对照

调用 `Player.BuffsExist`、`Player.GetBuffInfo` 和 `Player.BuffTime` 时，优先使用“英文 API 名”列。表中的历史拼写错误来自 RA 源码，为保证匹配必须原样保留，例如 `Bload`、`Enpowerment`、`Hawl`、`debuf` 以及 `Urali Trance Tonic,` 末尾的逗号。

| 源码枚举名 | 英文 API 名 | 中文名称 |
|---|---|---|
| `ActiveMeditation` | `Meditation` | 冥想 |
| `Agility` | `Agility` | 敏捷术 |
| `AnimalForm` | `Animal Form` | 动物形态 |
| `AnticipateHit` | `Anticipate Hit` | 预判攻击 |
| `ArcaneEmpowerment` | `Arcane Enpowerment` | 奥术强化 |
| `ArcaneEmpowermentNew` | `Arcane Enpowerment (new)` | 奥术强化（新版） |
| `ArchProtection` | `Arch Protection` | 群体防护 |
| `ArmorPierce` | `Armor Pierce` | 穿甲 |
| `AttuneWeapon` | `Attunement` | 武器调谐 |
| `AuraOfNausea` | `Aura of Nausea` | 恶心光环 |
| `BarakoDraftOfMight` | `Barako Draft Of Might` | 巴拉科力量药剂 |
| `BarrabHemolymphConcentrate` | `Barrab Hemolymph Concentrate` | 巴拉布血淋巴浓缩液 |
| `Bleed` | `Bleed` | 流血 |
| `Bless` | `Bless` | 祝福 |
| `Block` | `Block` | 格挡 |
| `BloodOathCaster` | `Bload Oath (caster)` | 血誓（施法者） |
| `BloodOathCurse` | `Bload Oath (curse)` | 血誓（受术者） |
| `BloodwormAnemia` | `BloodWorm Anemia` | 血虫贫血症 |
| `Boarding` | `Boarding` | 登船作战 |
| `Bodyguard` | `Bodyguard` | 贴身护卫 |
| `CalledShot` | `Called Shot` | 精准射击 |
| `CityTradeDeal` | `City Trade Deal` | 城市贸易协定 |
| `Clumsy` | `Clumsy` | 笨拙 |
| `CombatTraining` | `Combat Training` | 战斗训练 |
| `Conduit` | `Conduit` | 导流 |
| `Confidence` | `Confidence` | 自信 |
| `ConsecrateWeapon` | `Consecrate Weapon` | 神圣武器 |
| `CorpseSkin` | `Corpse Skin` | 尸皮术 |
| `CounterAttack` | `Counter Attack` | 反击 |
| `CriminalStatus` | `Criminal` | 犯罪状态 |
| `Cunning` | `Cunning` | 聪慧术 |
| `Curse` | `Curse` | 诅咒 |
| `CurseWeapon` | `Curse Weapon` | 诅咒武器 |
| `DeathRay` | `Death Ray` | 死亡射线 |
| `DeathRayDebuff` | `Death Ray Debuff` | 死亡射线（减益） |
| `DeathStrike` | `Death Strike` | 死亡一击 |
| `DefenseMastery` | `Defense Mastery` | 防御精通 |
| `Despair` | `Despair` | 绝望 |
| `DespairTarget` | `Despair (target)` | 绝望（目标） |
| `DisarmNew` | `Disarm (new)` | 缴械（新版） |
| `Disguised` | `Disguised` | 伪装 |
| `DismountPrevention` | `Dismount Prevention` | 禁止骑乘 |
| `DivineFury` | `Divine Fury` | 神圣狂怒 |
| `DragonSlasherFear` | `Dragon Slasher Fear` | 屠龙者恐惧 |
| `DragonTurtleDebuff` | `Dragon Turtle Debuff` | 龙龟减益 |
| `ElementalFury` | `Elemental Fury` | 元素狂怒 |
| `ElementalFuryDebuff` | `Elemental Fury Debuff` | 元素狂怒（减益） |
| `Enchant` | `Enchant` | 附魔 |
| `EnchantedSummoning` | `Enchanted Summoning` | 强化召唤 |
| `EnemyOfOne` | `Enemy Of One` | 唯一之敌 |
| `EnemyOfOneDebuf` | `Enemy Of One (debuf)` | 唯一之敌（减益） |
| `EssenceOfWind` | `Essence Of Wind` | 风之精华 |
| `EtherealBurst` | `Ethereal Burst` | 灵体爆发 |
| `EtherealVoyage` | `Ethereal Voyage` | 灵体旅程 |
| `Evasion` | `Evasion` | 闪避 |
| `EvilOmen` | `Evil Omen` | 邪恶预兆 |
| `FactionLoss` | `Faction Loss` | 阵营损失 |
| `FanDancerFanFire` | `Fan Dancer Fan Fire` | 扇舞者扇火 |
| `FeebleMind` | `Feeble Mind` | 弱智术 |
| `Feint` | `Feint` | 佯攻 |
| `FistsOfFury` | `Fists Of Fury` | 狂怒之拳 |
| `FocusedEye` | `Focused Eye` | 专注之眼 |
| `ForceArrow` | `Force Arrow` | 强力箭 |
| `GargoyleBerserk` | `Berserk` | 狂暴 |
| `GargoyleFly` | `Fly` | 飞行 |
| `GazeDespair` | `Gaze Despair` | 绝望凝视 |
| `GiftOfLife` | `Gift Of Life` | 生命恩赐 |
| `GiftOfRenewal` | `Gift Of Renewal` | 复苏恩赐 |
| `GrapesOfWrath` | `Grapes of Wrath` | 愤怒葡萄 |
| `HealingSkill` | `Healing` | 治疗 |
| `HeatOfBattleStatus` | `Heat Of Battle` | 战斗热潮 |
| `HeightenedSenses` | `Heightened Senses` | 感官强化 |
| `HidingAndOrStealth` | `Hiding` | 隐藏 |
| `HiryuPhysicalResistance` | `Hiryu Physical Malus` | 飞龙物理抗性削弱 |
| `HitDualwield` | `Hit Dual Wield` | 双持命中 |
| `HitLowerAttack` | `Hit Lower Attack` | 降低攻击命中 |
| `HitLowerDefense` | `Hit Lower Defense` | 降低防御命中 |
| `HonorableExecution` | `Honorable Execution` | 荣耀处决 |
| `Honored` | `Honored` | 受尊敬 |
| `HorrificBeast` | `Horrific Beast` | 恐怖野兽 |
| `HowlOfCacophony` | `Hawl Of Cacophony` | 嘈杂怒嚎 |
| `ImmolatingWeapon` | `Immolating Weapon` | 献祭武器 |
| `Incognito` | `Incognito` | 易容术 |
| `InjectedStrike` | `Injected Strike` | 注毒打击 |
| `InjectedStrikeDebuff` | `Injected Strike Debuff` | 注毒打击（减益） |
| `Inspire` | `Inspire` | 激励 |
| `Intuition` | `Intuition` | 直觉 |
| `Invigorate` | `Invigorate` | 振奋 |
| `Invisibility` | `Invisibility` | 隐形术 |
| `JukariBurnPoiltice` | `Jukari Burn Poiltice` | 朱卡里灼伤药膏 |
| `Knockout` | `Knockout` | 击倒 |
| `KurakAmbushersEssence` | `Kurak Ambushers Essence` | 库拉克伏击者精华 |
| `LichForm` | `Lich Form` | 巫妖形态 |
| `LightningStrike` | `Lightning Strike` | 雷霆一击 |
| `MagicFish` | `Magic Fish` | 魔法鱼效果 |
| `MagicReflection` | `Magic Reflection` | 魔法反射 |
| `ManaPhase` | `Mana Phase` | 法力相位 |
| `ManaShield` | `Mana Shield` | 法力护盾 |
| `MassCurse` | `Mass Curse` | 群体诅咒 |
| `MedusaStone` | `Medusa Stone` | 美杜莎石化 |
| `Mindrot` | `Mind Rot` | 心智腐化 |
| `MomentumStrike` | `Momentum Strike` | 动量打击 |
| `MortalStrike` | `Mortal Strike` | 致命一击 |
| `MysticWeapon` | `Mystic Weapon` | 秘法武器 |
| `NightSight` | `Night Sight` | 夜视术 |
| `NoRearm` | `NoRearm` | 禁止重新装备 |
| `Onslaught` | `Onslaught` | 猛攻 |
| `OrangePetals` | `Orange Petals` | 橙色花瓣 |
| `PainSpike` | `Pain Spike` | 痛苦尖刺 |
| `Paralyze` | `Paralyze` | 麻痹 |
| `Perfection` | `Perfection` | 完美 |
| `Perseverance` | `Perseverance` | 坚毅 |
| `Pierce` | `Pierce` | 穿刺 |
| `PlayingTheOdds` | `Playing The Odds` | 孤注一掷 |
| `PlayingTheOddsDebuff` | `Playing The Odds Debuff` | 孤注一掷（减益） |
| `Poison` | `Poison` | 中毒 |
| `PoisonResistanceImmunity` | `Poison Resistance` | 毒素免疫 |
| `Polymorph` | `Polymorph` | 变形术 |
| `Potency` | `Potency` | 效能 |
| `Protection` | `Protection` | 防护术 |
| `PsychicAttack` | `Psychic Attack` | 心灵攻击 |
| `Rage` | `Rage` | 狂怒 |
| `RageFocusing` | `Rage Focusing` | 聚焦狂怒 |
| `RageFocusingTarget` | `Rage Focusing (target)` | 聚焦狂怒（目标） |
| `Rampage` | `Rampage` | 横冲直撞 |
| `ReactiveArmor` | `Reactive Armor` | 反应护甲 |
| `ReaperForm` | `Reaper Form` | 收割者形态 |
| `Resilience` | `Resilience` | 韧性 |
| `RoseOfTrinsic` | `Rose Of Trinsic` | 特林西克玫瑰 |
| `RotwormBloodDisease` | `Rotworm Blood Disease` | 腐虫血液病 |
| `RuneBeetleCorruption` | `Rune Beetle Corruption` | 符文甲虫腐化 |
| `SakkhraProphylaxis` | `Sakkhra Prophylaxis` | 萨克拉预防剂 |
| `SavingThrow` | `Saving Throw` | 豁免 |
| `Shadow` | `Shadow` | 暗影 |
| `ShieldBash` | `Shield Bash` | 盾击 |
| `SkillUseDelay` | `Skill Use Delay` | 技能使用延迟 |
| `Sleep` | `Sleep` | 睡眠 |
| `SpellFocusing` | `Spell Focusing` | 法术聚焦 |
| `SpellFocusingTarget` | `Spell Focusing (target)` | 法术聚焦（目标） |
| `SpellPlague` | `Spell Plague` | 法术瘟疫 |
| `SplinteringEffect` | `Splintering Effect` | 碎裂效果 |
| `Stagger` | `Stagger` | 踉跄 |
| `StoneForm` | `Stone Form` | 石化形态 |
| `Strangle` | `Strangle` | 绞杀 |
| `Strength` | `Strength` | 力量术 |
| `Surge` | `Surge` | 涌动 |
| `SwingSpeed` | `Swing Speed` | 攻击速度 |
| `TalonStrike` | `Talon Strike` | 利爪打击 |
| `Thrust` | `Thrust` | 突刺 |
| `ThrustDebuff` | `Thrust Debuff` | 突刺（减益） |
| `Tolerance` | `Tolerance` | 耐受 |
| `Toughness` | `Toughness` | 强韧 |
| `Tribulation` | `Tribulation` | 磨难 |
| `TribulationTarget` | `TribulationTarget` | 磨难（目标） |
| `UnknownTomato` | `Unknown Tomato` | 未知番茄效果 |
| `UraliTranceTonic` | `Urali Trance Tonic,` | 乌拉利入神药剂 |
| `VampiricEmbrace` | `Vampiric Embrace` | 吸血鬼拥抱 |
| `Veterinary` | `Veterinary` | 兽医 |
| `Warcry` | `Warcry` | 战吼 |
| `Weaken` | `Weaken` | 虚弱术 |
| `Whispering` | `Whispering` | 低语 |
| `WhiteTigerForm` | `White Tiger Form` | 白虎形态 |
| `WraithForm` | `Wraith Form` | 幽魂形态 |

## 6. 已知源码注意事项

1. `BuffInfo.DescriptionArgs` 和 `BuffInfo.ExtraInfoArgs` 当前错误地读取了 `TitleArgs`。在源码修复前，这两个字段可能不是服务器实际发送的说明/附加参数。
2. `Remaining` 没有自动限制为 `0`，Buff 过期后可能出现负数，脚本显示时建议使用 `max(0, info.Remaining)`。
3. `Duration == 0` 通常表示无倒计时或服务器未提供时长，此时 `Remaining` 不适合作为存在性判断。
4. `Player.Buffs`、`Player.BuffsInfo` 和 `Player.BuffTime` 没有完整的未登录保护，应在人物进入世界后调用。
5. 中文界面名称和英文 API 名是两套用途；脚本参数必须使用英文 API 名或精确源码枚举名。

## 7. C# 脚本示例

```csharp
using System;
using RazorEnhanced;

if (Player.BuffsExist("Poison", false))
{
    BuffInfo info = Player.GetBuffInfo("Poison", false);
    Misc.SendMessage($"中毒剩余时间: {Math.Max(0, info.Remaining)} ms");
}
```

## 8. UOSteam 宏条件

UOSteam 引擎也通过相同的 `Player.BuffsExist` 实现 `buffexists` 条件：

```text
if buffexists 'Poison'
    sysmsg '人物处于中毒状态'
endif
```

UOSteam 条件同样建议使用上表中的英文 API 名。
