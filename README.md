# 🎮 MultiRank – Jellyfin Plugin

> **Voraussetzung:** [Playback Reporting Plugin](https://github.com/jellyfin/jellyfin-plugin-playbackreporting) muss installiert sein.

---

## ✨ Features

| Genre | Ränge | |
|-------|-------|-|
| **Isekai** | Bauer → Dorfbewohner → Lehrling → Abenteurer F/E/D → Krieger → Ritter → Edelmann → Baron → Graf → Herzog → König → Legendär → **Held** | 15 Ränge |
| **Abenteurer-Gilde** | Kupfer → Eisen → Silber → Gold → Platin → Mithril → Orichalcum → Adamantit → **Diamant** *(Bonus)* | 9 Ränge |
| **Fortnite Ranked** | Bronze → Silber → Gold → Platin → Diamant → Elite → Champion → **Unreal** | 8 Ränge, keine Sub-Divisionen |
| **Waifu Tier** | 6 Slots – eigene Bilder hochladen, werden zu Icons | |
| **Custom** | Eigene Genres mit beliebigen Rängen direkt in der UI erstellen | |

- 📊 **XP-System** – XP/Minute + Abschluss-Bonus (Anti-Cheat: nur echte Schauzeit)
- 🔥 **Watch-Session Bonus** – Wer an einem Tag ≥ 90 Min schaut bekommt +25% XP
- 🗓️ **Seasons** – Winter/Frühling/Sommer/Herbst (automatisch, kein manuelles Reset nötig)
- 🏆 **Jahres-Rückblick** – wird am Ende der Winter-Season eingeblendet
- ✨ **Prestige** – XP Reset nach dem Top-Rang mit Prestige-Badge
- 🌍 **DE / EN** – Sprache per Knopf in der UI umschalten

---

## 🚀 Installation

### Option A – ZIP aus GitHub Releases

1. Neueste ZIP-Datei von der [Releases-Seite](../../releases) herunterladen
2. Alle DLL-Dateien in den Plugin-Ordner kopieren:
   - **Linux:**   `~/.local/share/jellyfin/plugins/MultiRank/`
   - **Windows:** `%APPDATA%\jellyfin\plugins\MultiRank\`
   - **Docker:**  `/config/plugins/MultiRank/`
3. Jellyfin neu starten
4. **🎮 MultiRank** im Seitenmenü öffnen

### Option B – Selbst kompilieren

```bash
git clone https://github.com/YOUR-USERNAME/MultiRank
cd MultiRank
dotnet publish --configuration Release --output ./publish
# Alle Dateien aus ./publish/ in den Plugin-Ordner kopieren
```

### Option C – Jellyfin Plugin-Repository

In den Dashboard-Einstellungen folgende URL als Repository hinzufügen:

```
https://raw.githubusercontent.com/YOUR-USERNAME/MultiRank/main/manifest.json
```

---

## ⚙️ Admin-Einstellungen

Dashboard → Plugins → MultiRank

| Einstellung | Standard | Beschreibung |
|-------------|---------|--------------|
| XP/Minute | 2 | Echte Schauzeit |
| XP/Episode | 20 | Abschluss-Bonus |
| XP/Film | 50 | Abschluss-Bonus |
| Min. Abschluss % | 80 % | Anti-Cheat |
| Session-Bonus | EIN | Watch-Marathon Bonus |
| Session-Schwelle | 90 Min | Mindest-Watchtime/Tag |
| Session-Bonus % | 25 % | Aufschlag |

---

## 🏰 Abenteurer-Gilde Ränge (Overlord-Style)

| # | Rang | XP | Beschreibung |
|---|------|----|-------------|
| 1 | 🪙 Kupfer     | 0           | Anfänger – niemand kennt deinen Namen |
| 2 | ⚙️ Eisen      | 40.000      | Einsteiger – erste Quests überlebt |
| 3 | 🥈 Silber     | 120.000     | Erfahren – die Rezeptionistin kennt dich |
| 4 | 🥇 Gold       | 300.000     | Wie Climb – Limit für talentlose Menschen |
| 5 | 💠 Platin     | 700.000     | Elitestufe – Gruppenführer |
| 6 | 🔵 Mithril    | 1.500.000   | Lokale Berühmtheit |
| 7 | 🟠 Orichalcum | 3.000.000   | Selten gesehen – Könige horchen auf |
| 8 | 🔱 Adamantit  | 5.000.000   | Höchster offizieller Rang – wie Momon |
| 9 | 💎 **Diamant** | 6.500.000  | **Bonus-Rang** – jenseits jeder Klassifikation |

---

## 🔌 API

| Method | Endpoint | |
|--------|----------|-|
| GET  | `/MultiRank/Me`                              | Eigener Rang + XP |
| GET  | `/MultiRank/Leaderboard?season=true`         | Rangliste |
| GET  | `/MultiRank/Genres`                          | Alle Genres |
| POST | `/MultiRank/Genre/SetActive`                 | Genre wechseln |
| POST | `/MultiRank/Genre/Custom`                    | Custom Genre (Admin) |
| DELETE | `/MultiRank/Genre/Custom/{id}`             | Löschen (Admin) |
| POST | `/MultiRank/Waifu/UploadIcon/{genreId}/{i}` | Bild hochladen (Admin) |
| POST | `/MultiRank/Prestige`                        | Prestige! |
| GET  | `/MultiRank/Season`                          | Aktuelle Season |
| GET  | `/MultiRank/Season/YearEnd/{year}`           | Jahres-Rückblick |
| GET  | `/MultiRank/History`                         | Eigene Season-History |

---

## 📄 Lizenz

AGPL-3.0
