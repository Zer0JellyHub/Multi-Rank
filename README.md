# 🎮 MultiRank – Jellyfin Plugin (not yet working)

> **Requirement:** [Playback Reporting Plugin](https://github.com/jellyfin/jellyfin-plugin-playbackreporting) must be installed.

---

## ✨ Features

| Genre | Ranks | |
|---|---|---|
| **Isekai** | Farmer → Villager → Apprentice → Adventurer F/E/D → Warrior → Knight → Nobleman → Baron → Count → Duke → King → Legendary → **Hero** | 15 Ranks |
| **Adventurer's Guild** | Copper → Iron → Silver → Gold → Platinum → Mithril → Orichalcum → Adamantite → **Diamond** *(Bonus)* | 9 Ranks |
| **Fortnite Ranked** | Bronze → Silver → Gold → Platinum → Diamond → Elite → Champion → **Unreal** | 8 Ranks, no Sub-Divisions |
| **Waifu Tier** | 6 Slots – upload your own images, they become icons | |
| **Custom** | Create your own genres with any ranks directly in the UI | |

- 📊 **XP System** – XP/Minute + completion bonus (Anti-Cheat: real watch time only)
- 🔥 **Watch-Session Bonus** – Watch ≥ 90 min in one day to get +25% XP
- 🗓️ **Seasons** – Winter/Spring/Summer/Autumn (automatic, no manual reset needed)
- 🏆 **Year in Review** – displayed at the end of the Winter Season
- ✨ **Prestige** – XP reset after the top rank with a Prestige Badge
- 🌍 **DE / EN** – switch language via button in the UI

---

## 🚀 Installation

### Jellyfin Plugin Repository *(recommended)*

1. Open Jellyfin Dashboard → **Settings → Plugins → Repositories**
2. Add the following URL as a new repository:
```
   https://raw.githubusercontent.com/Zer0JellyHub/Multi-Rank/main/manifest.json
```
3. Open **Catalog** → search for **MultiRank** → install
4. Restart Jellyfin
5. Open **🎮 MultiRank** in the side menu

---

## ⚙️ Admin Settings

Dashboard → Plugins → MultiRank

| Setting | Default | Description |
|---|---|---|
| XP/Minute | 2 | Real watch time |
| XP/Episode | 20 | Completion bonus |
| XP/Movie | 50 | Completion bonus |
| Min. Completion % | 80 % | Anti-Cheat |
| Session Bonus | ON | Watch marathon bonus |
| Session Threshold | 90 min | Minimum watchtime/day |
| Session Bonus % | 25 % | Bonus multiplier |

---

## 🏰 Adventurer's Guild Ranks (Overlord-Style)

| # | Rank | XP | Description |
|---|---|---|---|
| 1 | 🪙 Copper | 0 | Beginner – nobody knows your name |
| 2 | ⚙️ Iron | 40,000 | Newcomer – survived the first quests |
| 3 | 🥈 Silver | 120,000 | Experienced – the receptionist knows you |
| 4 | 🥇 Gold | 300,000 | Like Climb – the limit for untalented people |
| 5 | 💠 Platinum | 700,000 | Elite level – group leader |
| 6 | 🔵 Mithril | 1,500,000 | Local celebrity |
| 7 | 🟠 Orichalcum | 3,000,000 | Rarely seen – kings take notice |
| 8 | 🔱 Adamantite | 5,000,000 | Highest official rank – like Momon |
| 9 | 💎 **Diamond** | 6,500,000 | **Bonus Rank** – beyond all classification |

---

## 🔌 API

| Method | Endpoint | |
|---|---|---|
| GET | `/MultiRank/Me` | Own rank + XP |
| GET | `/MultiRank/Leaderboard?season=true` | Leaderboard |
| GET | `/MultiRank/Genres` | All genres |
| POST | `/MultiRank/Genre/SetActive` | Switch genre |
| POST | `/MultiRank/Genre/Custom` | Custom genre (Admin) |
| DELETE | `/MultiRank/Genre/Custom/{id}` | Delete (Admin) |
| POST | `/MultiRank/Waifu/UploadIcon/{genreId}/{i}` | Upload image (Admin) |
| POST | `/MultiRank/Prestige` | Prestige! |
| GET | `/MultiRank/Season` | Current season |
| GET | `/MultiRank/Season/YearEnd/{year}` | Year in review |
| GET | `/MultiRank/History` | Own season history |

---

## 📄 License

AGPL-3.0
