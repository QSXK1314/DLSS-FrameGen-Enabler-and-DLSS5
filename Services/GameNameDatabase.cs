using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DLSSFrameGenEnabler_WinUI3.Services;

/// <summary>
/// 游戏名称数据库：根据exe文件名识别真正的游戏名称
/// 中文界面优先用中文名称（如果中文更出名），英文界面统一用英文名称
/// </summary>
public static class GameNameDatabase
{
    // 映射：exe文件名（小写，不含.exe） -> (中文名称, 英文名称, 是否中文更出名)
    private static readonly Dictionary<string, (string Chinese, string English, bool ChineseFamous)> _database = new()
    {
        // 异环
        ["htgame"] = ("异环", "Neverness To Everness", true),
        // 赛博朋克2077
        ["cyberpunk2077"] = ("赛博朋克2077", "Cyberpunk 2077", true),
        // 黑神话悟空
        ["b1"] = ("黑神话：悟空", "Black Myth: Wukong", true),
        ["blackmyth-wukong"] = ("黑神话：悟空", "Black Myth: Wukong", true),
        // 影之刃零
        ["phantombladezero"] = ("影之刃零", "Phantom Blade Zero", true),
        ["pbz"] = ("影之刃零", "Phantom Blade Zero", true),
        ["pbz-win64"] = ("影之刃零", "Phantom Blade Zero", true),
        ["yingzhirenling"] = ("影之刃零", "Phantom Blade Zero", true),
        ["phantomblade"] = ("影之刃零", "Phantom Blade Zero", true),
        // 燕云十六声
        ["yyshotsgame"] = ("燕云十六声", "Where Winds Meet", true),
        ["yygame"] = ("燕云十六声", "Where Winds Meet", true),
        // 原神
        ["genshinimpact"] = ("原神", "Genshin Impact", true),
        ["yuanshen"] = ("原神", "Genshin Impact", true),
        // 崩坏：星穹铁道
        ["starrail"] = ("崩坏：星穹铁道", "Honkai: Star Rail", true),
        // 绝区零
        ["zenlesszonezero"] = ("绝区零", "Zenless Zone Zero", true),
        // 永劫无间
        ["naraka"] = ("永劫无间", "Naraka: Bladepoint", true),
        ["narakabladepoint"] = ("永劫无间", "Naraka: Bladepoint", true),
        // 艾尔登法环
        ["eldenring"] = ("艾尔登法环", "Elden Ring", true),
        // 黑暗之魂3
        ["darksoulsiii"] = ("黑暗之魂3", "Dark Souls III", true),
        // 只狼
        ["sekiro"] = ("只狼：影逝二度", "Sekiro: Shadows Die Twice", true),
        // 生化危机4重制版
        ["re4"] = ("生化危机4 重制版", "Resident Evil 4", true),
        ["re4remake"] = ("生化危机4 重制版", "Resident Evil 4", true),
        // 生化危机2重制版
        ["re2"] = ("生化危机2 重制版", "Resident Evil 2", true),
        // 生化危机3重制版
        ["re3"] = ("生化危机3 重制版", "Resident Evil 3", true),
        // 生化危机8
        ["re8"] = ("生化危机8：村庄", "Resident Evil Village", true),
        ["re村庄"] = ("生化危机8：村庄", "Resident Evil Village", true),
        // 鬼泣5
        ["dmc5"] = ("鬼泣5", "Devil May Cry 5", true),
        // 怪物猎人世界
        ["monsterhunterworld"] = ("怪物猎人：世界", "Monster Hunter: World", true),
        // 怪物猎人崛起
        ["monsterhunterrise"] = ("怪物猎人：崛起", "Monster Hunter Rise", true),
        // 荒野大镖客2
        ["rdr2"] = ("荒野大镖客：救赎2", "Red Dead Redemption 2", true),
        // GTA5
        ["gta5"] = ("侠盗猎车手5", "Grand Theft Auto V", true),
        ["gtav"] = ("侠盗猎车手5", "Grand Theft Auto V", true),
        // 巫师3
        ["witcher3"] = ("巫师3：狂猎", "The Witcher 3: Wild Hunt", true),
        // 星空
        ["starfield"] = ("星空", "Starfield", true),
        // 上古卷轴5
        ["skyrim"] = ("上古卷轴5：天际", "The Elder Scrolls V: Skyrim", true),
        // 辐射4
        ["fallout4"] = ("辐射4", "Fallout 4", true),
        // 赛博朋克2077的另一个exe名
        ["cyberpunk2077.exe"] = ("赛博朋克2077", "Cyberpunk 2077", true),
        // 地平线5
        ["forzahorizon5"] = ("极限竞速：地平线5", "Forza Horizon 5", true),
        // 地平线4
        ["forzahorizon4"] = ("极限竞速：地平线4", "Forza Horizon 4", true),
        // 微软飞行模拟
        ["flightsimulator"] = ("微软飞行模拟", "Microsoft Flight Simulator", true),
        // 光环无限
        ["haloinfinite"] = ("光环：无限", "Halo Infinite", true),
        // 极限竞速8
        ["forzamotorsport"] = ("极限竞速", "Forza Motorsport", true),
        // 地狱之刃2
        ["senua'ssaga"] = ("地狱之刃2：塞娜的传说", "Senua's Saga: Hellblade II", false),
        // 阿凡达：潘多拉边境
        ["avatar"] = ("阿凡达：潘多拉边境", "Avatar: Frontiers of Pandora", true),
        // 心灵杀手2
        ["alanwake2"] = ("心灵杀手2", "Alan Wake 2", true),
        // 博德之门3
        ["baldursgate3"] = ("博德之门3", "Baldur's Gate 3", true),
        // 龙之信条2
        ["dragonsdogma2"] = ("龙之信条2", "Dragon's Dogma 2", true),
        // 最终幻想7重制版
        ["ff7remake"] = ("最终幻想7 重制版", "Final Fantasy VII Remake", true),
        // 最终幻想16
        ["finalfantasyxvi"] = ("最终幻想16", "Final Fantasy XVI", true),
        // 最终幻想15
        ["finalfantasyxv"] = ("最终幻想15", "Final Fantasy XV", true),
        // 古墓丽影暗影
        ["shadowofthetombraider"] = ("古墓丽影：暗影", "Shadow of the Tomb Raider", true),
        // 刺客信条英灵殿
        ["acvalhalla"] = ("刺客信条：英灵殿", "Assassin's Creed Valhalla", true),
        // 刺客信条幻景
        ["acmirage"] = ("刺客信条：幻景", "Assassin's Creed Mirage", true),
        // 孤岛惊魂6
        ["farcry6"] = ("孤岛惊魂6", "Far Cry 6", true),
        // 彩虹六号异种
        ["rainbowsixextraction"] = ("彩虹六号：异种", "Tom Clancy's Rainbow Six Extraction", true),
        // 彩虹六号围攻
        ["rainbowsix"] = ("彩虹六号：围攻", "Tom Clancy's Rainbow Six Siege", true),
        // 荣耀战魂
        ["forhonor"] = ("荣耀战魂", "For Honor", true),
        // 飙酷车神2
        ["thecrew2"] = ("飙酷车神2", "The Crew 2", true),
        // 渡神纪
        ["immortals"] = ("渡神纪：芬尼斯崛起", "Immortals Fenyx Rising", true),
        // 看门狗军团
        ["watchdogslegion"] = ("看门狗：军团", "Watch Dogs: Legion", true),
        // 幽灵行动断点
        ["ghostreconbreakpoint"] = ("幽灵行动：断点", "Tom Clancy's Ghost Recon Breakpoint", true),
        // 全境封锁2
        ["thedivision2"] = ("全境封锁2", "Tom Clancy's The Division 2", true),
        // 无主之地3
        ["borderlands3"] = ("无主之地3", "Borderlands 3", true),
        // 无主之地4
        ["borderlands4"] = ("无主之地4", "Borderlands 4", true),
        // 遗迹2
        ["remnant2"] = ("遗迹2", "Remnant II", true),
        // 死亡空间重制版
        ["deadspace"] = ("死亡空间 重制版", "Dead Space", true),
        // 木卫四协议
        ["thecallistoprotocol"] = ("木卫四协议", "The Callisto Protocol", true),
        // 哥谭骑士
        ["gothamknights"] = ("哥谭骑士", "Gotham Knights", true),
        // 自杀小队：杀死正义联盟
        ["suicidesquad"] = ("自杀小队：杀死正义联盟", "Suicide Squad: Kill the Justice League", true),
        // 霍格沃茨之遗
        ["hogwartslegacy"] = ("霍格沃茨之遗", "Hogwarts Legacy", true),
        // 死亡岛2
        ["deadisland2"] = ("死亡岛2", "Dead Island 2", true),
        // 消逝的光芒2
        ["dyinglight2"] = ("消逝的光芒2：人与仁之战", "Dying Light 2 Stay Human", true),
        // 消逝的光芒
        ["dyinglight"] = ("消逝的光芒", "Dying Light", true),
        // 地铁离去
        ["metroexodus"] = ("地铁：离去", "Metro Exodus", true),
        // 控制
        ["control"] = ("控制", "Control", true),
        // 量子破碎
        ["quantumbreak"] = ("量子破碎", "Quantum Break", true),
        // 心灵杀手
        ["alanwake"] = ("心灵杀手", "Alan Wake", true),
        // 马克思佩恩3
        ["maxpayne3"] = ("马克思佩恩3", "Max Payne 3", true),
        // 荒野大镖客1
        ["rdr"] = ("荒野大镖客：救赎", "Red Dead Redemption", true),
        // GTA4
        ["gtaiv"] = ("侠盗猎车手4", "Grand Theft Auto IV", true),
        // 黑手党3
        ["mafia3"] = ("黑手党3", "Mafia III", true),
        // 黑手党1重制版
        ["mafia"] = ("黑手党：最终版", "Mafia: Definitive Edition", true),
        // 黑手党2最终版
        ["mafia2"] = ("黑手党2：最终版", "Mafia II: Definitive Edition", true),
        // 四海兄弟
        ["lostheaven"] = ("四海兄弟：故乡", "Mafia: The Old Country", true),
        // 杀手3
        ["hitman3"] = ("杀手3", "Hitman 3", true),
        // 杀手2
        ["hitman2"] = ("杀手2", "Hitman 2", true),
        // 杀手
        ["hitman"] = ("杀手", "Hitman", true),
        // 正当防卫4
        ["justcause4"] = ("正当防卫4", "Just Cause 4", true),
        // 正当防卫3
        ["justcause3"] = ("正当防卫3", "Just Cause 3", true),
        // 疯狂的麦克斯
        ["madmax"] = ("疯狂的麦克斯", "Mad Max", true),
        // 中土世界：战争之影
        ["shadowofwar"] = ("中土世界：战争之影", "Middle-earth: Shadow of War", true),
        // 中土世界：暗影魔多
        ["shadowofmordor"] = ("中土世界：暗影魔多", "Middle-earth: Shadow of Mordor", true),
        // 蝙蝠侠：阿卡姆骑士
        ["batmanarkhamknight"] = ("蝙蝠侠：阿卡姆骑士", "Batman: Arkham Knight", true),
        // 蝙蝠侠：阿卡姆之城
        ["batmanarkhamcity"] = ("蝙蝠侠：阿卡姆之城", "Batman: Arkham City", true),
        // 蝙蝠侠：阿卡姆起源
        ["batmanarkhamorigins"] = ("蝙蝠侠：阿卡姆起源", "Batman: Arkham Origins", true),
        // 乐高星球大战
        ["legostarwars"] = ("乐高星球大战：天行者传奇", "LEGO Star Wars: The Skywalker Saga", true),
        // 极品飞车22
        ["needforspeedunbound"] = ("极品飞车：不羁", "Need for Speed Unbound", true),
        // 极品飞车21
        ["needforspeedheat"] = ("极品飞车：热度", "Need for Speed Heat", true),
        // 极品飞车20
        ["needforspeedpayback"] = ("极品飞车：复仇", "Need for Speed Payback", true),
        // 极品飞车19
        ["needforspeed"] = ("极品飞车", "Need for Speed", true),
        // 尘埃5
        ["dirt5"] = ("尘埃5", "DIRT 5", true),
        // 尘埃拉力赛2.0
        ["dirtrally2"] = ("尘埃拉力赛2.0", "DiRT Rally 2.0", true),
        // F1 23
        ["f123"] = ("F1 23", "F1 23", false),
        // F1 22
        ["f122"] = ("F1 22", "F1 22", false),
        // F1 2021
        ["f12021"] = ("F1 2021", "F1 2021", false),
        // 赛车计划3
        ["projectcars3"] = ("赛车计划3", "Project CARS 3", true),
        // 神力科莎：竞速
        ["assettocorsacompetizione"] = ("神力科莎：竞速", "Assetto Corsa Competizione", true),
        // 神力科莎
        ["assettocorsa"] = ("神力科莎", "Assetto Corsa", true),
        // 欧洲卡车模拟2
        ["eurotrucks2"] = ("欧洲卡车模拟2", "Euro Truck Simulator 2", true),
        // 美国卡车模拟
        ["americantrucksimulator"] = ("美国卡车模拟", "American Truck Simulator", true),
        // 农场模拟22
        ["farmingsimulator22"] = ("农场模拟22", "Farming Simulator 22", true),
        // 城市：天际线2
        ["citiesskylines2"] = ("城市：天际线2", "Cities: Skylines II", true),
        // 城市：天际线
        ["citiesskylines"] = ("城市：天际线", "Cities: Skylines", true),
        // 过山车之星
        ["planetcoaster"] = ("过山车之星", "Planet Coaster", true),
        // 动物园之星
        ["planetzoo"] = ("动物园之星", "Planet Zoo", true),
        // 双点医院
        ["twopointhospital"] = ("双点医院", "Two Point Hospital", true),
        // 双点校园
        ["twopointcampus"] = ("双点校园", "Two Point Campus", true),
        // 缺氧
        ["oxygennotincluded"] = ("缺氧", "Oxygen Not Included", true),
        // 饥荒
        ["dontstarve"] = ("饥荒", "Don't Starve", true),
        // 泰拉瑞亚
        ["terraria"] = ("泰拉瑞亚", "Terraria", true),
        // 星露谷物语
        ["stardewvalley"] = ("星露谷物语", "Stardew Valley", true),
        // 我的世界
        ["minecraft"] = ("我的世界", "Minecraft", true),
        // 环世界
        ["rimworld"] = ("环世界", "RimWorld", true),
        // 异星工厂
        ["factorio"] = ("异星工厂", "Factorio", true),
        // 幸福工厂
        ["satisfactory"] = ("幸福工厂", "Satisfactory", true),
        // 戴森球计划
        ["dysonsphereprogram"] = ("戴森球计划", "Dyson Sphere Program", true),
        // 鬼谷八荒
        ["guigubahuang"] = ("鬼谷八荒", "Tale of Immortal", true),
        // 太吾绘卷
        ["taiwu"] = ("太吾绘卷", "The Scroll of Taiwu", true),
        // 古剑奇谭3
        ["gujian3"] = ("古剑奇谭3", "GuJian3", true),
        // 仙剑奇侠传7
        ["swordandfairy7"] = ("仙剑奇侠传7", "Sword and Fairy 7", true),
        // 光明记忆：无限
        ["brightmemory"] = ("光明记忆：无限", "Bright Memory: Infinite", true),
        // 暗影火炬城
        ["fist"] = ("暗影火炬城", "F.I.S.T.: Forged In Shadow Torch", true),
        // 永劫无间的另一个exe名
        ["naraka: bladepoint"] = ("永劫无间", "Naraka: Bladepoint", true),
        // 无畏契约
        ["valorant"] = ("无畏契约", "VALORANT", true),
        // 英雄联盟
        ["leagueoflegends"] = ("英雄联盟", "League of Legends", true),
        // DOTA2
        ["dota2"] = ("刀塔2", "Dota 2", true),
        // CS2
        ["cs2"] = ("反恐精英2", "Counter-Strike 2", true),
        // CSGO
        ["csgo"] = ("反恐精英：全球攻势", "Counter-Strike: Global Offensive", true),
        // 守望先锋2
        ["overwatch"] = ("守望先锋2", "Overwatch 2", true),
        // 炉石传说
        ["hearthstone"] = ("炉石传说", "Hearthstone", true),
        // 魔兽世界
        ["wow"] = ("魔兽世界", "World of Warcraft", true),
        // 暗黑破坏神4
        ["diablo4"] = ("暗黑破坏神4", "Diablo IV", true),
        // 暗黑破坏神3
        ["diablo3"] = ("暗黑破坏神3", "Diablo III", true),
        // 暗黑破坏神2重制版
        ["diablo2resurrected"] = ("暗黑破坏神2：重制版", "Diablo II: Resurrected", true),
        // 风暴英雄
        ["heroesofthestorm"] = ("风暴英雄", "Heroes of the Storm", true),
        // 星际争霸2
        ["starcraft2"] = ("星际争霸2", "StarCraft II", true),
        // 魔兽争霸3重制版
        ["warcraft3reforged"] = ("魔兽争霸3：重制版", "Warcraft III: Reforged", true),
        // 命运2
        ["destiny2"] = ("命运2", "Destiny 2", true),
        //  Apex英雄
        ["apex"] = ("Apex英雄", "Apex Legends", true),
        // 泰坦陨落2
        ["titanfall2"] = ("泰坦陨落2", "Titanfall 2", true),
        // 战地5
        ["battlefieldv"] = ("战地5", "Battlefield V", true),
        // 战地1
        ["battlefield1"] = ("战地1", "Battlefield 1", true),
        // 战地2042
        ["battlefield2042"] = ("战地2042", "Battlefield 2042", true),
        // 使命召唤19
        ["cod19"] = ("使命召唤：现代战争2", "Call of Duty: Modern Warfare II", true),
        // 使命召唤20
        ["cod20"] = ("使命召唤：现代战争3", "Call of Duty: Modern Warfare III", true),
        // 使命召唤18
        ["cod18"] = ("使命召唤：先锋", "Call of Duty: Vanguard", true),
        // 使命召唤17
        ["cod17"] = ("使命召唤：黑色行动冷战", "Call of Duty: Black Ops Cold War", true),
        // 使命召唤16
        ["cod16"] = ("使命召唤：现代战争", "Call of Duty: Modern Warfare", true),
        // 绝地求生
        ["pubg"] = ("绝地求生", "PUBG: Battlegrounds", true),
        // 堡垒之夜
        ["fortnite"] = ("堡垒之夜", "Fortnite", true),
        // 糖豆人
        ["fallguys"] = ("糖豆人", "Fall Guys", true),
        //  among us
        ["amongus"] = ("在我们之中", "Among Us", true),
        // 我的世界地下城
        ["minecraftdungeons"] = ("我的世界：地下城", "Minecraft Dungeons", true),
        // 我的世界传奇
        ["minecraftlegends"] = ("我的世界：传奇", "Minecraft Legends", true),
        // 盗贼之海
        ["seaofthieves"] = ("盗贼之海", "Sea of Thieves", true),
        // 禁闭求生
        ["grounded"] = ("禁闭求生", "Grounded", true),
        // 脑航员2
        ["psychonauts2"] = ("脑航员2", "Psychonauts 2", true),
        // 意航员2
        ["意航员2"] = ("意航员2", "Psychonauts 2", true),
        // 双人成行
        ["ittakestwo"] = ("双人成行", "It Takes Two", true),
        // 逃出生天
        ["awayout"] = ("逃出生天", "A Way Out", true),
        // 兄弟：双子传说
        ["brothers"] = ("兄弟：双子传说", "Brothers: A Tale of Two Sons", true),
        // 毛线小精灵2
        ["unravel2"] = ("毛线小精灵2", "Unravel Two", true),
        // 植物大战僵尸
        ["plantsvszombies"] = ("植物大战僵尸", "Plants vs. Zombies", true),
        // 愤怒的小鸟
        ["angrybirds"] = ("愤怒的小鸟", "Angry Birds", true),
        // 割绳子
        ["cuttherope"] = ("割绳子", "Cut the Rope", true),
        // 水果忍者
        ["fruitninja"] = ("水果忍者", "Fruit Ninja", true),
        // 神庙逃亡
        ["templerun"] = ("神庙逃亡", "Temple Run", true),
        // 地铁跑酷
        ["subwaysurfers"] = ("地铁跑酷", "Subway Surfers", true),
        // 愤怒的小鸟2
        ["angrybirds2"] = ("愤怒的小鸟2", "Angry Birds 2", true),
        // 植物大战僵尸2
        ["plantsvszombies2"] = ("植物大战僵尸2", "Plants vs. Zombies 2", true),
        // 王者荣耀
        ["honorofkings"] = ("王者荣耀", "Honor of Kings", true),
        // 和平精英
        ["gameforpeace"] = ("和平精英", "Game for Peace", true),
        // 阴阳师
        ["onmyoji"] = ("阴阳师", "Onmyoji", true),
        // 第五人格
        ["identityv"] = ("第五人格", "Identity V", true),
        // 明日方舟
        ["arknights"] = ("明日方舟", "Arknights", true),
        // 原神的另一个exe名
        ["genshin"] = ("原神", "Genshin Impact", true),
        // 崩坏3
        ["honkai3"] = ("崩坏3", "Honkai Impact 3rd", true),
        // 崩坏学园2
        ["gunsschool"] = ("崩坏学园2", "Guns GirlZ", true),
        // 未定事件簿
        ["tearsofthemis"] = ("未定事件簿", "Tears of Themis", true),
        // 恋与制作人
        ["loveandproducer"] = ("恋与制作人", "Love and Producer", true),
        // 闪耀暖暖
        ["shiningnikki"] = ("闪耀暖暖", "Shining Nikki", true),
        // 奇迹暖暖
        ["miraclenikki"] = ("奇迹暖暖", "Miracle Nikki", true),
        // 暖暖环游世界
        ["nikkiup2u"] = ("暖暖环游世界", "Nikki Up2U", true),
        // 无限暖暖
        ["infinitynikki"] = ("无限暖暖", "Infinity Nikki", true),
        // 鸣潮
        ["wutheringwaves"] = ("鸣潮", "Wuthering Waves", true),
        // 绝区零的另一个exe名
        ["zzz"] = ("绝区零", "Zenless Zone Zero", true),
        // 重返未来1999
        ["reverse1999"] = ("重返未来：1999", "Reverse: 1999", true),
        // 白夜极光
        ["alchemystars"] = ("白夜极光", "Alchemy Stars", true),
        // 深空之眼
        ["aethergazer"] = ("深空之眼", "Aether Gazer", true),
        // 战双帕弥什
        ["punishinggrayraven"] = ("战双帕弥什", "Punishing: Gray Raven", true),
        // 幻塔
        ["toweroffantasy"] = ("幻塔", "Tower of Fantasy", true),
        // 天谕
        ["revelation"] = ("天谕", "Revelation", true),
        // 逆水寒
        ["justiceonline"] = ("逆水寒", "Justice Online", true),
        // 一梦江湖
        ["dreamofjianghu"] = ("一梦江湖", "Dream of Jianghu", true),
        // 楚留香
        ["crlm"] = ("楚留香", "Chu Liuxiang", true),
        // 天涯明月刀
        ["moonlightblade"] = ("天涯明月刀", "Moonlight Blade", true),
        // 剑网3
        ["jx3"] = ("剑网3", "Jian Wang 3", true),
        // 剑网3缘起
        ["jx3origin"] = ("剑网3缘起", "Jian Wang 3 Origin", true),
        // 剑网1归来
        ["jx1return"] = ("剑网1归来", "Jian Wang 1 Return", true),
        // 剑网2
        ["jx2"] = ("剑网2", "Jian Wang 2", true),
        // 剑侠世界3
        ["jsxsj3"] = ("剑侠世界3", "Jian Xia Shi Jie 3", true),
        // 剑侠情缘2剑歌行
        ["jxqy2"] = ("剑侠情缘2：剑歌行", "Jian Xia Qing Yuan 2", true),
        // 新剑侠情缘
        ["xjxqy"] = ("新剑侠情缘", "Xin Jian Xia Qing Yuan", true),
        // 剑侠情缘
        ["jxqy"] = ("剑侠情缘", "Jian Xia Qing Yuan", true),
        // 月影传说
        ["yueyingchuanshuo"] = ("月影传说", "Yue Ying Chuan Shuo", true),
        // 新剑侠传奇
        ["xjxcq"] = ("新剑侠传奇", "Xin Jian Xia Chuan Qi", true),
        // 剑网3指尖江湖
        ["jx3zhijian"] = ("剑网3：指尖江湖", "Jian Wang 3: Zhi Jian Jiang Hu", true),
        // 剑网3指尖对弈
        ["jx3duiyi"] = ("剑网3：指尖对弈", "Jian Wang 3: Zhi Jian Dui Yi", true),
        // 剑网3口袋版
        ["jx3koudai"] = ("剑网3口袋版", "Jian Wang 3 Kou Dai Ban", true),
        // 剑网3手游
        ["jx3shouyou"] = ("剑网3手游", "Jian Wang 3 Shou You", true),
        // 剑网3重制版
        ["jx3chongzhi"] = ("剑网3重制版", "Jian Wang 3 Chong Zhi Ban", true),
        // 剑网3怀旧服
        ["jx3huaijiu"] = ("剑网3怀旧服", "Jian Wang 3 Huai Jiu Fu", true),
        // 剑网3国际服
        ["jx3guoji"] = ("剑网3国际服", "Jian Wang 3 Guo Ji Fu", true),
        // 剑网3台服
        ["jx3taifu"] = ("剑网3台服", "Jian Wang 3 Tai Fu", true),
        // 剑网3港澳服
        ["jx3gangao"] = ("剑网3港澳服", "Jian Wang 3 Gang Ao Fu", true),
        // 剑网3东南亚服
        ["jx3dongnanya"] = ("剑网3东南亚服", "Jian Wang 3 Dong Nan Ya Fu", true),
        // 剑网3欧美服
        ["jx3oumei"] = ("剑网3欧美服", "Jian Wang 3 Ou Mei Fu", true),
        // 剑网3日韩服
        ["jx3rihan"] = ("剑网3日韩服", "Jian Wang 3 Ri Han Fu", true),
        // 剑网3全球服
        ["jx3quanqiu"] = ("剑网3全球服", "Jian Wang 3 Quan Qiu Fu", true),
        // 呼吸边缘2
        ["breathedge2"] = ("呼吸边缘2", "Breathedge 2", true),
        // 呼吸边缘
        ["breathedge"] = ("呼吸边缘", "Breathedge", true),
        // 深海迷航
        ["subnautica"] = ("深海迷航", "Subnautica", true),
        // 深海迷航：零度之下
        ["subnauticabelowzero"] = ("深海迷航：零度之下", "Subnautica: Below Zero", true),
        // 无人深空
        ["nomanssky"] = ("无人深空", "No Man's Sky", true),
        // ICARUS
        ["icarus"] = ("翼星求生", "ICARUS", true),
        // 007：无暇赴死
        ["007firstlight"] = ("007：初露锋芒", "007: First Light", true),
        // 赛博朋克2077的exe
        ["cyberpunk2077 (2)"] = ("赛博朋克2077", "Cyberpunk 2077", true),
    };

    /// <summary>
    /// 根据exe路径获取游戏名称
    /// </summary>
    /// <param name="exePath">exe文件路径</param>
    /// <param name="isEnglish">是否英文界面</param>
    /// <returns>游戏名称，如果数据库中没有则返回null</returns>
    public static string? GetGameName(string exePath, bool isEnglish)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return null;

        string exeFileName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant().Trim();

        if (_database.TryGetValue(exeFileName, out var info))
        {
            // 英文界面统一用英文名称
            if (isEnglish)
                return info.English;

            // 中文界面：如果中文更出名就用中文，否则用英文
            return info.ChineseFamous ? info.Chinese : info.English;
        }

        return null;
    }

    /// <summary>
    /// 根据exe文件名（不含路径）获取游戏名称
    /// </summary>
    public static string? GetGameNameByFileName(string exeFileName, bool isEnglish)
    {
        if (string.IsNullOrEmpty(exeFileName))
            return null;

        string key = Path.GetFileNameWithoutExtension(exeFileName).ToLowerInvariant().Trim();

        if (_database.TryGetValue(key, out var info))
        {
            if (isEnglish)
                return info.English;
            return info.ChineseFamous ? info.Chinese : info.English;
        }

        return null;
    }

    /// <summary>名字里是否含汉字。</summary>
    public static bool HasChinese(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        foreach (char c in text)
        {
            if (c >= '\u4e00' && c <= '\u9fff')
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 英文界面的兜底：当前名字含中文、又没被上面的数据库收录时，改用不含中文的目录名或 exe 文件名。
    /// 都找不到合适的英文名就保留原值——宁可显示中文，也不要显示空名字。
    /// </summary>
    public static string EnglishFallback(string current, string? exePath, string gamePath)
    {
        if (!HasChinese(current))
        {
            return current;
        }
        string dirName = Path.GetFileName(gamePath.TrimEnd('\\', '/'));
        if (!string.IsNullOrWhiteSpace(dirName) && !HasChinese(dirName) && dirName.Length > 2)
        {
            return dirName;
        }
        if (!string.IsNullOrEmpty(exePath))
        {
            string exeName = Path.GetFileNameWithoutExtension(exePath);
            if (!string.IsNullOrWhiteSpace(exeName) && !HasChinese(exeName) && exeName.Length > 2)
            {
                return exeName;
            }
        }
        return current;
    }

    /// <summary>
    /// 把"正好等于数据库里某个官方中文名"的名字换成英文名（英文界面切换语言、读取旧列表时用）。
    /// 只认完全相同的官方中文名，所以不会动用户自己改过的名字。
    /// </summary>
    public static string TranslateToEnglishIfKnown(string name)
    {
        if (!HasChinese(name))
        {
            return name;
        }
        string trimmed = name.Trim();
        foreach (var item in _database)
        {
            if (item.Value.Chinese.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return item.Value.English;
            }
        }
        return name;
    }

    /// <summary>
    /// 尝试从exe的文件版本信息中获取产品名称
    /// </summary>
    public static string? GetProductNameFromExe(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return null;
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            string? productName = versionInfo.ProductName;
            if (!string.IsNullOrWhiteSpace(productName) && productName.Length > 1)
            {
                // 过滤掉一些明显不是游戏名称的产品名
                if (!productName.Contains("Windows", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("AMD", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("Intel", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("Unity", StringComparison.OrdinalIgnoreCase) &&
                    !productName.Contains("Unreal", StringComparison.OrdinalIgnoreCase))
                {
                    return productName;
                }
            }
        }
        catch { }
        return null;
    }
}
